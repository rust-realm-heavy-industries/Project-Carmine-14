using Content.Client.Gameplay;
using Content.Shared.CombatMode;
using Content.Shared.Effects;
using Content.Shared.Hands.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.StatusEffect;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Wieldable.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Client.State;
using Robust.Shared.Input;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using System.Linq;

namespace Content.Client.Weapons.Melee;

public sealed partial class MeleeWeaponSystem : SharedMeleeWeaponSystem
{
    [Dependency] private readonly IPrototypeManager _protoManager = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IEntitySystemManager _entitySystemManager = default!;
    [Dependency] private readonly IOverlayManager _overlayManager = default!;
    [Dependency] private readonly IEyeManager _eyeManager = default!;
    [Dependency] private readonly IInputManager _inputManager = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IStateManager _stateManager = default!;
    [Dependency] private readonly AnimationPlayerSystem _animation = default!;
    [Dependency] private readonly InputSystem _inputSystem = default!;
    [Dependency] private readonly SharedColorFlashEffectSystem _color = default!;

    private EntityQuery<TransformComponent> _xformQuery;

    private const string MeleeLungeKey = "melee-lunge";

    public override void Initialize()
    {
        base.Initialize();
        _overlayManager.AddOverlay(new MeleeWindupOverlay(EntityManager, _timing, _player, _protoManager));
        _xformQuery = GetEntityQuery<TransformComponent>();
        SubscribeNetworkEvent<MeleeLungeEvent>(OnMeleeLunge);
        UpdatesOutsidePrediction = true;
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlayManager.RemoveOverlay<MeleeWindupOverlay>();
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);
        UpdateEffects();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!Timing.IsFirstTimePredicted)
            return;

        var entityNull = _player.LocalEntity;

        if (entityNull == null)
            return;

        var entity = entityNull.Value;

        if (!TryGetWeapon(entity, out var weaponUid, out var weapon))
            return;

        if (!CombatMode.IsInCombatMode(entity) || !Blocker.CanAttack(entity))
        {
            weapon.Attacking = false;
            RaisePredictiveEvent(new StopHeavyAttackEvent(GetNetEntity(weaponUid)));
            return;
        }

        var useDown = _inputSystem.CmdStates.GetState(EngineKeyFunctions.Use) == BoundKeyState.Down;            //LMB
        var altDown = _inputSystem.CmdStates.GetState(EngineKeyFunctions.UseSecondary) == BoundKeyState.Down;   //RMB
        TimeSpan? gracePeriod = null;

        // Disregard inputs to the shoot binding
        if (TryComp<GunComponent>(weaponUid, out var gun)
            && (!HasComp<GunRequiresWieldComponent>(weaponUid)
            || TryComp<WieldableComponent>(weaponUid, out var wieldable)
            && wieldable.Wielded))
        {
            if (gun.UseKey)
                useDown = false;
            else
                altDown = false;
        }

        if (!useDown)
        {
            if (weapon.Attacking)
            {
                RaisePredictiveEvent(new StopAttackEvent(GetNetEntity(weaponUid)));
                weapon.index = 1;
            }
        }

        if (weapon.Attacking || weapon.NextAttack > Timing.CurTime)
        {
            return;
        }

        // TODO using targeted actions while combat mode is enabled should NOT trigger attacks.

        var mousePos = _eyeManager.PixelToMap(_inputManager.MouseScreenPosition);
        EntityCoordinates coordinates;

        if (MapManager.TryFindGridAt(mousePos, out var gridUid, out _))
        {
            coordinates = TransformSystem.ToCoordinates(gridUid, mousePos);
        }
        else
        {
            var mapSystem = _entitySystemManager.GetEntitySystem<MapSystem>();
            coordinates = TransformSystem.ToCoordinates(mapSystem.GetMap(mousePos.MapId), mousePos);  //TODO: FIX 4000000 ERRORS IN CONSOLE WHEN HOVERING OVER UI
        }

        // LMB held
        if (useDown)
        {
            // If it's an unarmed attack then do a disarm
            if (weapon.AltDisarm && weaponUid == entity)
            {
                EntityUid? target = null;

                if (_stateManager.CurrentState is GameplayStateBase screen)
                {
                    target = screen.GetClickedEntity(mousePos);
                }

                EntityManager.RaisePredictiveEvent(new DisarmAttackEvent(GetNetEntity(target), GetNetCoordinates(coordinates)));
                return;
            }

            // Start a windup
            if (weapon.WindUpStart == null)
            {
                EntityManager.RaisePredictiveEvent(new StartHeavyAttackEvent(GetNetEntity(weaponUid)));
                weapon.WindUpStart = Timing.CurTime;
            }
        }

        // If we released LMB and there is a windup
        if (!useDown && weapon.WindUpStart != null)
        {
            // An attack within the GracePeriod is going to be registered as a light attack
            if ((weapon.WindUpStart > Timing.CurTime - weapon.GracePeriod))
            {
                // Light attack
                {

                    if (weapon.Attacking || weapon.NextAttack > Timing.CurTime)
                    {
                        return;
                    }

                    var attackerPos = TransformSystem.GetMapCoordinates(entity);

                    if (mousePos.MapId != attackerPos.MapId ||
                        (attackerPos.Position - mousePos.Position).Length() > weapon.Range * weapon.LightRangeModifier)
                    {
                        return;
                    }

                    EntityUid? target = null;

                    if (_stateManager.CurrentState is GameplayStateBase screen)
                        target = screen.GetClickedEntity(mousePos);

                    // Don't light-attack if interaction will be handling this instead
                    if (Interaction.CombatModeCanHandInteract(entity, target))
                        return;

                    RaisePredictiveEvent(new LightAttackEvent(GetNetEntity(target), GetNetEntity(weaponUid), GetNetCoordinates(coordinates)));
                    RaisePredictiveEvent(new StopHeavyAttackEvent(GetNetEntity(weaponUid)));
                    gracePeriod = null;
                }
            }
            else
            {
                ClientHeavyAttack(entity, coordinates, weaponUid, weapon);
                RaisePredictiveEvent(new StopHeavyAttackEvent(GetNetEntity(weaponUid)));
                gracePeriod = null;
            }
            return;
        }

    }

    protected override bool InRange(EntityUid user, EntityUid target, float range, ICommonSession? session)
    {
        var xform = Transform(target);
        var targetCoordinates = xform.Coordinates;
        var targetLocalAngle = xform.LocalRotation;

        return Interaction.InRangeUnobstructed(user, target, targetCoordinates, targetLocalAngle, range);
    }

    protected override void DoDamageEffect(List<EntityUid> targets, EntityUid? user, TransformComponent targetXform) =>
        _color.RaiseEffect(Color.Red, targets, Filter.Local());

    protected override bool DoDisarm(EntityUid user, DisarmAttackEvent ev, EntityUid meleeUid, MeleeWeaponComponent component, ICommonSession? session)
    {
        if (!base.DoDisarm(user, ev, meleeUid, component, session)
            || !TryComp<CombatModeComponent>(user, out var combatMode)
            || combatMode.CanDisarm != true)
            return false;

        var target = GetEntity(ev.Target);

        // They need to either have hands...
        if (!HasComp<HandsComponent>(target!.Value))
        {
            // or just be able to be shoved over.
            if (TryComp<StatusEffectsComponent>(target, out var status) && status.AllowedEffects.Contains("KnockedDown"))
                return true;

            if (Timing.IsFirstTimePredicted && HasComp<MobStateComponent>(target.Value))
                PopupSystem.PopupEntity(Loc.GetString("disarm-action-disarmable", ("targetName", target.Value)), target.Value);

            return false;
        }

        return true;
    }

    /// <summary>
    /// Raises a heavy attack event with the relevant attacked entities.
    /// This is to avoid lag effecting the client's perspective too much.
    /// </summary>
    private void ClientHeavyAttack(EntityUid user, EntityCoordinates coordinates, EntityUid meleeUid, MeleeWeaponComponent component)
    {
        // Only run on first prediction to avoid the potential raycast entities changing.
        if (!_xformQuery.TryGetComponent(user, out var userXform)
            || !Timing.IsFirstTimePredicted)
            return;

        var targetMap = TransformSystem.ToMapCoordinates(coordinates);
        if (targetMap.MapId != userXform.MapID)
            return;

        var userPos = TransformSystem.GetWorldPosition(userXform);
        var direction = targetMap.Position - userPos;
        var distance = MathF.Min(component.Range * component.HeavyRangeModifier, direction.Length());

        // This should really be improved. GetEntitiesInArc uses pos instead of bounding boxes.
        // Server will validate it with InRangeUnobstructed.
        var entities = GetNetEntityList(ArcRayCast(userPos, direction.ToWorldAngle(), component.Angle, distance, userXform.MapID, user).ToList());
        RaisePredictiveEvent(new HeavyAttackEvent(GetNetEntity(meleeUid), entities.GetRange(0, Math.Min(component.MaxTargets, entities.Count)), GetNetCoordinates(coordinates)));
    }

    private void OnMeleeLunge(MeleeLungeEvent ev)
    {
        var ent = GetEntity(ev.Entity);
        var entWeapon = GetEntity(ev.Weapon);

        // Entity might not have been sent by PVS.
        if (!Exists(ent) || Exists(entWeapon))
            return;

        DoLunge(ent, entWeapon, ev.Angle, ev.LocalPos, ev.Animation);
    }
}

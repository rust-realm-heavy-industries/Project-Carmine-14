using Robust.Shared.Serialization;

namespace Content.Shared.Weapons.Melee.Events;

[Serializable, NetSerializable]
public sealed class StartHeavyAttackEvent : EntityEventArgs
{
    public readonly NetEntity? Weapon;

    public StartHeavyAttackEvent(NetEntity? weapon)
    {
        Weapon = weapon;
    }
}

using Robust.Shared.Configuration;

namespace Content.Shared._White.CCVar;

public sealed partial class WhiteCVars
{
    //     public static readonly CVarDef<bool> LogInChat =
    //         CVarDef.Create("chat.log", true, CVar.CLIENT | CVar.ARCHIVE | CVar.REPLICATED); //.2 edit: im an idiot and cant figure out why this throws an exception, so it's auto-true.

    //     public static readonly CVarDef<bool> ChatFancyFont =
    //         CVarDef.Create("chat.fancy_font", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    //     public static readonly CVarDef<bool> ColoredBubbleChat =
    //         CVarDef.Create("chat.colored_bubble", true, CVar.CLIENTONLY | CVar.ARCHIVE);
    // }


    /// <summary>
    /// Whether or not to log actions in the chat.
    /// </summary>
    public static readonly CVarDef<bool> LogInChat =
        CVarDef.Create("chat.log_in_chat", true, CVar.CLIENT | CVar.ARCHIVE | CVar.REPLICATED);

    /// <summary>
    /// Whether or not to coalesce identical messages in the chat.
    /// </summary>
    public static readonly CVarDef<bool> CoalesceIdenticalMessages =
            CVarDef.Create("chat.coalesce_identical_messages", true, CVar.CLIENT | CVar.ARCHIVE | CVar.CLIENTONLY);

    /// <summary>
    /// Whether or not to show detailed examine text.
    /// </summary>
    public static readonly CVarDef<bool> DetailedExamine =
        CVarDef.Create("misc.detailed_examine", true, CVar.CLIENT | CVar.ARCHIVE | CVar.REPLICATED);
}
using SwiftlyS2.Shared;

namespace VIP_Chat;

public static class NativeChatTemplate
{
    public static bool TryBuild(string originalKey, string? language, bool hasPrefix,
        string prefixColor, string nameColor, string textColor, out string template)
    {
        template = "";
        var key = originalKey.TrimStart('#');
        (bool Known, bool Team, bool Dead, bool Spec, bool Loc) info = key switch
        {
            "Cstrike_Chat_All" => (true, false, false, false, false),
            "Cstrike_Chat_AllDead" => (true, false, true, false, false),
            "Cstrike_Chat_AllSpec" => (true, false, false, true, false),
            "Cstrike_Chat_CT" or "Cstrike_Chat_T" => (true, true, false, false, false),
            "Cstrike_Chat_CT_Dead" or "Cstrike_Chat_T_Dead" => (true, true, true, false, false),
            "Cstrike_Chat_CT_Loc" or "Cstrike_Chat_T_Loc" => (true, true, false, false, true),
            "Cstrike_Chat_Spec" => (true, true, false, true, false),
            _ => (false, false, false, false, false)
        };
        if (!info.Known) return false;
        var lang = (language ?? "ru").ToLowerInvariant();
        string Label(string ru, string uk, string en) => lang switch { "uk" or "ua" => uk, "en" => en, _ => ru };
        var labels = "";
        if (info.Dead) labels += Label("*МЁРТВ* ", "*МЕРТВИЙ* ", "*DEAD* ");
        if (info.Spec) labels += Label("[НАБЛЮДАТЕЛЬ] ", "[СПОСТЕРІГАЧ] ", "[SPECTATOR] ");
        if (info.Team) labels += Label("[КОМАНДА] ", "[КОМАНДА] ", "[TEAM] ");
        var pc = ChatFormatting.ColorTag(prefixColor);
        if (pc.Length == 0) pc = "[default]";
        var nc = ChatFormatting.ColorTag(nameColor);
        if (nc.Length == 0) nc = info.Spec ? "[default]" : "[lightpurple]";
        var tc = ChatFormatting.ColorTag(textColor);
        if (tc.Length == 0) tc = "[default]";
        // Colour codes belong to the message format, NOT to substituted parameter strings.
        // Player input is kept exclusively in parameters, so [red] / %s1 in player text cannot
        // become a colour tag / format placeholder. Param3 remains the client's location token.
        var prefix = hasPrefix ? pc + "%s4 " : "";
        var location = info.Loc ? "[default] @ %s3" : "";
        template = ($"[default]{labels}{prefix}{nc}%s1{location}[default]: {tc}%s2").Colored();
        // Helper.Colored adds the leading space required for a directly rendered CS2 chat line.
        return true;
    }
}

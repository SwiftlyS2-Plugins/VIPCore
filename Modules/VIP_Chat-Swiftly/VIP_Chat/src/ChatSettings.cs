using System.Text;
using SwiftlyS2.Shared;
using System.Text.RegularExpressions;

namespace VIP_Chat;

public sealed class ChatGroupSettings
{
    public string Prefix { get; set; } = "list";
    public string NameColor { get; set; } = "list";
    public string TextColor { get; set; } = "list";
    public string PrefixColor { get; set; } = "list";
}

public sealed class ChatModuleConfig
{
    public string Language { get; set; } = "ru";
    public bool FreezePlayer { get; set; } = false;
    public int MaxPrefixLength { get; set; } = 32;
    public int InputTimeoutSeconds { get; set; } = 60;
    public Dictionary<string, string> Prefixes { get; set; } = new()
    {
        ["VIP"] = "[VIP]", ["PREMIUM"] = "[PREMIUM]", ["Kyiv Light"] = "[Kyiv Light]"
    };
    public Dictionary<string, string> Colors { get; set; } = new()
    {
        ["Командный"] = "{TEAM}", ["Белый"] = "{DEFAULT}", ["Красный"] = "{RED}",
        ["Тёмно-красный"] = "{DARKRED}", ["Светло-красный"] = "{LIGHTRED}",
        ["Фиолетовый"] = "{PURPLE}", ["Светло-фиолетовый"] = "{LIGHTPURPLE}",
        ["Зелёный"] = "{GREEN}", ["Лаймовый"] = "{LIME}", ["Светло-зелёный"] = "{LIGHTGREEN}",
        ["Серый"] = "{GRAY}", ["Оливковый"] = "{OLIVE}", ["Жёлтый"] = "{LIGHTOLIVE}",
        ["Синий"] = "{BLUE}", ["Светло-синий"] = "{LIGHTBLUE}", ["Голубой"] = "{GRAYBLUE}"
    };
}

public sealed class PlayerChatSettings
{
    // Null = not set yet; empty string = deliberately disabled. Keep that distinction on reconnect.
    public string? Prefix { get; set; }
    public string? NameColor { get; set; }
    public string? TextColor { get; set; }
    public string? PrefixColor { get; set; }
    public PlayerChatSettings Copy() => (PlayerChatSettings)MemberwiseClone();
}

public static class ChatFormatting
{
    private static readonly Dictionary<string, string> ColorTags = new(StringComparer.OrdinalIgnoreCase)
    {
        ["TEAM"] = "[lightpurple]", ["DEFAULT"] = "[default]", ["WHITE"] = "[white]",
        ["DARKRED"] = "[darkred]", ["LIGHTPURPLE"] = "[lightpurple]", ["GREEN"] = "[green]",
        ["LIGHTGREEN"] = "[olive]", ["OLIVE"] = "[olive]", ["LIME"] = "[lime]", ["RED"] = "[red]",
        ["GRAY"] = "[gray]", ["GREY"] = "[grey]", ["LIGHTOLIVE"] = "[yellow]",
        ["YELLOW"] = "[yellow]", ["LIGHTYELLOW"] = "[lightyellow]", ["GRAYBLUE"] = "[bluegrey]",
        ["BLUEGREY"] = "[bluegrey]", ["SILVER"] = "[silver]", ["LIGHTBLUE"] = "[lightblue]",
        ["BLUE"] = "[blue]", ["DARKBLUE"] = "[darkblue]", ["PURPLE"] = "[purple]",
        ["MAGENTA"] = "[magenta]", ["LIGHTRED"] = "[lightred]", ["ORANGE"] = "[orange]",
        ["GOLD"] = "[gold]", ["/"] = "[/]"
    };
    private static readonly Regex Tags = new(@"\{[^}]*\}|\[[^\]]*\]", RegexOptions.Compiled);
    public static bool IsList(string? value) => string.Equals(value, "list", StringComparison.OrdinalIgnoreCase);
    public static bool IsCustom(string? value) => string.Equals(value, "custom", StringComparison.OrdinalIgnoreCase);
    public static string ColorTag(string? token)
    {
        var key = (token ?? "").Trim().Trim('{', '}', '[', ']');
        return ColorTags.GetValueOrDefault(key, "");
    }
    public static string ColorCode(string? token)
    {
        var tag = ColorTag(token);
        return tag.Length == 0 ? "" : tag.Colored().TrimStart(' ');
    }
    public static string CleanPrefix(string? text, int maxLength)
    {
        // No control characters, injected colour tokens, HTML or command quoting in custom prefixes.
        var clean = Tags.Replace(text ?? "", m => ColorCode(m.Value).Length > 0 ? "" : m.Value);
        var result = new StringBuilder();
        var count = 0;
        foreach (var rune in clean.EnumerateRunes())
        {
            if (Rune.IsControl(rune) || rune.Value is '<' or '>' or '"' or '\\' or '{' or '}') continue;
            if (count++ >= maxLength) break;
            result.Append(rune);
        }
        return result.ToString().Trim();
    }
    public static string ResolvePrefix(string policy, string? saved, ChatModuleConfig config)
    {
        if (string.IsNullOrEmpty(policy)) return "";
        if (IsList(policy))
            return saved != null && config.Prefixes.Values.Contains(saved, StringComparer.Ordinal)
                ? CleanPrefix(saved, config.MaxPrefixLength) : "";
        if (IsCustom(policy)) return CleanPrefix(saved, config.MaxPrefixLength);
        return saved == "" ? "" : CleanPrefix(policy, config.MaxPrefixLength);
    }
    public static string ResolveColor(string policy, string? saved, ChatModuleConfig config)
    {
        if (string.IsNullOrEmpty(policy)) return "";
        if (IsList(policy))
            return saved != null && ColorCode(saved).Length > 0
                && config.Colors.Values.Any(x => ColorCode(x) == ColorCode(saved)) ? saved : "";
        return saved == "" || ColorCode(policy).Length == 0 ? "" : policy;
    }
}

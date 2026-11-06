using SwiftlyS2.Shared.Menu;
using SwiftlyS2.Shared.Players;

namespace VIPCore.Contract;

/// <summary>
/// One group a player holds. Times are Unix seconds: <see cref="ExpiresAt"/> 0 is permanent,
/// <see cref="GrantedAt"/> 0 unknown (granted before VIPCore 1.2.0).
/// </summary>
public sealed record VipGrant(string Group, bool Global, long ExpiresAt, long GrantedAt)
{
    public bool Permanent => ExpiresAt == 0;
}

/// <summary>A configured group, and how many registered features its config grants.</summary>
public sealed record VipGroupInfo(string Name, int Weight, int FeatureCount);

/// <summary>
/// A feature as a player's menu shows it. <see cref="Value"/> is a Selectable feature's current choice;
/// <see cref="Locked"/> when it cannot be switched right now.
/// </summary>
public sealed record VipFeatureView(string Key, FeatureType Type, string Name, string? Description, string? Value, FeatureState State, bool Locked);

/// <summary>
/// One choice a feature offers: a FOV, a chat tag, a gun. <see cref="Group"/> sorts choices into sections;
/// <see cref="Color"/> is a chat colour name ("gold") or #rrggbb.
/// </summary>
public sealed record VipFeatureOption(string Id, string Label, string? Group = null, string? Description = null, string? Color = null, bool IsDefault = false);

/// <summary>
/// A module's own page in the VIP menu. <see cref="Build"/> runs for the player opening it, whose hub becomes the page's parent.
/// </summary>
public sealed record VipMenuPage(string Id, int Order, Func<IPlayer, string> Label, Func<IPlayer, bool> IsAvailable, Func<IPlayer, IMenu> Build);

/// <summary>A VIP as the admin pages list them; <see cref="LastVisit"/> is Unix seconds.</summary>
public sealed record VipUserInfo(ulong SteamId, string Name, long LastVisit, bool Online, IReadOnlyList<VipGrant> Grants);

/// <summary>A per-round feature's uses left, and when it stops taking them (null: while the round lasts).</summary>
public sealed record VipFeatureLimit(int Left, int PerRound, DateTimeOffset? Until);

/// <summary>Counts the admin pages carry as their menu's Tag.</summary>
public sealed record VipUserStats(int Users, int EndingThisWeek);

/// <summary>The ids of VIPCore's own menus.</summary>
public static class VipMenuIds
{
    public const string Hub = "vipcore.hub";
    public const string Perks = "vipcore.perks";
    public const string Customize = "vipcore.customize";
    public const string Users = "vipcore.users";
    public const string UsersList = "vipcore.users.list";
    public const string Grant = "vipcore.grant";
}

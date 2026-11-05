using System.Collections.Concurrent;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Players;
using VIPCore.Contract;
using VIPCore.Models;

namespace VIPCore.Services;

public class Feature
{
    public required string Key { get; init; }
    public FeatureType FeatureType { get; set; }
    public Action<IPlayer, FeatureState>? OnSelectItem { get; set; }
    public Func<IPlayer, string>? DisplayNameResolver { get; set; }
    public Func<IPlayer, string?>? DescriptionResolver { get; set; }
    public FeatureOptions? Options { get; set; }
}

/// <summary>A feature's choices as registered through RegisterFeatureOptions.</summary>
public sealed record FeatureOptions(Func<IPlayer, IReadOnlyList<VipFeatureOption>> List, Func<IPlayer, string?> Current, Action<IPlayer, string> Choose);

public class PlayerCookie
{
    public ulong SteamId64 { get; set; }
    public Dictionary<string, object> Features { get; set; } = new();
}

public class VipUser : User
{
    public ConcurrentDictionary<string, FeatureState> FeatureStates { get; set; } = new();
    public List<string> OwnedGroups { get; set; } = new();
    /// <summary>The player's valid grants, the active one first, then by group weight. Replaced, never mutated.</summary>
    public List<User> Grants { get; set; } = new();
    /// <summary>True when this entry was created in-memory only (no DB record). Used by OverrideClientVipGroup for non-VIP players.</summary>
    public bool IsTemporary { get; set; } = false;
}

internal static class GrantExtensions
{
    public static VipGrant ToGrant(this User user) => new(user.group, user.sid == 0, user.expires, user.granted);
}

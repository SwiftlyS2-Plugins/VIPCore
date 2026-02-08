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
}

public class PlayerCookie
{
    public ulong SteamId64 { get; set; }
    public Dictionary<string, object> Features { get; set; } = new();
}

public class VipUser : User
{
    public ConcurrentDictionary<string, FeatureState> FeatureStates { get; set; } = new();
}

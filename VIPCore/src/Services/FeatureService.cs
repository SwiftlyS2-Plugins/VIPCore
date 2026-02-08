using System.Collections.Concurrent;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Players;
using VIPCore.Contract;
using VIPCore.Models;
using VIPCore.Database.Repositories;
using VIPCore.Config;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace VIPCore.Services;

public class FeatureService(ISwiftlyCore core, IOptionsMonitor<VipConfig> configMonitor)
{
    private VipConfig config => configMonitor.CurrentValue;
    private readonly ConcurrentDictionary<string, Feature> _features = new();
    private readonly HashSet<string> _forcedDisabledFeatures = new();

    public void RegisterFeature(string key, FeatureType type, Action<IPlayer, FeatureState>? onSelect = null, Func<IPlayer, string>? displayNameResolver = null)
    {
        var feature = new Feature
        {
            Key = key,
            FeatureType = type,
            OnSelectItem = onSelect,
            DisplayNameResolver = displayNameResolver
        };
        _features[key] = feature;
        if (config.VipLogging)
            core.Logger.LogDebug("[VIPCore] Registered feature: {Key} ({Type})", key, type);
    }

    public void UnregisterFeature(string key)
    {
        _features.TryRemove(key, out _);
    }

    public IEnumerable<Feature> GetRegisteredFeatures() => _features.Values;

    public Feature? GetFeature(string key)
    {
        _features.TryGetValue(key, out var feature);
        return feature;
    }

    public void DisableAllFeatures()
    {
        foreach (var key in _features.Keys) _forcedDisabledFeatures.Add(key);
    }

    public void EnableAllFeatures() => _forcedDisabledFeatures.Clear();

    public bool IsFeatureForcedDisabled(string key) => _forcedDisabledFeatures.Contains(key);
}

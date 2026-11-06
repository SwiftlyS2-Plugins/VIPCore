using System.Globalization;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Players;
using VIPCore.Config;
using VIPCore.Contract;

using Microsoft.Extensions.Logging;

namespace VIPCore.Services;

/// <summary>Features as a player's menus show them. Every module callback is guarded.</summary>
public class FeatureViewService(ISwiftlyCore core, FeatureService featureService, VipService vipService, CookieService cookieService, GroupsConfig groupsConfig)
{
    public VipFeatureView? GetView(IPlayer player, string featureKey)
    {
        var feature = featureService.GetFeature(featureKey);
        return feature == null || feature.FeatureType == FeatureType.Hide ? null : View(player, feature);
    }

    public VipFeatureView View(IPlayer player, Feature feature)
    {
        var state = State(player, feature.Key);
        var locked = state == FeatureState.NoAccess || featureService.IsFeatureForcedDisabled(feature.Key);
        return new VipFeatureView(feature.Key, feature.FeatureType, Name(player, feature), Description(player, feature), Value(player, feature), state, locked);
    }

    public FeatureState State(IPlayer player, string featureKey) =>
        vipService.GetVipUser(player.SteamID) is { } user && user.FeatureStates.TryGetValue(featureKey, out var state)
            ? state
            : FeatureState.NoAccess;

    public string Name(IPlayer player, Feature feature)
    {
        if (feature.DisplayNameResolver is not { } resolve) return feature.Key;
        var name = Guard<string?>(feature.Key, "name", () => resolve(player), null);
        return string.IsNullOrEmpty(name) ? feature.Key : name;
    }

    public string? Description(IPlayer player, Feature feature) =>
        feature.DescriptionResolver is { } resolve ? Guard(feature.Key, "description", () => resolve(player), null) : null;

    public IReadOnlyList<VipFeatureOption> Options(IPlayer player, Feature feature) =>
        feature.Options is { } options
            ? Guard<IReadOnlyList<VipFeatureOption>?>(feature.Key, "options", () => options.List(player), null) ?? []
            : [];

    /// <summary>The choice in use: the current one, or the default while there is none.</summary>
    public VipFeatureOption? Selected(IPlayer player, Feature feature, IReadOnlyList<VipFeatureOption> options)
    {
        if (feature.Options is not { } registered) return null;

        var current = Guard(feature.Key, "current", () => registered.Current(player), null);
        return current == null
            ? options.FirstOrDefault(option => option.IsDefault)
            : options.FirstOrDefault(option => string.Equals(option.Id, current, StringComparison.Ordinal));
    }

    public bool Choose(IPlayer player, Feature feature, string optionId)
    {
        if (feature.Options is not { } registered) return false;

        try
        {
            registered.Choose(player, optionId);
            return true;
        }
        catch (Exception ex)
        {
            core.Logger.LogWarning(ex, "[VIPCore] Feature '{Key}' failed to apply option '{Option}'.", feature.Key, optionId);
            return false;
        }
    }

    /// <summary>The registered, shown features the player's group grants, in the order the group lists them.</summary>
    public IReadOnlyList<Feature> PlayerFeatures(IPlayer player)
    {
        var user = vipService.GetVipUser(player.SteamID);
        if (user == null || groupsConfig.Find(user.group) is not { } found) return [];

        var order = found.Group.FeatureOrder;
        return featureService.GetRegisteredFeatures()
            .Where(feature => feature.FeatureType != FeatureType.Hide && found.Group.HasFeature(feature.Key))
            .OrderBy(feature => Position(order, feature.Key))
            .ThenBy(feature => feature.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public IReadOnlyList<VipGroupInfo> GroupInfos()
    {
        var shown = featureService.GetRegisteredFeatures()
            .Where(feature => feature.FeatureType != FeatureType.Hide)
            .Select(feature => feature.Key)
            .ToList();

        return groupsConfig.Groups
            .Select(pair => new VipGroupInfo(pair.Key, pair.Value.Weight, shown.Count(pair.Value.HasFeature)))
            .OrderBy(info => info.Weight)
            .ThenBy(info => info.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public IReadOnlyList<VipGrant> Grants(IPlayer player) =>
        vipService.GetVipUser(player.SteamID)?.Grants.Select(grant => grant.ToGrant()).ToList() ?? [];

    private string? Value(IPlayer player, Feature feature)
    {
        if (feature.Options != null)
            return Selected(player, feature, Options(player, feature))?.Label;

        if (feature.FeatureType != FeatureType.Selectable) return null;

        // Selectable modules without options keep their choice in the "<key>.value" cookie.
        var value = cookieService.GetCookie<int>(player.SteamID, feature.Key + ".value");
        return value > 0 ? value.ToString(CultureInfo.InvariantCulture) : null;
    }

    private static int Position(List<string> order, string featureKey)
    {
        var index = order.FindIndex(key => key.Equals(featureKey, StringComparison.OrdinalIgnoreCase));
        return index < 0 ? int.MaxValue : index;
    }

    private T Guard<T>(string featureKey, string resolver, Func<T> resolve, T fallback)
    {
        try
        {
            return resolve();
        }
        catch (Exception ex)
        {
            core.Logger.LogDebug(ex, "[VIPCore] The {Resolver} resolver of feature '{Key}' threw.", resolver, featureKey);
            return fallback;
        }
    }
}

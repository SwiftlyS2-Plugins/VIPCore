using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Players;
using VIPCore.Contract;
using VIPCore.Models;
using VIPCore.Database.Repositories;
using VIPCore.Config;

using System.Collections.Concurrent;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace VIPCore.Services;

public class VipService(
    ISwiftlyCore core, 
    IUserRepository userRepository, 
    FeatureService featureService,
    CookieService cookieService,
    IOptionsMonitor<VipConfig> coreConfigMonitor,
    GroupsConfig groupsConfig,
    ServerIdentifier serverIdentifier)
{
    private VipConfig coreConfig => coreConfigMonitor.CurrentValue;
    private readonly ConcurrentDictionary<ulong, VipUser> _users = new();

    public bool IsClientVip(IPlayer player)
    {
        if (player.IsFakeClient) return false;
        return _users.TryGetValue(player.SteamID, out var user) && (user.expires == DateTime.MinValue || user.expires > DateTime.UtcNow);
    }

    public VipUser? GetVipUser(ulong steamId)
    {
        _users.TryGetValue(steamId, out var user);
        return user;
    }

    public async Task LoadPlayer(IPlayer player)
    {
        if (player.IsFakeClient) return;

        var allGroups = (await userRepository.GetUserGroupsAsync(player.SteamID, serverIdentifier.ServerId)).ToList();

        if (allGroups.Count == 0) return;

        var now = DateTime.UtcNow;
        var expiredGroups = allGroups.Where(u => u.expires != DateTime.MinValue && u.expires < now).ToList();
        var validGroups = allGroups.Where(u => u.expires == DateTime.MinValue || u.expires >= now).ToList();

        foreach (var expired in expiredGroups)
        {
            await userRepository.DeleteUserGroupAsync(expired.steam_id, expired.sid, expired.group);
            core.Logger.LogInformation("[VIPCore] VIP group '{Group}' expired for player {Name} ({SteamId})", expired.group, player.Controller.PlayerName, player.SteamID);
        }

        if (validGroups.Count == 0) return;

        var activeUser = ResolveHighestWeightGroup(validGroups);
        if (activeUser == null) return;

        var vipUser = new VipUser
        {
            steam_id = activeUser.steam_id,
            name = activeUser.name,
            last_visit = now,
            sid = activeUser.sid,
            group = activeUser.group,
            expires = activeUser.expires,
            OwnedGroups = validGroups.Select(u => u.group).ToList()
        };

        InitializeFeaturesForUser(vipUser);
        _users[player.SteamID] = vipUser;

        foreach (var g in validGroups)
        {
            g.last_visit = now;
            g.name = player.Controller.PlayerName;
            await userRepository.UpdateUserAsync(g);
        }

        if (coreConfig.VipLogging)
            core.Logger.LogDebug("[VIPCore] Loaded VIP player {Name} ({SteamId}) with active group {Group} (owns: {OwnedGroups})", 
                player.Controller.PlayerName, player.SteamID, activeUser.group, string.Join(", ", vipUser.OwnedGroups));
    }

    private User? ResolveHighestWeightGroup(List<User> validGroups)
    {
        User? best = null;
        int bestWeight = int.MinValue;

        foreach (var user in validGroups)
        {
            var groupName = groupsConfig.Groups.Keys.FirstOrDefault(k => k.Equals(user.group, StringComparison.OrdinalIgnoreCase));
            if (groupName == null || !groupsConfig.Groups.TryGetValue(groupName, out var groupConfig))
                continue;

            if (groupConfig.Weight > bestWeight)
            {
                bestWeight = groupConfig.Weight;
                best = user;
            }
        }

        return best ?? validGroups.FirstOrDefault();
    }

    public void UnloadPlayer(IPlayer player)
    {
        if (_users.TryRemove(player.SteamID, out var user))
        {
            foreach (var state in user.FeatureStates)
            {
                var feature = featureService.GetFeature(state.Key);
                if (feature?.FeatureType == FeatureType.Toggle)
                {
                    cookieService.SetCookie(player.SteamID, state.Key, (int)state.Value);
                }
            }
        }
    }

    private void InitializeFeaturesForUser(VipUser user)
    {
        var groupName = groupsConfig.Groups.Keys.FirstOrDefault(k => k.Equals(user.group, StringComparison.OrdinalIgnoreCase));
        if (groupName == null || !groupsConfig.Groups.TryGetValue(groupName, out var group))
        {
            core.Logger.LogWarning("[VIPCore] Group '{Group}' not found in config for user {SteamId}", user.group, user.steam_id);
            return;
        }

        foreach (var feature in featureService.GetRegisteredFeatures())
        {
            if (!group.Values.ContainsKey(feature.Key))
            {
                user.FeatureStates[feature.Key] = FeatureState.NoAccess;
                continue;
            }

            var cookieVal = cookieService.GetCookie<int?>(user.steam_id, feature.Key);
            user.FeatureStates[feature.Key] = cookieVal.HasValue ? (FeatureState)cookieVal.Value : FeatureState.Enabled;
        }
    }

    public async Task AddVip(ulong steamId, string name, string group, int time)
    {
        var expires = CalculateExpires(time);
        var user = new User
        {
            steam_id = steamId,
            name = name,
            last_visit = DateTime.UtcNow,
            sid = serverIdentifier.ServerId,
            group = group,
            expires = expires
        };

        await userRepository.AddUserAsync(user);
    }

    public async Task RemoveVip(ulong steamId)
    {
        await userRepository.DeleteUserAsync(steamId, serverIdentifier.ServerId);
        _users.TryRemove(steamId, out _);
    }

    public async Task RemoveVipGroup(ulong steamId, string group)
    {
        await userRepository.DeleteUserGroupAsync(steamId, serverIdentifier.ServerId, group);
    }

    public void InitializeFeatureForLoadedPlayers(string featureKey)
    {
        foreach (var kvp in _users)
        {
            var user = kvp.Value;
            
            if (user.FeatureStates.ContainsKey(featureKey))
                continue;

            var groupName = groupsConfig.Groups.Keys.FirstOrDefault(k => k.Equals(user.group, StringComparison.OrdinalIgnoreCase));
            if (groupName == null || !groupsConfig.Groups.TryGetValue(groupName, out var group))
                continue;

            if (!group.Values.ContainsKey(featureKey))
            {
                user.FeatureStates[featureKey] = FeatureState.NoAccess;
                continue;
            }

            var cookieVal = cookieService.GetCookie<int?>(user.steam_id, featureKey);
            user.FeatureStates[featureKey] = cookieVal.HasValue ? (FeatureState)cookieVal.Value : FeatureState.Enabled;

            if (coreConfig.VipLogging)
                core.Logger.LogDebug("[VIPCore] Initialized late-registered feature '{Feature}' for loaded player {SteamId} (Group: {Group}, State: {State})", 
                featureKey, user.steam_id, user.group, user.FeatureStates[featureKey]);
        }
    }

    private DateTime CalculateExpires(int time)
    {
        if (time <= 0) return DateTime.MinValue;
        var now = DateTime.UtcNow;
        return coreConfig.TimeMode switch
        {
            1 => now.AddMinutes(time),
            2 => now.AddHours(time),
            3 => now.AddDays(time),
            _ => now.AddSeconds(time)
        };
    }
}

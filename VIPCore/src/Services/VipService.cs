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
    private readonly ConcurrentDictionary<long, VipUser> _users = new();

    public bool IsClientVip(IPlayer player)
    {
        if (player.IsFakeClient) return false;
        return _users.TryGetValue(player.SteamID, out var user) && (user.expires == 0 || user.expires > DateTimeOffset.UtcNow.ToUnixTimeSeconds());
    }

    public VipUser? GetVipUser(long accountId)
    {
        _users.TryGetValue(accountId, out var user);
        return user;
    }

    public async Task LoadPlayer(IPlayer player)
    {
        if (player.IsFakeClient) return;

        var allGroups = (await userRepository.GetUserGroupsAsync(player.SteamID, serverIdentifier.ServerId)).ToList();

        if (allGroups.Count == 0) return;

        var nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var expiredGroups = allGroups.Where(u => u.expires != 0 && u.expires < nowUnix).ToList();
        var validGroups = allGroups.Where(u => u.expires == 0 || u.expires >= nowUnix).ToList();

        foreach (var expired in expiredGroups)
        {
            await userRepository.DeleteUserGroupAsync(expired.account_id, expired.sid, expired.group);
            core.Logger.LogInformation("[VIPCore] VIP group '{Group}' expired for player {Name} ({SteamId})", expired.group, player.Controller.PlayerName, player.SteamID);
        }

        if (validGroups.Count == 0) return;

        var activeUser = ResolveHighestWeightGroup(validGroups);
        if (activeUser == null) return;

        var vipUser = new VipUser
        {
            account_id = activeUser.account_id,
            name = activeUser.name,
            lastvisit = nowUnix,
            sid = activeUser.sid,
            group = activeUser.group,
            expires = activeUser.expires,
            OwnedGroups = validGroups.Select(u => u.group).ToList()
        };

        InitializeFeaturesForUser(vipUser);
        _users[player.SteamID] = vipUser;

        foreach (var g in validGroups)
        {
            g.lastvisit = nowUnix;
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
            core.Logger.LogWarning("[VIPCore] Group '{Group}' not found in config for user {SteamId}", user.group, user.account_id);
            return;
        }

        foreach (var feature in featureService.GetRegisteredFeatures())
        {
            if (!group.Values.ContainsKey(feature.Key))
            {
                user.FeatureStates[feature.Key] = FeatureState.NoAccess;
                continue;
            }

            var cookieVal = cookieService.GetCookie<int?>(user.account_id, feature.Key);
            user.FeatureStates[feature.Key] = cookieVal.HasValue ? (FeatureState)cookieVal.Value : FeatureState.Enabled;
        }
    }

    public async Task AddVip(long accountId, string name, string group, int time)
    {
        var expires = CalculateExpires(time);
        var user = new User
        {
            account_id = accountId,
            name = name,
            lastvisit = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            sid = serverIdentifier.ServerId,
            group = group,
            expires = expires
        };

        await userRepository.AddUserAsync(user);
    }

    public async Task RemoveVip(long accountId)
    {
        await userRepository.DeleteUserAsync(accountId, serverIdentifier.ServerId);
        _users.TryRemove(accountId, out _);
    }

    public async Task RemoveVipGroup(long accountId, string group)
    {
        await userRepository.DeleteUserGroupAsync(accountId, serverIdentifier.ServerId, group);
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

            var cookieVal = cookieService.GetCookie<int?>(user.account_id, featureKey);
            user.FeatureStates[featureKey] = cookieVal.HasValue ? (FeatureState)cookieVal.Value : FeatureState.Enabled;

            if (coreConfig.VipLogging)
                core.Logger.LogDebug("[VIPCore] Initialized late-registered feature '{Feature}' for loaded player {SteamId} (Group: {Group}, State: {State})", 
                featureKey, user.account_id, user.group, user.FeatureStates[featureKey]);
        }
    }

    private long CalculateExpires(int time)
    {
        if (time <= 0) return 0;
        var now = DateTimeOffset.UtcNow;
        var expiresDateTime = coreConfig.TimeMode switch
        {
            1 => now.AddMinutes(time),
            2 => now.AddHours(time),
            3 => now.AddDays(time),
            _ => now.AddSeconds(time)
        };
        return expiresDateTime.ToUnixTimeSeconds();
    }
}

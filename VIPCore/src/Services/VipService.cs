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
    GroupsConfig groupsConfig)
{
    private VipConfig coreConfig => coreConfigMonitor.CurrentValue;
    private readonly ConcurrentDictionary<ulong, VipUser> _users = new();

    public bool IsClientVip(IPlayer player)
    {
        if (player.IsFakeClient) return false;
        return _users.TryGetValue(player.SteamID, out var user) && (user.expires == 0 || user.expires > DateTimeOffset.UtcNow.ToUnixTimeSeconds());
    }

    public VipUser? GetVipUser(ulong steamId)
    {
        _users.TryGetValue(steamId, out var user);
        return user;
    }

    public async Task LoadPlayer(IPlayer player)
    {
        if (player.IsFakeClient) return;

        var user = await userRepository.GetUserAsync((long)player.SteamID, coreConfig.ServerId);
        if (user != null)
        {
            if (user.expires != 0 && user.expires < DateTimeOffset.UtcNow.ToUnixTimeSeconds())
            {
                await userRepository.DeleteUserAsync((long)player.SteamID, coreConfig.ServerId);
                core.Logger.LogInformation("[VIPCore] VIP expired for player {Name} ({SteamId})", player.Controller.PlayerName, player.SteamID);
                return;
            }

            var vipUser = new VipUser
            {
                account_id = user.account_id,
                name = user.name,
                lastvisit = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                sid = user.sid,
                group = user.group,
                expires = user.expires
            };

            InitializeFeaturesForUser(vipUser);
            _users[player.SteamID] = vipUser;
            
            await userRepository.UpdateUserAsync(vipUser);
            
            if (coreConfig.VipLogging)
                core.Logger.LogDebug("[VIPCore] Loaded VIP player {Name} ({SteamId}) with group {Group}", player.Controller.PlayerName, player.SteamID, user.group);
        }
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
            core.Logger.LogWarning("[VIPCore] Group '{Group}' not found in config for user {AccountId}", user.group, user.account_id);
            return;
        }

        foreach (var feature in featureService.GetRegisteredFeatures())
        {
            if (!group.Values.ContainsKey(feature.Key))
            {
                user.FeatureStates[feature.Key] = FeatureState.NoAccess;
                continue;
            }

            var cookieVal = cookieService.GetCookie<int?>((ulong)user.account_id, feature.Key);
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
            sid = coreConfig.ServerId,
            group = group,
            expires = expires
        };

        await userRepository.AddUserAsync(user);
    }

    public async Task RemoveVip(long accountId)
    {
        await userRepository.DeleteUserAsync(accountId, coreConfig.ServerId);
        _users.TryRemove((ulong)accountId, out _);
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

            var cookieVal = cookieService.GetCookie<int?>((ulong)user.account_id, featureKey);
            user.FeatureStates[featureKey] = cookieVal.HasValue ? (FeatureState)cookieVal.Value : FeatureState.Enabled;
            
            if (coreConfig.VipLogging)
                core.Logger.LogDebug("[VIPCore] Initialized late-registered feature '{Feature}' for loaded player {AccountId} (Group: {Group}, State: {State})", 
                featureKey, user.account_id, user.group, user.FeatureStates[featureKey]);
        }
    }

    private long CalculateExpires(int time)
    {
        if (time <= 0) return 0;
        var now = DateTimeOffset.UtcNow;
        return coreConfig.TimeMode switch
        {
            1 => now.AddMinutes(time).ToUnixTimeSeconds(),
            2 => now.AddHours(time).ToUnixTimeSeconds(),
            3 => now.AddDays(time).ToUnixTimeSeconds(),
            _ => now.AddSeconds(time).ToUnixTimeSeconds()
        };
    }
}

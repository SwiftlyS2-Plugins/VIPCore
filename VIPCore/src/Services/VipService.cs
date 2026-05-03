using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Players;
using VIPCore.Contract;
using VIPCore.Models;
using VIPCore.Database.Repositories;
using VIPCore.Config;

using System.Collections.Concurrent;

using Microsoft.Extensions.Configuration;
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

    private const long SteamId64Base = 76561197960265728L;

    /// <summary>Converts SteamID64 to AccountID. Returns AccountID unchanged.</summary>
    private static long NormalizeToAccountId(long id) =>
        id >= SteamId64Base ? id - SteamId64Base : id;

    /// <summary>Converts AccountID to SteamID64. Returns SteamID64 unchanged.</summary>
    private static long ToSteamId64(long id) =>
        id < SteamId64Base ? id + SteamId64Base : id;

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
        await LoadPlayerWithExpiredInfo(player);
    }

    public async Task<string?> LoadPlayerWithExpiredInfo(IPlayer player)
    {
        if (player.IsFakeClient) return null;

        var steamId64 = (long)player.SteamID;
        var accountId = NormalizeToAccountId(steamId64);

        var allGroups = (await userRepository.GetUserGroupsAsync(steamId64, serverIdentifier.EffectiveServerId)).ToList();

        // Also try the AccountID form in case VIP was added with the short Steam AccountID
        if (allGroups.Count == 0 && accountId != steamId64)
            allGroups = (await userRepository.GetUserGroupsAsync(accountId, serverIdentifier.EffectiveServerId)).ToList();

        if (allGroups.Count == 0) return null;

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var expiredGroups = allGroups.Where(u => u.expires != 0 && u.expires < now).ToList();
        var validGroups = allGroups.Where(u => u.expires == 0 || u.expires >= now).ToList();

        foreach (var expired in expiredGroups)
        {
            await userRepository.DeleteUserGroupAsync(expired.account_id, expired.sid, expired.group);
            core.Logger.LogInformation("[VIPCore] VIP group '{Group}' expired for player {Name} ({SteamId})", expired.group, player.Controller.PlayerName, player.SteamID);
        }

        if (validGroups.Count == 0)
        {
            // Don't evict a temporary in-memory entry created by OverrideVipGroup
            if (!(_users.TryGetValue(player.SteamID, out var current) && current.IsTemporary))
                _users.TryRemove(player.SteamID, out _);

            if (expiredGroups.Count == 0) return null;

            var expiredBest = ResolveHighestWeightGroup(expiredGroups);
            return expiredBest?.group;
        }

        var activeUser = ResolveHighestWeightGroup(validGroups);
        if (activeUser == null) return null;

        var vipUser = new VipUser
        {
            account_id = activeUser.account_id,
            name = activeUser.name,
            lastvisit = now,
            sid = activeUser.sid,
            group = activeUser.group,
            expires = activeUser.expires,
            OwnedGroups = validGroups.Select(u => u.group).ToList()
        };

        InitializeFeaturesForUser(vipUser);
        _users[player.SteamID] = vipUser;

        foreach (var g in validGroups)
        {
            g.lastvisit = now;
            g.name = player.Controller.PlayerName;
            await userRepository.UpdateUserAsync(g);
        }

        if (coreConfig.VipLogging)
            core.Logger.LogDebug("[VIPCore] Loaded VIP player {Name} ({SteamId}) with active group {Group} (owns: {OwnedGroups})",
                player.Controller.PlayerName, player.SteamID, activeUser.group, string.Join(", ", vipUser.OwnedGroups));

        return null;
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

    public void OverrideVipGroup(IPlayer player, string group)
    {
        var logging = coreConfig.VipLogging;

        if (!_users.TryGetValue(player.SteamID, out var user))
        {
            // Player has no VIP — create a temporary in-memory entry, no DB write
            var tempUser = new VipUser
            {
                account_id = NormalizeToAccountId((long)player.SteamID),
                name = player.Controller?.PlayerName ?? "unknown",
                lastvisit = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                sid = serverIdentifier.ServerId,
                group = group,
                expires = 0,
                IsTemporary = true
            };
            InitializeFeaturesForUser(tempUser);
            _users[player.SteamID] = tempUser;

            if (logging)
                core.Logger.LogInformation("[VIPCore] OverrideVipGroup: {SteamId} had no VIP, created temporary in-memory entry with group '{Group}'", player.SteamID, group);
            return;
        }

        var oldGroup = user.group;
        user.group = group;
        user.FeatureStates.Clear();
        InitializeFeaturesForUser(user);

        if (logging)
            core.Logger.LogInformation("[VIPCore] OverrideVipGroup: {SteamId} '{OldGroup}' -> '{NewGroup}', features count={Count}", player.SteamID, oldGroup, user.group, user.FeatureStates.Count);
    }

    public bool RemoveTempUser(IPlayer player)
    {
        if (_users.TryGetValue(player.SteamID, out var user) && user.IsTemporary)
        {
            _users.TryRemove(player.SteamID, out _);
            return true;
        }
        return false;
    }

    public void UnloadPlayer(IPlayer player)
    {
        if (_users.TryRemove(player.SteamID, out var user))
        {
            foreach (var state in user.FeatureStates)
            {
                var feature = featureService.GetFeature(state.Key);
                if (feature?.FeatureType == FeatureType.Toggle && state.Value != FeatureState.NoAccess)
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
            var hasFeature = group.ValuesSection != null
                ? group.ValuesSection.GetChildren().Any(c => c.Key.Equals(feature.Key, StringComparison.OrdinalIgnoreCase))
                : group.Values.ContainsKey(feature.Key);

            if (!hasFeature)
            {
                user.FeatureStates[feature.Key] = FeatureState.NoAccess;
                continue;
            }

            var cookieVal = cookieService.GetCookie<int?>((ulong)user.account_id, feature.Key);
            var restoredState = cookieVal.HasValue ? (FeatureState)cookieVal.Value : FeatureState.Enabled;
            if (restoredState == FeatureState.NoAccess) restoredState = FeatureState.Enabled;
            user.FeatureStates[feature.Key] = restoredState;
        }
    }

    public async Task AddVip(long accountId, string name, string group, int time)
    {
        accountId = NormalizeToAccountId(accountId);
        var existingUser = await userRepository.GetUserAsync(accountId, serverIdentifier.EffectiveServerId);
        long expires;

        if (time <= 0)
        {
            expires = 0; // Permanent
        }
        else if (existingUser != null && existingUser.group.Equals(group, StringComparison.OrdinalIgnoreCase) && existingUser.expires > 0)
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var remaining = existingUser.expires > now ? existingUser.expires - now : 0;
            
            // CalculateExpires(time) returns the absolute time in the future. 
            // We need to add the remaining seconds to that.
            expires = CalculateExpires(time) + remaining;
        }
        else
        {
            expires = CalculateExpires(time);
        }

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
        var normalized = NormalizeToAccountId(accountId);
        var steamId64 = ToSteamId64(accountId);

        await userRepository.DeleteUserAsync(normalized, serverIdentifier.EffectiveServerId);
        if (steamId64 != normalized)
            await userRepository.DeleteUserAsync(steamId64, serverIdentifier.EffectiveServerId);

        _users.TryRemove((ulong)steamId64, out _);
        _users.TryRemove((ulong)normalized, out _);
    }

    public async Task RemoveVipGroup(long accountId, string group)
    {
        var normalized = NormalizeToAccountId(accountId);
        var steamId64 = ToSteamId64(accountId);

        await userRepository.DeleteUserGroupAsync(normalized, serverIdentifier.EffectiveServerId, group);
        if (steamId64 != normalized)
            await userRepository.DeleteUserGroupAsync(steamId64, serverIdentifier.EffectiveServerId, group);
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

            var hasFeatureKey = group.ValuesSection != null
                ? group.ValuesSection.GetChildren().Any(c => c.Key.Equals(featureKey, StringComparison.OrdinalIgnoreCase))
                : group.Values.ContainsKey(featureKey);

            if (!hasFeatureKey)
            {
                user.FeatureStates[featureKey] = FeatureState.NoAccess;
                continue;
            }

            var cookieVal = cookieService.GetCookie<int?>((ulong)user.account_id, featureKey);
            var restoredState = cookieVal.HasValue ? (FeatureState)cookieVal.Value : FeatureState.Enabled;
            if (restoredState == FeatureState.NoAccess) restoredState = FeatureState.Enabled;
            user.FeatureStates[featureKey] = restoredState;

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
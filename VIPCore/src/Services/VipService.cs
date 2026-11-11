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
    internal static long ToSteamId64(long id) =>
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

    /// <summary>Reloads a player whose grants just changed, if they are still connected. Logs instead of throwing.</summary>
    public async Task ReloadPlayer(IPlayer? player)
    {
        if (player is not { IsValid: true }) return;

        try
        {
            await LoadPlayer(player);
        }
        catch (Exception ex)
        {
            core.Logger.LogError(ex, "[VIPCore] Failed to reload VIP player {SteamId}", player.SteamID);
        }
    }

    public async Task<string?> LoadPlayerWithExpiredInfo(IPlayer player, string? playerName = null)
    {
        if (player.IsFakeClient) return null;

        // Capture before any await: the player may be disposed (disconnect) while DB calls run.
        var name = playerName ?? SafeGetName(player);
        var steamId64 = (long)player.SteamID;
        var accountId = NormalizeToAccountId(steamId64);

        var allGroups = (await userRepository.GetUserGroupsAsync(steamId64, serverIdentifier.EffectiveServerId)).ToList();

        // Also try the AccountID form in case VIP was added with the short Steam AccountID
        if (accountId != steamId64)
            allGroups.AddRange(await userRepository.GetUserGroupsAsync(accountId, serverIdentifier.EffectiveServerId));

        if (allGroups.Count == 0)
        {
            if (!(_users.TryGetValue((ulong)steamId64, out var current) && current.IsTemporary))
                _users.TryRemove((ulong)steamId64, out _);
            return null;
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var expiredGroups = allGroups.Where(u => u.expires != 0 && u.expires < now).ToList();
        var validGroups = allGroups.Where(u => u.expires == 0 || u.expires >= now).ToList();

        foreach (var expired in expiredGroups)
        {
            await userRepository.DeleteUserGroupAsync(expired.account_id, expired.sid, expired.group);
            core.Logger.LogInformation("[VIPCore] VIP group '{Group}' expired for player {Name} ({SteamId})", expired.group, name, steamId64);
        }

        if (validGroups.Count == 0)
        {
            // Don't evict a temporary in-memory entry created by OverrideVipGroup
            if (!(_users.TryGetValue((ulong)steamId64, out var current) && current.IsTemporary))
                _users.TryRemove((ulong)steamId64, out _);

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
            granted = activeUser.granted,
            OwnedGroups = validGroups.Select(u => u.group).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            Grants = OrderGrants(validGroups, activeUser)
        };

        InitializeFeaturesForUser(vipUser);
        _users[(ulong)steamId64] = vipUser;

        foreach (var g in validGroups)
        {
            g.lastvisit = now;
            g.name = name;
            await userRepository.UpdateUserAsync(g);
        }

        if (coreConfig.VipLogging)
            core.Logger.LogDebug("[VIPCore] Loaded VIP player {Name} ({SteamId}) with active group {Group} (owns: {OwnedGroups})",
                name, steamId64, activeUser.group, string.Join(", ", vipUser.OwnedGroups));

        return null;
    }

    private static string SafeGetName(IPlayer player)
    {
        try { return player.Controller?.PlayerName ?? "unknown"; }
        catch (ObjectDisposedException) { return "unknown"; }
    }

    private User? ResolveHighestWeightGroup(List<User> validGroups)
    {
        return validGroups
            .OrderByDescending(user => groupsConfig.Groups.FirstOrDefault(g => g.Key.Equals(user.group, StringComparison.OrdinalIgnoreCase)).Value?.Weight ?? int.MinValue)
            .ThenByDescending(user => user.sid == serverIdentifier.ServerId)
            .ThenBy(user => user.group, StringComparer.OrdinalIgnoreCase)
            .ThenBy(user => user.account_id)
            .FirstOrDefault();
    }

    /// <summary>The same priority ResolveHighestWeightGroup picks by, with the active grant forced first.</summary>
    internal List<User> OrderGrants(IEnumerable<User> grants, User? active = null)
    {
        return DistinctGrants(grants)
            .OrderByDescending(user => active != null && SameGrant(user, active))
            .ThenByDescending(user => groupsConfig.WeightOf(user.group))
            .ThenByDescending(user => user.sid == serverIdentifier.ServerId)
            .ThenBy(user => user.group, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    // Legacy rows can hold one grant twice, under the AccountID and the SteamID64 form; keep the longer one.
    private static IEnumerable<User> DistinctGrants(IEnumerable<User> grants) =>
        grants.GroupBy(user => (user.sid, user.group.ToLowerInvariant()))
            .Select(same => same.OrderBy(user => user.expires == 0 ? 0 : 1).ThenByDescending(user => user.expires).First());

    private static bool SameGrant(User a, User b) =>
        a.sid == b.sid && a.group.Equals(b.group, StringComparison.OrdinalIgnoreCase);

    public void OverrideVipGroup(IPlayer player, string group)
    {
        var logging = coreConfig.VipLogging;

        if (!_users.TryGetValue(player.SteamID, out var user))
        {
            // Player has no VIP — create a temporary in-memory entry, no DB write
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var tempUser = new VipUser
            {
                account_id = NormalizeToAccountId((long)player.SteamID),
                name = player.Controller?.PlayerName ?? "unknown",
                lastvisit = now,
                sid = serverIdentifier.ServerId,
                group = group,
                expires = 0,
                granted = now,
                IsTemporary = true
            };
            tempUser.Grants = [SyntheticGrant(tempUser, group)];
            InitializeFeaturesForUser(tempUser);
            _users[player.SteamID] = tempUser;

            if (logging)
                core.Logger.LogInformation("[VIPCore] OverrideVipGroup: {SteamId} had no VIP, created temporary in-memory entry with group '{Group}'", player.SteamID, group);
            return;
        }

        var oldGroup = user.group;
        user.group = group;
        user.Grants = WithActiveGroup(user, group);
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

    // An override makes its group the active one: a grant of it moves first, otherwise a synthetic one leads.
    private static List<User> WithActiveGroup(VipUser user, string group)
    {
        if (user.IsTemporary)
            return [SyntheticGrant(user, group)];

        var held = user.Grants.FirstOrDefault(g => g.group.Equals(group, StringComparison.OrdinalIgnoreCase));
        return held != null
            ? [held, .. user.Grants.Where(g => !ReferenceEquals(g, held))]
            : [SyntheticGrant(user, group), .. user.Grants];
    }

    private static User SyntheticGrant(VipUser user, string group) => new()
    {
        account_id = user.account_id,
        name = user.name,
        lastvisit = user.lastvisit,
        sid = user.sid,
        group = group,
        expires = 0,
        granted = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
    };

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
            var hasFeature = group.HasFeature(feature.Key);

            if (!hasFeature)
            {
                user.FeatureStates[feature.Key] = FeatureState.NoAccess;
                continue;
            }

            var cookieVal = cookieService.GetCookie<int?>((ulong)ToSteamId64(user.account_id), feature.Key);
            var restoredState = cookieVal.HasValue && Enum.IsDefined(typeof(FeatureState), cookieVal.Value) ? (FeatureState)cookieVal.Value : FeatureState.Enabled;
            if (restoredState == FeatureState.NoAccess) restoredState = FeatureState.Enabled;
            user.FeatureStates[feature.Key] = restoredState;
        }
    }

    public Task AddVip(long accountId, string name, string group, int time, bool global = false) =>
        AddVip(accountId, name, group, time <= 0 ? null : () => CalculateExpires(time), global);

    /// <summary>As the TimeMode overload, for an exact length; zero or less is permanent.</summary>
    public Task AddVip(long accountId, string name, string group, TimeSpan length, bool global = false) =>
        AddVip(accountId, name, group, length <= TimeSpan.Zero ? null : () => DateTimeOffset.UtcNow.Add(length).ToUnixTimeSeconds(), global);

    /// <param name="calculateExpires">The expiry of a grant starting now; null grants permanently.</param>
    private async Task AddVip(long accountId, string name, string group, Func<long>? calculateExpires, bool global)
    {
        accountId = NormalizeToAccountId(accountId);
        var serverId = global ? 0 : serverIdentifier.ServerId;
        if (!global && serverId <= 0) throw new InvalidOperationException("Server ID is not initialized.");
        var existingUser = await userRepository.GetUserAsync(accountId, serverId, group);
        var steamId64 = ToSteamId64(accountId);
        if (existingUser == null && steamId64 != accountId)
            existingUser = await userRepository.GetUserAsync(steamId64, serverId, group);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        long expires;

        if (calculateExpires == null)
        {
            expires = 0; // Permanent
        }
        else if (existingUser != null && existingUser.group.Equals(group, StringComparison.OrdinalIgnoreCase) && existingUser.expires > 0)
        {
            var remaining = existingUser.expires > now ? existingUser.expires - now : 0;
            
            // calculateExpires() returns the absolute time in the future. 
            // We need to add the remaining seconds to that.
            expires = calculateExpires() + remaining;
        }
        else
        {
            expires = calculateExpires();
        }

        // A grant still running keeps the date it was first made; a lapsed or missing one starts now.
        var running = existingUser != null && (existingUser.expires == 0 || existingUser.expires > now);
        var granted = running && existingUser!.granted > 0 ? existingUser.granted : now;

        var user = new User
        {
            account_id = existingUser?.account_id ?? accountId,
            name = name,
            lastvisit = now,
            sid = serverId,
            group = group,
            expires = expires,
            granted = granted
        };

        await userRepository.AddUserAsync(user);
    }

    /// <summary>
    /// Extends a stored grant by <paramref name="time"/> TimeMode units, counted from its expiry or from now
    /// when that has passed. Returns the new expiry.
    /// </summary>
    public async Task<long> ExtendVip(User grant, int time)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var from = grant.expires == 0 ? now : Math.Max(grant.expires, now);

        var updated = CopyOf(grant);
        updated.expires = AddTime(DateTimeOffset.FromUnixTimeSeconds(from), time);
        if (updated.granted == 0) updated.granted = now;

        await userRepository.UpdateUserAsync(updated);
        return updated.expires;
    }

    public async Task MakeVipPermanent(User grant)
    {
        var updated = CopyOf(grant);
        updated.expires = 0;
        await userRepository.UpdateUserAsync(updated);
    }

    private static User CopyOf(User grant) => new()
    {
        account_id = grant.account_id,
        name = grant.name,
        lastvisit = grant.lastvisit,
        sid = grant.sid,
        group = grant.group,
        expires = grant.expires,
        granted = grant.granted
    };

    public async Task RemoveVip(long accountId, bool global = false)
    {
        var normalized = NormalizeToAccountId(accountId);
        var steamId64 = ToSteamId64(accountId);
        var serverId = global ? 0 : serverIdentifier.ServerId;
        if (!global && serverId <= 0) throw new InvalidOperationException("Server ID is not initialized.");

        await userRepository.DeleteUserAsync(normalized, serverId);
        if (steamId64 != normalized)
            await userRepository.DeleteUserAsync(steamId64, serverId);

        _users.TryRemove((ulong)steamId64, out _);
        _users.TryRemove((ulong)normalized, out _);
    }

    public async Task RemoveVipGroup(long accountId, string group, long? serverId = null)
    {
        var normalized = NormalizeToAccountId(accountId);
        var steamId64 = ToSteamId64(accountId);
        var grantServerId = serverId ?? serverIdentifier.ServerId;
        if (grantServerId < 0 || (serverId == null && grantServerId == 0))
            throw new InvalidOperationException("Server ID is not initialized.");

        await userRepository.DeleteUserGroupAsync(normalized, grantServerId, group);
        if (steamId64 != normalized)
            await userRepository.DeleteUserGroupAsync(steamId64, grantServerId, group);

        _users.TryRemove((ulong)steamId64, out _);
        _users.TryRemove((ulong)normalized, out _);
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

            var hasFeatureKey = group.HasFeature(featureKey);

            if (!hasFeatureKey)
            {
                user.FeatureStates[featureKey] = FeatureState.NoAccess;
                continue;
            }

            var cookieVal = cookieService.GetCookie<int?>((ulong)ToSteamId64(user.account_id), featureKey);
            var restoredState = cookieVal.HasValue && Enum.IsDefined(typeof(FeatureState), cookieVal.Value) ? (FeatureState)cookieVal.Value : FeatureState.Enabled;
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
        return AddTime(DateTimeOffset.UtcNow, time);
    }

    private long AddTime(DateTimeOffset from, int time)
    {
        return coreConfig.TimeMode switch
        {
            1 => from.AddMinutes(time).ToUnixTimeSeconds(),
            2 => from.AddHours(time).ToUnixTimeSeconds(),
            3 => from.AddDays(time).ToUnixTimeSeconds(),
            _ => from.AddSeconds(time).ToUnixTimeSeconds()
        };
    }
}
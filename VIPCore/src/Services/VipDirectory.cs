using SwiftlyS2.Shared;
using VIPCore.Contract;
using VIPCore.Database.Repositories;
using VIPCore.Models;

using Microsoft.Extensions.Logging;

namespace VIPCore.Services;

/// <summary>A VIP as the admin pages list them; <see cref="Rows"/>[i] is the stored row behind Info.Grants[i].</summary>
public sealed record DirectoryUser(VipUserInfo Info, IReadOnlyList<User> Rows);

/// <summary>Every VIP at one moment. <see cref="Version"/> is the directory's when the load began.</summary>
public sealed record DirectorySnapshot(IReadOnlyList<DirectoryUser> Users, int EndingThisWeek, int Version)
{
    public VipUserStats Stats => new(Users.Count, EndingThisWeek);
}

/// <summary>The VIPs this server can see, for the admin pages, and the counts the menus show.</summary>
public class VipDirectory(ISwiftlyCore core, IUserRepository userRepository, VipService vipService, ServerIdentifier serverIdentifier)
{
    private static readonly long WeekSeconds = (long)TimeSpan.FromDays(7).TotalSeconds;

    private int _version;
    private volatile VipUserStats? _stats;

    /// <summary>How many VIPs there are, or null before the first load.</summary>
    public int? UserCount => _stats?.Users;

    /// <summary>How many grants run out within seven days.</summary>
    public int EndingThisWeek => _stats?.EndingThisWeek ?? 0;

    public VipUserStats? Stats => _stats;

    /// <summary>Bumped by every change made through VIPCore, so a page built before it can tell it is stale.</summary>
    public int Version => Volatile.Read(ref _version);

    public async Task<IReadOnlyList<VipUserInfo>> LoadUsersAsync()
    {
        var snapshot = await LoadAsync();
        return snapshot.Users.Select(user => user.Info).ToList();
    }

    public async Task<DirectorySnapshot> LoadAsync()
    {
        var version = Version;
        var rows = (await userRepository.GetAllUsersAsync(serverIdentifier.EffectiveServerId)).ToList();
        var online = await GameThread.Read(core, OnlinePlayers);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        // Lapsed rows linger until their player reconnects; they are no longer grants.
        var users = rows
            .Where(row => row.expires == 0 || row.expires >= now)
            .GroupBy(row => (ulong)VipService.ToSteamId64(row.account_id))
            .Select(user => ToUser(user.Key, user.ToList(), online))
            .OrderBy(user => user.Info.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(user => user.Info.SteamId)
            .ToList();

        var ending = users.Sum(user => user.Info.Grants.Count(grant => !grant.Permanent && grant.ExpiresAt <= now + WeekSeconds));
        _stats = new VipUserStats(users.Count, ending);
        return new DirectorySnapshot(users, ending, version);
    }

    /// <summary>Records a change to the stored grants, then reloads.</summary>
    public Task<DirectorySnapshot> ChangedAsync()
    {
        Interlocked.Increment(ref _version);
        return LoadAsync();
    }

    /// <summary>Reloads the counts, logging instead of throwing.</summary>
    public async Task RefreshAsync(bool changed = false)
    {
        try
        {
            if (changed)
                await ChangedAsync();
            else
                await LoadAsync();
        }
        catch (Exception ex)
        {
            core.Logger.LogWarning(ex, "[VIPCore] Failed to refresh the VIP user counts.");
        }
    }

    private DirectoryUser ToUser(ulong steamId, List<User> rows, Dictionary<ulong, string> online)
    {
        var grants = vipService.OrderGrants(rows);
        var latest = rows.MaxBy(row => row.lastvisit)!;
        var isOnline = online.TryGetValue(steamId, out var onlineName);

        var info = new VipUserInfo(steamId, isOnline ? onlineName! : latest.name, latest.lastvisit, isOnline,
            grants.Select(grant => grant.ToGrant()).ToList());
        return new DirectoryUser(info, grants);
    }

    private Dictionary<ulong, string> OnlinePlayers()
    {
        var online = new Dictionary<ulong, string>();
        foreach (var player in core.PlayerManager.GetAllValidPlayers())
        {
            if (player.IsFakeClient) continue;
            online[player.SteamID] = player.Controller?.PlayerName ?? "unknown";
        }

        return online;
    }
}

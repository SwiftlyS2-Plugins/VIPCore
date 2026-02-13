using SwiftlyS2.Shared;
using Dapper;
using Dommel;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using VIPCore.Models;

namespace VIPCore.Database.Repositories;

public interface IUserRepository
{
    Task<User?> GetUserAsync(long accountId, long serverId);
    Task<IEnumerable<User>> GetUserGroupsAsync(long accountId, long serverId);
    Task<IEnumerable<User>> GetExpiredUsersAsync(long serverId, long currentTime);
    Task AddUserAsync(User user);
    Task UpdateUserAsync(User user);
    Task DeleteUserAsync(long accountId, long serverId);
    Task DeleteUserGroupAsync(long accountId, long serverId, string group);
    Task<bool> ServerExistsAsync(string ip, int port);
    Task AddServerAsync(VipServer server);
    Task<long> GetServerIdAsync(string ip, int port);
    Task<IEnumerable<User>> GetAllUsersAsync(long serverId);
}

public class UserRepository(DatabaseConnectionFactory connectionFactory) : IUserRepository
{
    private static bool IsDuplicateKeyException(Exception ex)
    {
        var msg = ex.Message;
        if (string.IsNullOrEmpty(msg)) return false;

        return msg.Contains("Duplicate entry", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("duplicate key", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("UNIQUE constraint failed", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<User?> GetUserAsync(long accountId, long serverId)
    {
        using var db = connectionFactory.CreateConnection();
        var users = await db.SelectAsync<User>(u => u.account_id == accountId && u.sid == serverId);
        return users.FirstOrDefault();
    }

    public async Task<IEnumerable<User>> GetUserGroupsAsync(long accountId, long serverId)
    {
        using var db = connectionFactory.CreateConnection();
        var users = await db.SelectAsync<User>(u => u.account_id == accountId && u.sid == serverId);
        return users.ToList();
    }

    public async Task<IEnumerable<User>> GetExpiredUsersAsync(long serverId, long currentTime)
    {
        using var db = connectionFactory.CreateConnection();
        var users = await db.SelectAsync<User>(u => u.sid == serverId && u.expires < currentTime && u.expires > 0);
        return users.ToList();
    }

    public async Task AddUserAsync(User user)
    {
        using var db = connectionFactory.CreateConnection();
        try
        {
            await db.InsertAsync(user);
        }
        catch (Exception ex) when (IsDuplicateKeyException(ex))
        {
            await db.UpdateAsync(user);
        }
    }

    public async Task UpdateUserAsync(User user)
    {
        using var db = connectionFactory.CreateConnection();
        await db.UpdateAsync(user);
    }

    public async Task DeleteUserAsync(long accountId, long serverId)
    {
        using var db = connectionFactory.CreateConnection();
        var groups = await db.SelectAsync<User>(u => u.account_id == accountId && u.sid == serverId);
        foreach (var user in groups)
            await db.DeleteAsync(user);
    }

    public async Task DeleteUserGroupAsync(long accountId, long serverId, string group)
    {
        using var db = connectionFactory.CreateConnection();
        var user = new User { account_id = accountId, sid = serverId, group = group, name = string.Empty };
        await db.DeleteAsync(user);
    }

    public async Task<bool> ServerExistsAsync(string ip, int port)
    {
        using var db = connectionFactory.CreateConnection();
        var servers = await db.SelectAsync<VipServer>(s => s.serverIp == ip && s.port == port);
        return servers.Any();
    }

    public async Task AddServerAsync(VipServer server)
    {
        using var db = connectionFactory.CreateConnection();
        await db.InsertAsync(server);
    }

    public async Task<long> GetServerIdAsync(string ip, int port)
    {
        using var db = connectionFactory.CreateConnection();
        var servers = await db.SelectAsync<VipServer>(s => s.serverIp == ip && s.port == port);
        var server = servers.FirstOrDefault();
        return server?.serverId ?? 0;
    }

    public async Task<IEnumerable<User>> GetAllUsersAsync(long serverId)
    {
        using var db = connectionFactory.CreateConnection();
        var users = await db.SelectAsync<User>(u => u.sid == serverId);
        return users.ToList();
    }
}
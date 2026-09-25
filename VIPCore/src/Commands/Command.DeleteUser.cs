using SwiftlyS2.Shared.Commands;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using VIPCore.Services;

namespace VIPCore;

public sealed partial class VIPCore
{
    [Command("vip_deleteuser", registerRaw: true, permission: "vipcore.deleteuser")]
    [CommandAlias("vipdelete", registerRaw: true)]
    public void OnDeleteUserCommand(ICommandContext context) => DeleteUser(context, false);

    [Command("vip_deleteglobal", registerRaw: true, permission: "vipcore.deleteglobal")]
    public void OnDeleteGlobalCommand(ICommandContext context) => DeleteUser(context, true);

    private void DeleteUser(ICommandContext context, bool global)
    {
        if (_serviceProvider == null)
        {
            context.Reply("VIPCore is not initialized.");
            return;
        }

        if (context.Args.Length < 1)
        {
            context.Reply($"Usage: {(global ? "vip_deleteglobal" : "vip_deleteuser")} <steamid>");
            return;
        }

        if (!long.TryParse(context.Args[0], out var steamId))
        {
            context.Reply($"Invalid SteamID format: {context.Args[0]}");
            return;
        }

        var vipService = _serviceProvider.GetRequiredService<VipService>();

        Task.Run(async () =>
        {
            try
            {
                await vipService.RemoveVip(steamId, global);
                Core.Scheduler.NextTick(() =>
                {
                    var target = FindOnlinePlayerBySteamId((ulong)VipService.ToSteamId64(steamId));
                    if (target != null)
                    {
                        Task.Run(async () =>
                        {
                            try { await vipService.LoadPlayer(target); }
                            catch (Exception ex) { Core.Logger.LogError(ex, "[VIPCore] Failed to reload VIP player {SteamId}", steamId); }
                        });
                    }
                    context.Reply($"Removed {(global ? "global" : "local")} VIP: {steamId}");
                });
            }
            catch (Exception ex)
            {
                Core.Logger.LogError(ex, "[VIPCore] Failed to remove VIP user {SteamId}", steamId);
                Core.Scheduler.NextTick(() =>
                {
                    context.Reply($"Failed to remove VIP: {ex.Message}");
                });
            }
        });
    }
}

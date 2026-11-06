using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Commands;
using SwiftlyS2.Shared.Players;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using VIPCore.Contract;
using VIPCore.Services;
using VIPCore.Services.Menus;

namespace VIPCore;

public sealed partial class VIPCore
{
    [Command("vip")]
    public void OnVipCommand(ICommandContext context)
    {
        if (!context.IsSentByPlayer)
        {
            context.Reply("This command can only be used by players!");
            return;
        }

        var serviceProvider = _serviceProvider;
        if (serviceProvider == null)
        {
            context.Reply("VIPCore is not initialized.");
            return;
        }

        var player = context.Sender!;
        if (player == null) return;

        var vipService = serviceProvider.GetRequiredService<VipService>();
        var menus = serviceProvider.GetRequiredService<VipMenus>();

        Core.Scheduler.NextTick(() =>
        {
            if (!player.IsValid) return;

            if (vipService.IsClientVip(player))
            {
                menus.OpenMenu(player, VipMenuIds.Perks);
            }
            else if (menus.CanManage(player))
            {
                menus.OpenMenu(player, VipMenuIds.Users);
            }
            else
            {
                var localizer = Core.Translation.GetPlayerLocalizer(player);
                player.SendMessage(MessageType.Chat, localizer["vip.NoAccess"]);
            }
        });
    }
}

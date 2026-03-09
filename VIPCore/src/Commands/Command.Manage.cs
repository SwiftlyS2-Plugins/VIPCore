using SwiftlyS2.Shared.Commands;
using Microsoft.Extensions.DependencyInjection;
using VIPCore.Services;

namespace VIPCore;

public sealed partial class VIPCore
{
    [Command("vip_manage", permission: "vipcore.manage")]
    [CommandAlias("vipmanage", registerRaw: true)]
    public void OnManageCommand(ICommandContext context)
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

        var manageMenuService = serviceProvider.GetRequiredService<ManageMenuService>();
        manageMenuService.OpenManageMenu(player);
    }
}

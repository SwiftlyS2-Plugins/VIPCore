using System.Globalization;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Menu;
using SwiftlyS2.Shared.Menu.Components;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.Translation;
using VIPCore.Config;

using Microsoft.Extensions.Options;

namespace VIPCore.Services.Menus;

/// <summary>What every VIPCore page shares: building, freezing, permissions and translations.</summary>
public class MenuKit(ISwiftlyCore core, IOptionsMonitor<VipConfig> configMonitor)
{
    public const string ManagePermission = "vipcore.manage";

    /// <summary>Marks the chosen row of a set for renderers that only draw text.</summary>
    public const string Marker = " *";

    public ISwiftlyCore Core => core;

    public VipConfig Config => configMonitor.CurrentValue;

    public ILocalizer Loc(IPlayer player) => core.Translation.GetPlayerLocalizer(player);

    public IMenuBuilder Create(string id, string title) =>
        core.Menu.CreateMenu(id).AddHeader(new TitleComponent(title) { Overflow = MenuTextOverflow.None });

    /// <summary>Builds a page; admin pages freeze with FreezeAdminMenu, player pages with FreezeMenu.</summary>
    public IMenu Finish(IMenuBuilder builder, bool adminPage)
    {
        var menu = builder.Build().WithDefaultOption();
        if (adminPage ? Config.FreezeAdminMenu : Config.FreezeMenu)
            menu.FreezeWhileOpen(core);

        return menu;
    }

    public bool CanManage(IPlayer player) =>
        player.IsValid && !player.IsFakeClient && core.Permission.PlayerHasPermission(player.SteamID, ManagePermission);

    /// <summary>Checked before every admin action, since the permission can be taken away while a page is open.</summary>
    public bool EnsureManage(IMenuSession session)
    {
        if (CanManage(session.Player)) return true;

        if (session.Player.IsValid)
            session.Player.SendMessage(MessageType.Chat, Loc(session.Player)["vip.NoAccess"]);
        session.Close();
        return false;
    }

    /// <summary>Whether the player is still on this page, so work that finished later may redraw it.</summary>
    public bool IsShowing(IPlayer player, IMenu menu) =>
        player.IsValid && ReferenceEquals(core.Menu.GetSession(player)?.Menu, menu);

    /// <summary>Opens a rebuilt page in place of the one the player is on, returning to the same parent.</summary>
    public void Replace(IPlayer player, IMenu page, IMenu? parent, string? focusId)
    {
        page.Parent = parent;
        page.Open(player).FocusOn(focusId);
    }

    public static string Mark(string text, bool chosen) => chosen ? text + Marker : text;

    public static string Date(long unixSeconds) =>
        DateTimeOffset.FromUnixTimeSeconds(unixSeconds).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}

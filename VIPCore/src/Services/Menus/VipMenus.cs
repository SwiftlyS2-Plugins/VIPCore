using SwiftlyS2.Shared.Menu;
using SwiftlyS2.Shared.Menu.Components;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.Translation;
using VIPCore.Contract;

namespace VIPCore.Services.Menus;

/// <summary>The VIP hub, built per player, and the way into it and its pages.</summary>
public class VipMenus(MenuKit kit, VipService vipService, VipDirectory directory, MenuPageRegistry pages, PerkPages perkPages, UserPages userPages, GrantPage grantPage)
{
    public bool CanManage(IPlayer player) => kit.CanManage(player);

    /// <summary>
    /// Opens the hub, or a page of it with the hub as its parent. Off the game thread it opens on the next tick
    /// and the answer is a guess that does not ask module pages' IsAvailable.
    /// </summary>
    public bool OpenMenu(IPlayer player, string? pageId = null)
    {
        if (kit.Core.IsGameThread) return Open(player, pageId);

        kit.Core.Scheduler.NextTick(() => Open(player, pageId));
        return !player.IsFakeClient && (vipService.IsClientVip(player) || kit.CanManage(player) || !pages.IsEmpty);
    }

    private bool Open(IPlayer player, string? pageId)
    {
        if (!player.IsValid || player.IsFakeClient) return false;

        var hub = BuildHub(player);
        if (hub == null) return false;

        if (pageId != null && UserPages.IsUsersPage(pageId) && kit.CanManage(player))
        {
            userPages.LoadAndOpen(player, hub, pageId);
            return true;
        }

        var page = pageId == null ? null : BuildPage(player, pageId, hub);
        if (page == null)
        {
            hub.Open(player);
            return true;
        }

        page.Parent = hub;
        page.Open(player);
        return true;
    }

    private IMenu? BuildPage(IPlayer player, string pageId, IMenu hub)
    {
        if (pageId.Equals(VipMenuIds.Perks, StringComparison.OrdinalIgnoreCase))
            return perkPages.BuildPerks(player);
        if (pageId.Equals(VipMenuIds.Customize, StringComparison.OrdinalIgnoreCase))
            return perkPages.HasCustomize(player) ? perkPages.BuildCustomize(player) : null;
        if (pageId.Equals(VipMenuIds.Grant, StringComparison.OrdinalIgnoreCase))
            return kit.CanManage(player) ? grantPage.Build(player, new GrantState()) : null;

        return pages.Find(pageId) is { } page ? pages.Build(page, player, hub) : null;
    }

    // Plain SubmenuComponents with fixed text and the page's menu id: a renderer can mirror them as navigation.
    private IMenu? BuildHub(IPlayer player)
    {
        var loc = kit.Loc(player);
        var entries = new List<IMenuComponent>();

        if (perkPages.HasPerks(player))
            entries.Add(new SubmenuComponent(loc["menu.Perks"], session => perkPages.BuildPerks(session.Player), VipMenuIds.Perks));
        if (perkPages.HasCustomize(player))
            entries.Add(new SubmenuComponent(loc["menu.Customize"], session => perkPages.BuildCustomize(session.Player), VipMenuIds.Customize));

        foreach (var page in pages.Available(player))
            entries.Add(new SubmenuComponent(pages.Label(page, player), session => pages.Build(page, session.Player, session.Menu), page.Id));

        var playerEntries = entries.Count;
        SubmenuComponent? usersEntry = null;

        if (kit.CanManage(player))
        {
            if (playerEntries > 0)
                entries.Add(new DividerComponent());

            usersEntry = new SubmenuComponent(UsersLabel(loc), userPages.OpenFromHub, VipMenuIds.Users);
            entries.Add(usersEntry);
            entries.Add(new SubmenuComponent(loc["manage.Grant"],
                session => kit.EnsureManage(session) ? grantPage.Build(session.Player, new GrantState()) : null, VipMenuIds.Grant));
        }

        if (entries.Count == 0) return null;

        var builder = kit.Create(VipMenuIds.Hub, loc["menu.Hub"]);
        foreach (var entry in entries)
            builder.Add(entry);

        var hub = kit.Finish(builder, adminPage: playerEntries == 0);

        // Back from the admin pages, the count reflects what was changed there.
        if (usersEntry != null)
            hub.Opened += session => usersEntry.Text = UsersLabel(kit.Loc(session.Player));

        return hub;
    }

    private string UsersLabel(ILocalizer loc) =>
        directory.UserCount is { } count ? loc["manage.UsersCount", count] : loc["manage.Users"];
}

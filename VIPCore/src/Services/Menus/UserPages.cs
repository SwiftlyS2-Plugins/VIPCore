using SwiftlyS2.Shared.Menu;
using SwiftlyS2.Shared.Menu.Components;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.Translation;
using VIPCore.Api;
using VIPCore.Contract;
using VIPCore.Models;

using Microsoft.Extensions.Logging;

namespace VIPCore.Services.Menus;

/// <summary>One admin's VIP users pages: the users they show and what is selected on them.</summary>
internal sealed class UsersState(DirectorySnapshot snapshot)
{
    public DirectorySnapshot Snapshot { get; set; } = snapshot;
    public ulong? Selected { get; set; }
    /// <summary>The <see cref="UserPages.GrantKey"/> of the selected user's expanded grant.</summary>
    public string? Expanded { get; set; }
    public TimeSpan? LastStep { get; set; }
    public bool Busy { get; set; }
}

/// <summary>VIP users: a host of tabs (all users, then one per group) over the list pages.</summary>
public class UserPages(MenuKit kit, VipService vipService, VipDirectory directory, FeatureViewService views, GrantPage grantPage, VipCoreApiV1 api)
{
    public static bool IsUsersPage(string pageId) =>
        pageId.Equals(VipMenuIds.Users, StringComparison.OrdinalIgnoreCase)
        || pageId.Equals(VipMenuIds.UsersList, StringComparison.OrdinalIgnoreCase)
        || pageId.StartsWith(VipMenuIds.UsersList + ".", StringComparison.OrdinalIgnoreCase);

    /// <summary>The hub entry's factory: loads the users, then hands the host to SubmenuComponent.</summary>
    public ValueTask<IMenu?> OpenFromHub(IMenuSession session)
    {
        if (!kit.EnsureManage(session)) return ValueTask.FromResult<IMenu?>(null);

        var admin = session.Player;
        // Completed on the game thread with inline continuations, so SubmenuComponent opens the host there.
        var host = new TaskCompletionSource<IMenu?>();
        Load(admin, snapshot =>
        {
            IMenu? menu = null;
            try
            {
                if (snapshot != null && kit.CanManage(admin))
                    menu = BuildHost(admin, new UsersState(snapshot));
            }
            finally
            {
                host.TrySetResult(menu);
            }
        });

        return new ValueTask<IMenu?>(host.Task);
    }

    /// <summary>Loads the users, then opens the host, or one of its lists, under the hub.</summary>
    public void LoadAndOpen(IPlayer admin, IMenu hub, string pageId)
    {
        Load(admin, snapshot =>
        {
            if (!admin.IsValid) return;
            if (snapshot == null || !kit.CanManage(admin))
            {
                hub.Open(admin);
                return;
            }

            var state = new UsersState(snapshot);
            var host = BuildHost(admin, state);
            host.Parent = hub;

            if (!TryListFilter(pageId, snapshot, out var group))
            {
                host.Open(admin);
                return;
            }

            var list = BuildList(admin, state, group);
            list.Parent = host;
            list.Open(admin);
        });
    }

    internal static string GrantKey(User row) => $"{row.sid}:{row.group.ToLowerInvariant()}";

    private IMenu BuildHost(IPlayer admin, UsersState state)
    {
        var loc = kit.Loc(admin);
        var users = state.Snapshot.Users;
        var builder = kit.Create(VipMenuIds.Users, loc["manage.Users"]);

        builder.Add(new SubmenuComponent(loc["manage.AllUsers", users.Count], session => OpenList(session, state, null), VipMenuIds.UsersList));
        foreach (var group in views.GroupInfos())
        {
            var count = users.Count(user => HasGroup(user, group.Name));
            if (count == 0) continue;

            var name = group.Name;
            builder.Add(new SubmenuComponent(loc["manage.GroupEntry", name, count], session => OpenList(session, state, name), ListId(name)));
        }

        var host = kit.Finish(builder, adminPage: true);
        host.Tag = state.Snapshot.Stats;
        ReloadWhenStale(host, admin, state, () => BuildHost(admin, state));
        return host;
    }

    private IMenu? OpenList(IMenuSession session, UsersState state, string? group) =>
        kit.EnsureManage(session) ? BuildList(session.Player, state, group) : null;

    private IMenu BuildList(IPlayer admin, UsersState state, string? group)
    {
        var loc = kit.Loc(admin);
        var users = group == null
            ? state.Snapshot.Users
            : state.Snapshot.Users.Where(user => HasGroup(user, group)).ToList();
        var selected = Selection(state, users);

        var builder = kit.Create(ListId(group), loc["manage.Users"]);

        // The selection leads, so a text menu shows it without scrolling.
        if (selected != null)
        {
            var steps = VipLengths.ExtendSteps(kit.Config.TimeMode);
            for (var i = 0; i < selected.Rows.Count; i++)
            {
                var row = selected.Rows[i];
                var grant = selected.Info.Grants[i];
                var key = GrantKey(row);
                var expanded = key == state.Expanded;

                builder.Add(new ButtonComponent(MenuKit.Mark(GrantText(loc, grant), expanded), ctx => Expand(ctx, state, group, key), $"grant:{key}")
                {
                    Tag = new VipGrantRow(grant, expanded)
                });

                if (!expanded) continue;

                // The expanded grant's controls sit directly under it, before the next grant.
                foreach (var step in steps)
                {
                    builder.Add(new ButtonComponent("+" + loc[step.LabelKey], ctx => Extend(ctx, state, group, selected, row, step), $"extend:{key}:{step.Time}")
                    {
                        Tag = new VipLengthRow(step.Span, step.Span == state.LastStep, grant),
                        // Extending counts from now for a permanent grant, which would cut it short.
                        Enabled = !grant.Permanent
                    });
                }

                builder.Add(new ButtonComponent(loc["manage.MakePermanent"], ctx => MakePermanent(ctx, state, group, selected, row), $"permanent:{key}")
                {
                    Tag = new VipActionRow(VipAction.MakePermanent, grant),
                    Enabled = !grant.Permanent
                });
                builder.Add(new ButtonComponent(loc["manage.Remove"], ctx => Remove(ctx, state, group, selected, row), $"remove:{key}")
                {
                    Tag = new VipActionRow(VipAction.Remove, grant)
                });
            }

            builder.Add(new ButtonComponent(loc["manage.AddGroup"], ctx => AddGroup(ctx, selected), "addgroup")
            {
                Tag = new VipActionRow(VipAction.AddGroup)
            });
            builder.Add(new ButtonComponent(loc["manage.RemoveAll"], ctx => RemoveAll(ctx, state, group, selected), "removeall")
            {
                Tag = new VipActionRow(VipAction.RemoveAll)
            });
        }

        foreach (var user in users)
        {
            var isSelected = ReferenceEquals(user, selected);
            var steamId = user.Info.SteamId;
            builder.Add(new ButtonComponent(MenuKit.Mark(UserText(loc, user.Info), isSelected), ctx => Select(ctx, state, group, steamId), UserId(steamId))
            {
                Tag = new VipUserRow(user.Info, isSelected)
            });
        }

        var list = kit.Finish(builder, adminPage: true);
        list.Tag = state.Snapshot.Stats;
        ReloadWhenStale(list, admin, state, () => BuildList(admin, state, group));
        return list;
    }

    // The page always shows someone's grants: without a selection in this filter, its first user's, first grant expanded.
    private static DirectoryUser? Selection(UsersState state, IReadOnlyList<DirectoryUser> users)
    {
        var selected = users.FirstOrDefault(user => user.Info.SteamId == state.Selected);
        if (selected == null)
        {
            selected = users.FirstOrDefault();
            state.Selected = selected?.Info.SteamId;
            state.Expanded = null;
        }

        if (selected != null && !selected.Rows.Any(row => GrantKey(row) == state.Expanded))
            state.Expanded = selected.Rows.Count > 0 ? GrantKey(selected.Rows[0]) : null;

        return selected;
    }

    private ValueTask Select(MenuActionContext ctx, UsersState state, string? group, ulong steamId)
    {
        if (!kit.EnsureManage(ctx.Session) || state.Selected == steamId) return ValueTask.CompletedTask;

        state.Selected = steamId;
        state.Expanded = null;
        Redraw(ctx, state, group, UserId(steamId));
        return ValueTask.CompletedTask;
    }

    private ValueTask Expand(MenuActionContext ctx, UsersState state, string? group, string grantKey)
    {
        if (!kit.EnsureManage(ctx.Session) || state.Expanded == grantKey) return ValueTask.CompletedTask;

        state.Expanded = grantKey;
        Redraw(ctx, state, group, $"grant:{grantKey}");
        return ValueTask.CompletedTask;
    }

    private ValueTask Extend(MenuActionContext ctx, UsersState state, string? group, DirectoryUser user, User row, VipLengths.Length step)
    {
        long expires = 0;
        return Run(ctx, state, group, user, $"extend:{GrantKey(row)}:{step.Time}",
            async () =>
            {
                state.LastStep = step.Span;
                expires = await vipService.ExtendVip(row, step.Time);
            },
            loc => loc["manage.chat.ExtendedVip", row.name, loc[step.LabelKey], DateTimeOffset.FromUnixTimeSeconds(expires).ToString("yyyy-MM-dd HH:mm")],
            (loc, ex) => loc["manage.chat.FailedExtend", ex.Message]);
    }

    private ValueTask MakePermanent(MenuActionContext ctx, UsersState state, string? group, DirectoryUser user, User row) =>
        Run(ctx, state, group, user, $"permanent:{GrantKey(row)}",
            () => vipService.MakeVipPermanent(row),
            loc => loc["manage.chat.MadePermanent", row.name],
            (loc, ex) => loc["manage.chat.FailedMakePermanent", ex.Message]);

    private ValueTask Remove(MenuActionContext ctx, UsersState state, string? group, DirectoryUser user, User row) =>
        Run(ctx, state, group, user, null,
            () => vipService.RemoveVipGroup(row.account_id, row.group, row.sid),
            loc => loc["manage.chat.RemovedVip", row.name, user.Info.SteamId],
            (loc, ex) => loc["manage.chat.FailedRemoveVip", ex.Message]);

    private ValueTask RemoveAll(MenuActionContext ctx, UsersState state, string? group, DirectoryUser user) =>
        Run(ctx, state, group, user, null,
            async () =>
            {
                var steamId = (long)user.Info.SteamId;
                if (user.Rows.Any(row => row.sid != 0))
                    await vipService.RemoveVip(steamId, global: false);
                if (user.Rows.Any(row => row.sid == 0))
                    await vipService.RemoveVip(steamId, global: true);
            },
            loc => loc["manage.chat.RemovedVip", user.Info.Name, user.Info.SteamId],
            (loc, ex) => loc["manage.chat.FailedRemoveVip", ex.Message]);

    private ValueTask AddGroup(MenuActionContext ctx, DirectoryUser user)
    {
        if (!kit.EnsureManage(ctx.Session)) return ValueTask.CompletedTask;

        var page = grantPage.Build(ctx.Player, new GrantState
        {
            Player = user.Info.SteamId,
            PlayerName = user.Info.Name,
            PlayerGroup = user.Info.Grants.FirstOrDefault()?.Group
        });
        page.Parent = ctx.Menu;
        page.Open(ctx.Player);
        return ValueTask.CompletedTask;
    }

    // The database work runs off the game thread; the page then redraws from a fresh load.
    private ValueTask Run(MenuActionContext ctx, UsersState state, string? group, DirectoryUser user, string? focusId,
        Func<Task> work, Func<ILocalizer, string> succeeded, Func<ILocalizer, Exception, string> failed)
    {
        if (!kit.EnsureManage(ctx.Session) || state.Busy) return ValueTask.CompletedTask;
        state.Busy = true;

        var admin = ctx.Player;
        var page = ctx.Menu;
        var target = kit.Core.PlayerManager.GetPlayerFromSteamId(user.Info.SteamId);
        // Completed on the game thread, so the button shows its waiting text until the page is redrawn.
        var finished = new TaskCompletionSource();

        _ = Task.Run(async () =>
        {
            Exception? error = null;
            DirectorySnapshot? snapshot = null;

            try
            {
                await work();
            }
            catch (Exception ex)
            {
                error = ex;
            }

            if (error == null)
            {
                await api.ReloadPlayer(target);
                try
                {
                    snapshot = await directory.ChangedAsync();
                }
                catch (Exception ex)
                {
                    kit.Core.Logger.LogError(ex, "[VIPCore] Failed to reload VIP users.");
                }
            }

            kit.Core.Scheduler.NextTick(() =>
            {
                try
                {
                    state.Busy = false;
                    if (snapshot != null) state.Snapshot = snapshot;
                    if (!admin.IsValid) return;

                    var loc = kit.Loc(admin);
                    if (error != null)
                    {
                        kit.Core.Logger.LogError(error, "[VIPCore] Failed to change VIP grants of {SteamId}", user.Info.SteamId);
                        admin.SendMessage(MessageType.Chat, failed(loc, error));
                    }
                    else
                    {
                        admin.SendMessage(MessageType.Chat, succeeded(loc));
                    }

                    if (kit.IsShowing(admin, page))
                        kit.Replace(admin, BuildList(admin, state, group), page.Parent, focusId);
                }
                catch (Exception ex)
                {
                    kit.Core.Logger.LogError(ex, "[VIPCore] Failed to redraw the VIP users page.");
                }
                finally
                {
                    finished.TrySetResult();
                }
            });
        });

        return new ValueTask(finished.Task);
    }

    private void Redraw(MenuActionContext ctx, UsersState state, string? group, string? focusId)
    {
        var admin = ctx.Player;
        var page = ctx.Menu;

        // Next tick: replacing the page from inside its own activation would release it mid-dispatch.
        kit.Core.Scheduler.NextTick(() =>
        {
            if (kit.IsShowing(admin, page))
                kit.Replace(admin, BuildList(admin, state, group), page.Parent, focusId);
        });
    }

    // Opened again after a change made elsewhere (Add group, another admin), a page redraws in place.
    private void ReloadWhenStale(IMenu page, IPlayer admin, UsersState state, Func<IMenu> rebuild)
    {
        var builtFrom = state.Snapshot.Version;
        page.Opened += _ =>
        {
            if (state.Busy || builtFrom == directory.Version) return;

            if (state.Snapshot.Version == directory.Version)
            {
                // Another page already reloaded the shared users; redraw from them, after this Open returns.
                kit.Core.Scheduler.NextTick(() =>
                {
                    if (kit.IsShowing(admin, page))
                        kit.Replace(admin, rebuild(), page.Parent, null);
                });
                return;
            }

            state.Busy = true;
            Load(admin, snapshot =>
            {
                state.Busy = false;
                if (snapshot == null) return;

                state.Snapshot = snapshot;
                if (kit.IsShowing(admin, page))
                    kit.Replace(admin, rebuild(), page.Parent, null);
            });
        };
    }

    private void Load(IPlayer admin, Action<DirectorySnapshot?> then)
    {
        _ = Task.Run(async () =>
        {
            DirectorySnapshot? snapshot = null;
            try
            {
                snapshot = await directory.LoadAsync();
            }
            catch (Exception ex)
            {
                kit.Core.Logger.LogError(ex, "[VIPCore] Failed to load VIP users.");
            }

            kit.Core.Scheduler.NextTick(() =>
            {
                try
                {
                    if (snapshot == null && admin.IsValid)
                        admin.SendMessage(MessageType.Chat, kit.Loc(admin)["manage.chat.FailedLoadUsers"]);

                    then(snapshot);
                }
                catch (Exception ex)
                {
                    kit.Core.Logger.LogError(ex, "[VIPCore] Failed to open the VIP users page.");
                }
            });
        });
    }

    private bool TryListFilter(string pageId, DirectorySnapshot snapshot, out string? group)
    {
        group = null;
        if (pageId.Equals(VipMenuIds.UsersList, StringComparison.OrdinalIgnoreCase)) return true;
        if (!pageId.StartsWith(VipMenuIds.UsersList + ".", StringComparison.OrdinalIgnoreCase)) return false;

        group = views.GroupInfos()
            .Select(info => info.Name)
            .FirstOrDefault(name => ListId(name).Equals(pageId, StringComparison.OrdinalIgnoreCase)
                && snapshot.Users.Any(user => HasGroup(user, name)));
        return group != null;
    }

    private static string ListId(string? group) =>
        group == null ? VipMenuIds.UsersList : $"{VipMenuIds.UsersList}.{group.ToLowerInvariant()}";

    private static string UserId(ulong steamId) => $"user:{steamId}";

    private static bool HasGroup(DirectoryUser user, string group) =>
        user.Info.Grants.Any(grant => grant.Group.Equals(group, StringComparison.OrdinalIgnoreCase));

    private static string GrantText(ILocalizer loc, VipGrant grant) =>
        loc["manage.GrantEntry", grant.Group, loc[grant.Global ? "manage.Scope.Global" : "manage.Scope.Local"], Expiry(loc, grant)];

    private static string UserText(ILocalizer loc, VipUserInfo user)
    {
        var groups = string.Join(", ", user.Grants.Select(grant => grant.Group).Distinct(StringComparer.OrdinalIgnoreCase));
        return loc["manage.UserEntry", user.Name, groups, user.Grants.Count > 0 ? Expiry(loc, user.Grants[0]) : string.Empty];
    }

    private static string Expiry(ILocalizer loc, VipGrant grant) =>
        grant.Permanent ? loc["manage.Permanent"] : loc["manage.Until", MenuKit.Date(grant.ExpiresAt)];
}

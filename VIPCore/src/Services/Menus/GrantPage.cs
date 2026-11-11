using System.Globalization;
using SwiftlyS2.Shared.Menu;
using SwiftlyS2.Shared.Menu.Components;
using SwiftlyS2.Shared.Players;
using VIPCore.Api;
using VIPCore.Contract;

using Microsoft.Extensions.Logging;

namespace VIPCore.Services.Menus;

/// <summary>What one admin has picked so far on Grant VIP, carried from each step to the next.</summary>
public sealed record GrantState
{
    public ulong? Player { get; init; }
    public string? PlayerName { get; init; }
    public string? Group { get; init; }
    /// <summary>In TimeMode units; 0 is permanent.</summary>
    public int Length { get; init; }
    public string LengthKey { get; init; } = "manage.Permanent";
}

/// <summary>Grant VIP, one choice a page: an online player, the group, how long, then where, which grants it.</summary>
public class GrantPage(MenuKit kit, VipService vipService, VipDirectory directory, FeatureViewService views, VipCoreApiV1 api)
{
    private sealed record Candidate(ulong SteamId, string Name, string? Team, string? Group);

    private sealed class Run
    {
        public bool Busy;
    }

    /// <summary>The first step left to choose: the players, or the groups when the player is already known (Add group).</summary>
    public IMenu Build(IPlayer admin, GrantState state) => state.Player == null ? Players(admin) : Groups(admin, state);

    private IMenu Players(IPlayer admin)
    {
        var builder = kit.Create(VipMenuIds.Grant, kit.Loc(admin)["manage.Grant"]);
        foreach (var candidate in OnlinePlayers())
        {
            var state = new GrantState { Player = candidate.SteamId, PlayerName = candidate.Name };
            builder.Add(new ButtonComponent(candidate.Name, ctx => Open(ctx, () => Groups(ctx.Player, state)), $"player:{candidate.SteamId}")
            {
                Tag = new VipPlayerRow(candidate.SteamId, candidate.Name, candidate.Team, candidate.Group, false)
            });
        }

        return Finish(builder);
    }

    // A player is an action rather than a submenu entry: this page is a list of people, not a set of sections.
    private ValueTask Open(MenuActionContext ctx, Func<IMenu> step)
    {
        if (!kit.EnsureManage(ctx.Session)) return ValueTask.CompletedTask;

        var page = step();
        page.Parent = ctx.Menu;
        page.Open(ctx.Player);
        return ValueTask.CompletedTask;
    }

    private IMenu Groups(IPlayer admin, GrantState state)
    {
        var builder = kit.Create(VipMenuIds.GrantGroup, kit.Loc(admin)["manage.Grant.Group", state.PlayerName ?? string.Empty]);
        foreach (var group in views.GroupInfos())
        {
            var picked = state with { Group = group.Name };
            builder.Add(new SubmenuComponent(group.Name, session => Next(session, () => Lengths(session.Player, picked)), $"group:{group.Name}")
            {
                Tag = new VipGroupRow(group, false)
            });
        }

        return Finish(builder);
    }

    private IMenu Lengths(IPlayer admin, GrantState state)
    {
        var loc = kit.Loc(admin);
        var builder = kit.Create(VipMenuIds.GrantLength, loc["manage.Grant.Length", state.PlayerName ?? string.Empty, state.Group ?? string.Empty]);
        var lengths = VipLengths.GrantOptions(kit.Config.TimeMode).Append(new VipLengths.Length(0, "manage.Permanent", TimeSpan.Zero));
        foreach (var length in lengths)
        {
            var picked = state with { Length = length.Time, LengthKey = length.LabelKey };
            builder.Add(new SubmenuComponent(loc[length.LabelKey], session => Next(session, () => Scopes(session.Player, picked)),
                length.Time == 0 ? "length:permanent" : $"length:{length.Time}")
            {
                Tag = new VipLengthRow(length.Time == 0 ? null : length.Span, false)
            });
        }

        return Finish(builder);
    }

    private IMenu Scopes(IPlayer admin, GrantState state)
    {
        var loc = kit.Loc(admin);
        var builder = kit.Create(VipMenuIds.GrantScope,
            loc["manage.Grant.Scope", state.PlayerName ?? string.Empty, state.Group ?? string.Empty, loc[state.LengthKey]]);
        var run = new Run();
        foreach (var global in new[] { false, true })
        {
            builder.Add(new ButtonComponent(loc[global ? "manage.Scope.Global" : "manage.Scope.Local"], ctx => Submit(ctx, state, global, run),
                global ? "scope:global" : "scope:local")
            {
                Tag = new VipScopeRow(global, false)
            });
        }

        return Finish(builder);
    }

    private IMenu? Next(IMenuSession session, Func<IMenu> step) => kit.EnsureManage(session) ? step() : null;

    private IMenu Finish(IMenuBuilder builder)
    {
        var menu = kit.Finish(builder, adminPage: true);
        menu.Tag = directory.Stats;
        return menu;
    }

    private ValueTask Submit(MenuActionContext ctx, GrantState state, bool global, Run run)
    {
        if (!kit.EnsureManage(ctx.Session) || run.Busy || state.Player is not { } steamId || state.Group is not { } group)
            return ValueTask.CompletedTask;
        run.Busy = true;

        var admin = ctx.Player;
        var page = ctx.Menu;
        var time = state.Length;
        var target = kit.Core.PlayerManager.GetPlayerFromSteamId(steamId);
        var name = target is { IsValid: true } && !target.IsFakeClient
            ? target.Controller?.PlayerName ?? state.PlayerName ?? "unknown"
            : state.PlayerName ?? steamId.ToString(CultureInfo.InvariantCulture);
        // Completed on the game thread, so the button shows its waiting text until the grant is done.
        var finished = new TaskCompletionSource();

        _ = Task.Run(async () =>
        {
            Exception? error = null;
            try
            {
                await vipService.AddVip((long)steamId, name, group, time, global);
            }
            catch (Exception ex)
            {
                error = ex;
            }

            if (error == null)
            {
                await api.ReloadPlayer(target);
                await directory.RefreshAsync(changed: true);
            }

            kit.Core.Scheduler.NextTick(() =>
            {
                try
                {
                    run.Busy = false;
                    if (!admin.IsValid) return;

                    var loc = kit.Loc(admin);
                    var granted = $"{group} ({loc[global ? "manage.Scope.Global" : "manage.Scope.Local"]})";
                    if (error != null)
                    {
                        kit.Core.Logger.LogError(error, "[VIPCore] Failed to add VIP user {SteamId}", steamId);
                        admin.SendMessage(MessageType.Chat, loc["manage.chat.FailedAddVip", error.Message]);
                        return;
                    }

                    admin.SendMessage(MessageType.Chat, time <= 0
                        ? loc["manage.chat.AddedVipPermanent", name, steamId, granted]
                        : loc["manage.chat.AddedVip", name, steamId, granted, loc[state.LengthKey]]);
                    if (kit.IsShowing(admin, page)) ReturnToStart(admin, page, steamId);
                }
                catch (Exception ex)
                {
                    kit.Core.Logger.LogError(ex, "[VIPCore] Failed to leave the Grant VIP steps.");
                }
                finally
                {
                    finished.TrySetResult();
                }
            });
        });

        return new ValueTask(finished.Task);
    }

    // Back where the grant began: the players, rebuilt so the new group shows, or the VIP it was added to.
    private void ReturnToStart(IPlayer admin, IMenu page, ulong steamId)
    {
        var origin = page;
        while (IsStep(origin.Id) && origin.Parent is { } parent)
            origin = parent;

        if (origin.Id == VipMenuIds.Grant)
            kit.Replace(admin, Players(admin), origin.Parent, $"player:{steamId}");
        else
            origin.Open(admin);
    }

    private static bool IsStep(string id) => id is VipMenuIds.GrantGroup or VipMenuIds.GrantLength or VipMenuIds.GrantScope;

    private List<Candidate> OnlinePlayers()
    {
        var candidates = new List<Candidate>();
        foreach (var player in kit.Core.PlayerManager.GetAllValidPlayers())
        {
            if (!player.IsFakeClient && player.SteamID != 0)
                candidates.Add(Describe(player));
        }

        candidates.Sort((a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.Name, b.Name));
        return candidates;
    }

    private Candidate Describe(IPlayer player)
    {
        var controller = player.Controller;
        string? name = null;
        byte team = 0;
        if (controller is { IsValid: true })
        {
            name = controller.PlayerName;
            team = controller.TeamNum;
        }

        var teamName = team switch
        {
            2 => "T",
            3 => "CT",
            1 => "SPEC",
            _ => null
        };

        return new Candidate(player.SteamID, string.IsNullOrEmpty(name) ? player.SteamID.ToString(CultureInfo.InvariantCulture) : name,
            teamName, vipService.GetVipUser(player.SteamID)?.group);
    }
}

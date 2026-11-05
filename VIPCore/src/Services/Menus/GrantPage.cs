using System.Globalization;
using SwiftlyS2.Shared.Menu;
using SwiftlyS2.Shared.Menu.Components;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.Translation;
using VIPCore.Contract;

using Microsoft.Extensions.Logging;

namespace VIPCore.Services.Menus;

/// <summary>What one admin has picked on Grant VIP; it outlives rebuilds of the page.</summary>
public sealed class GrantState
{
    public string Search { get; set; } = string.Empty;
    public ulong? Player { get; set; }
    public string? PlayerName { get; set; }
    /// <summary>The active group of a preselected player who is not online.</summary>
    public string? PlayerGroup { get; set; }
    public string? Group { get; set; }
    public bool Global { get; set; } = true;
    /// <summary>In TimeMode units; 0 is permanent, null the page's default.</summary>
    public int? Length { get; set; }
    public bool Busy { get; set; }
}

/// <summary>Grant VIP: player, group, scope and length on one page, then Grant.</summary>
public class GrantPage(MenuKit kit, VipService vipService, VipDirectory directory, FeatureViewService views)
{
    private const ulong SteamId64Base = 76561197960265728UL;
    private const string SearchId = "search";

    private sealed record Candidate(ulong SteamId, string Name, string? Team, string? Group);

    private sealed class Rows
    {
        public List<(ButtonComponent Row, Candidate Player)> Players { get; } = [];
        public List<(ButtonComponent Row, VipGroupInfo Group)> Groups { get; } = [];
        public List<(ButtonComponent Row, bool Global)> Scopes { get; } = [];
        public List<(ButtonComponent Row, VipLengths.Length Length, bool Permanent)> Lengths { get; } = [];
        public TextComponent Summary { get; } = new(string.Empty, "summary") { Tag = new VipActionRow(VipAction.Summary) };
        public ButtonComponent Submit { get; } = new(string.Empty, id: "submit") { Tag = new VipActionRow(VipAction.Submit) };
    }

    public IMenu Build(IPlayer admin, GrantState state)
    {
        var loc = kit.Loc(admin);
        var groups = views.GroupInfos();
        var lengths = VipLengths.GrantOptions(kit.Config.TimeMode);

        if (state.Group == null || !groups.Any(group => group.Name.Equals(state.Group, StringComparison.OrdinalIgnoreCase)))
            state.Group = groups.FirstOrDefault()?.Name;
        if (state.Length == null || (state.Length != 0 && !lengths.Any(length => length.Time == state.Length)))
            state.Length = DefaultLength(lengths);

        var rows = new Rows();
        var builder = kit.Create(VipMenuIds.Grant, loc["manage.Grant"]);

        builder.Add(new InputComponent(loc["manage.Search"], state.Search, onChanged: change => Search(change, state), id: SearchId)
        {
            MaxLength = 64,
            Tag = new VipActionRow(VipAction.Search)
        });

        foreach (var candidate in Candidates(state))
        {
            var row = new ButtonComponent(candidate.Name, ctx => Pick(ctx, state, rows, Part.Players, () => SelectPlayer(state, candidate)), $"player:{candidate.SteamId}");
            rows.Players.Add((row, candidate));
            builder.Add(row);
        }

        foreach (var group in groups)
        {
            var row = new ButtonComponent(group.Name, ctx => Pick(ctx, state, rows, Part.Groups, () => state.Group = group.Name), $"group:{group.Name}");
            rows.Groups.Add((row, group));
            builder.Add(row);
        }

        foreach (var global in new[] { false, true })
        {
            var row = new ButtonComponent(string.Empty, ctx => Pick(ctx, state, rows, Part.Scopes, () => state.Global = global), global ? "scope:global" : "scope:local");
            rows.Scopes.Add((row, global));
            builder.Add(row);
        }

        foreach (var length in lengths)
        {
            var row = new ButtonComponent(string.Empty, ctx => Pick(ctx, state, rows, Part.Lengths, () => state.Length = length.Time), $"length:{length.Time}");
            rows.Lengths.Add((row, length, false));
            builder.Add(row);
        }

        var permanent = new ButtonComponent(string.Empty, ctx => Pick(ctx, state, rows, Part.Lengths, () => state.Length = 0), "length:permanent");
        rows.Lengths.Add((permanent, new VipLengths.Length(0, "manage.Permanent", TimeSpan.Zero), true));
        builder.Add(permanent);

        rows.Submit.Text = loc["manage.Grant"];
        rows.Submit.OnSelect = ctx => Submit(ctx, state, rows);
        builder.Add(rows.Summary);
        builder.Add(rows.Submit);

        foreach (var part in Enum.GetValues<Part>())
            Refresh(loc, state, rows, part);

        var menu = kit.Finish(builder, adminPage: true);
        menu.Tag = directory.Stats;
        return menu;
    }

    private enum Part { Players, Groups, Scopes, Lengths }

    private ValueTask Pick(MenuActionContext ctx, GrantState state, Rows rows, Part part, Action select)
    {
        if (!kit.EnsureManage(ctx.Session)) return ValueTask.CompletedTask;

        select();
        Refresh(kit.Loc(ctx.Player), state, rows, part);
        ctx.Menu.InvalidateSessions();
        return ValueTask.CompletedTask;
    }

    private static void SelectPlayer(GrantState state, Candidate candidate)
    {
        state.Player = candidate.SteamId;
        state.PlayerName = candidate.Name;
        state.PlayerGroup = candidate.Group;
    }

    // Rewrites the picked set's rows, then the summary and Grant button that follow from every set.
    private static void Refresh(ILocalizer loc, GrantState state, Rows rows, Part part)
    {
        switch (part)
        {
            case Part.Players:
                foreach (var (row, player) in rows.Players)
                {
                    var chosen = state.Player == player.SteamId;
                    row.Tag = new VipPlayerRow(player.SteamId, player.Name, player.Team, player.Group, chosen);
                    row.Text = MenuKit.Mark(player.Name, chosen);
                }
                break;

            case Part.Groups:
                foreach (var (row, group) in rows.Groups)
                {
                    var chosen = group.Name.Equals(state.Group, StringComparison.OrdinalIgnoreCase);
                    row.Tag = new VipGroupRow(group, chosen);
                    row.Text = MenuKit.Mark(group.Name, chosen);
                }
                break;

            case Part.Scopes:
                foreach (var (row, global) in rows.Scopes)
                {
                    var chosen = state.Global == global;
                    row.Tag = new VipScopeRow(global, chosen);
                    row.Text = MenuKit.Mark(loc[global ? "manage.Scope.Global" : "manage.Scope.Local"], chosen);
                }
                break;

            case Part.Lengths:
                foreach (var (row, length, permanent) in rows.Lengths)
                {
                    var chosen = state.Length == length.Time;
                    row.Tag = new VipLengthRow(permanent ? null : length.Span, chosen);
                    row.Text = MenuKit.Mark(loc[length.LabelKey], chosen);
                }
                break;
        }

        rows.Summary.Text = Summary(loc, state, rows);
        rows.Summary.Tag = new VipActionRow(VipAction.Summary);

        var canSubmit = state.Player != null && state.Group != null;
        if (rows.Submit.Enabled != canSubmit)
        {
            rows.Submit.Enabled = canSubmit;
            rows.Submit.Tag = new VipActionRow(VipAction.Submit);
        }
    }

    private static string Summary(ILocalizer loc, GrantState state, Rows rows)
    {
        if (state.Player is not { } steamId || state.Group == null)
            return loc["manage.SummaryPickPlayer"];

        var name = rows.Players.FirstOrDefault(entry => entry.Player.SteamId == steamId).Player?.Name
            ?? state.PlayerName
            ?? steamId.ToString(CultureInfo.InvariantCulture);
        var length = rows.Lengths.FirstOrDefault(entry => entry.Length.Time == state.Length).Length;

        return loc["manage.Summary", name, state.Group, loc[state.Global ? "manage.Summary.Global" : "manage.Summary.Local"],
            loc[length.LabelKey ?? "manage.Permanent"]];
    }

    private void Search(MenuValueChangedContext<string> change, GrantState state)
    {
        if (!kit.EnsureManage(change.Session)) return;

        var admin = change.Player;
        var page = change.Session.Menu;
        state.Search = (change.NewValue ?? string.Empty).Trim();

        // The input is still finishing its chat capture; show the matches next tick.
        kit.Core.Scheduler.NextTick(() =>
        {
            if (kit.IsShowing(admin, page))
                kit.Replace(admin, Build(admin, state), page.Parent, SearchId);
        });
    }

    private ValueTask Submit(MenuActionContext ctx, GrantState state, Rows rows)
    {
        if (!kit.EnsureManage(ctx.Session) || state.Busy || state.Player is not { } steamId || state.Group is not { } group)
            return ValueTask.CompletedTask;
        state.Busy = true;

        var admin = ctx.Player;
        var page = ctx.Menu;
        var global = state.Global;
        var time = state.Length ?? 0;
        var lengthKey = rows.Lengths.FirstOrDefault(entry => entry.Length.Time == time).Length.LabelKey ?? "manage.Permanent";
        var target = kit.Core.PlayerManager.GetPlayerFromSteamId(steamId);
        var name = target is { IsValid: true } && !target.IsFakeClient
            ? target.Controller?.PlayerName ?? state.PlayerName ?? "unknown"
            : state.PlayerName ?? steamId.ToString(CultureInfo.InvariantCulture);
        // Completed on the game thread, so the button shows its waiting text until the page is redrawn.
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
                await vipService.ReloadPlayer(target);
                await directory.RefreshAsync(changed: true);
            }

            kit.Core.Scheduler.NextTick(() =>
            {
                try
                {
                    state.Busy = false;
                    if (!admin.IsValid) return;

                    var loc = kit.Loc(admin);
                    var granted = $"{group} ({loc[global ? "manage.Scope.Global" : "manage.Scope.Local"]})";
                    if (error != null)
                    {
                        kit.Core.Logger.LogError(error, "[VIPCore] Failed to add VIP user {SteamId}", steamId);
                        admin.SendMessage(MessageType.Chat, loc["manage.chat.FailedAddVip", error.Message]);
                    }
                    else if (time <= 0)
                    {
                        admin.SendMessage(MessageType.Chat, loc["manage.chat.AddedVipPermanent", name, steamId, granted]);
                    }
                    else
                    {
                        admin.SendMessage(MessageType.Chat, loc["manage.chat.AddedVip", name, steamId, granted, loc[lengthKey]]);
                    }

                    if (kit.IsShowing(admin, page))
                        kit.Replace(admin, Build(admin, state), page.Parent, "submit");
                }
                catch (Exception ex)
                {
                    kit.Core.Logger.LogError(ex, "[VIPCore] Failed to redraw the Grant VIP page.");
                }
                finally
                {
                    finished.TrySetResult();
                }
            });
        });

        return new ValueTask(finished.Task);
    }

    private List<Candidate> Candidates(GrantState state)
    {
        var search = state.Search;
        var candidates = new List<Candidate>();

        foreach (var player in kit.Core.PlayerManager.GetAllValidPlayers())
        {
            if (player.IsFakeClient || player.SteamID == 0) continue;

            var candidate = Describe(player);
            if (search.Length == 0
                || candidate.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
                || candidate.SteamId.ToString(CultureInfo.InvariantCulture).StartsWith(search, StringComparison.Ordinal))
            {
                candidates.Add(candidate);
            }
        }

        candidates.Sort((a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.Name, b.Name));

        // A full SteamID64 reaches a player who is not online.
        if (IsSteamId64(search, out var searched) && !candidates.Any(candidate => candidate.SteamId == searched))
            candidates.Add(new Candidate(searched, search, null, null));

        // The chosen player stays listed when the search hides them or they are not online.
        if (state.Player is { } chosen && !candidates.Any(candidate => candidate.SteamId == chosen))
        {
            var online = kit.Core.PlayerManager.GetPlayerFromSteamId(chosen);
            candidates.Insert(0, online is { IsValid: true } && !online.IsFakeClient
                ? Describe(online)
                : new Candidate(chosen, state.PlayerName ?? chosen.ToString(CultureInfo.InvariantCulture), null, state.PlayerGroup));
        }

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

    private static int DefaultLength(VipLengths.Length[] lengths)
    {
        foreach (var length in lengths)
        {
            if (length.LabelKey == VipLengths.MonthKey) return length.Time;
        }

        return lengths.Length > 0 ? lengths[0].Time : 0;
    }

    private static bool IsSteamId64(string text, out ulong steamId) =>
        ulong.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out steamId) && text.Length == 17 && steamId > SteamId64Base;
}

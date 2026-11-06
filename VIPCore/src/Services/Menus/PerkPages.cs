using SwiftlyS2.Shared.Menu;
using SwiftlyS2.Shared.Menu.Components;
using SwiftlyS2.Shared.Players;
using VIPCore.Contract;

namespace VIPCore.Services.Menus;

/// <summary>The player's pages: Perks, and Customize for the features that list their choices.</summary>
public class PerkPages(MenuKit kit, VipService vipService, FeatureViewService views, CookieService cookieService)
{
    public bool HasPerks(IPlayer player) => vipService.IsClientVip(player);

    public bool HasCustomize(IPlayer player) =>
        HasPerks(player) && views.PlayerFeatures(player).Any(feature => feature.Options != null);

    public IMenu? BuildPerks(IPlayer player)
    {
        if (!HasPerks(player)) return null;

        var builder = kit.Create(VipMenuIds.Perks, kit.Loc(player)["menu.Perks"]);
        var refreshers = new List<Action>();

        foreach (var feature in views.PlayerFeatures(player))
        {
            var view = views.View(player, feature);
            var id = $"{VipMenuIds.Perks}.{feature.Key}";

            if (feature.FeatureType == FeatureType.Toggle)
            {
                var toggle = new ToggleComponent(view.Name, id: id)
                {
                    Tag = new VipFeatureRow(feature.Key),
                    Comment = view.Description,
                    Enabled = !view.Locked,
                    DefaultValueProvider = _ => views.State(player, feature.Key) == FeatureState.Enabled
                };
                toggle.OnChanged = change => Toggle(change, feature, toggle);
                builder.Add(toggle);
                continue;
            }

            TextComponent row;
            if (feature.Options != null)
            {
                row = new SubmenuComponent(PerkText(player, feature, view), _ => BuildCustomize(player, feature.Key), id);
            }
            else
            {
                var button = new ButtonComponent(PerkText(player, feature, view), id: id);
                button.OnSelect = ctx => Select(ctx, feature, () => Refresh(player, feature, button));
                row = button;
            }

            row.Tag = new VipFeatureRow(feature.Key);
            row.Comment = view.Description;
            row.Enabled = !view.Locked;
            refreshers.Add(() => Refresh(player, feature, row));
            builder.Add(row);
        }

        var menu = kit.Finish(builder, adminPage: false);

        // Coming back from Customize: the rows show what was chosen there.
        var opened = false;
        menu.Opened += _ =>
        {
            if (!opened)
            {
                opened = true;
                return;
            }

            foreach (var refresh in refreshers) refresh();
        };

        return menu;
    }

    public IMenu? BuildCustomize(IPlayer player, string? focusKey = null)
    {
        if (!HasPerks(player)) return null;

        var builder = kit.Create(VipMenuIds.Customize, kit.Loc(player)["menu.Customize"]);
        string? focusId = null;

        foreach (var feature in views.PlayerFeatures(player).Where(feature => feature.Options != null))
        {
            var options = views.Options(player, feature);
            if (options.Count == 0) continue;

            var view = views.View(player, feature);
            var sectionId = $"{VipMenuIds.Customize}.{feature.Key}";
            builder.Add(new TextComponent(view.Name, sectionId) { Tag = new VipSectionRow(feature.Key) });

            var rows = new List<(ButtonComponent Row, VipFeatureOption Option)>();
            for (var i = 0; i < options.Count; i++)
            {
                var option = options[i];
                var row = new ButtonComponent(option.Label, id: $"{sectionId}.{i}")
                {
                    Comment = option.Description,
                    Enabled = !view.Locked
                };
                row.OnSelect = ctx => Choose(ctx, feature, option, rows);
                rows.Add((row, option));
                builder.Add(row);
            }

            var selected = views.Selected(player, feature, options);
            Mark(feature.Key, rows, selected);

            if (feature.Key == focusKey)
                focusId = rows.FirstOrDefault(entry => entry.Option == selected).Row?.Id ?? sectionId;
        }

        var menu = kit.Finish(builder, adminPage: false);
        if (focusId != null)
            menu.FocusOnFirstOpen(focusId);

        return menu;
    }

    private void Toggle(MenuValueChangedContext<bool> change, Feature feature, ToggleComponent toggle)
    {
        var player = change.Player;
        var state = change.NewValue ? FeatureState.Enabled : FeatureState.Disabled;

        GameThread.Post(kit.Core, () =>
        {
            var user = vipService.GetVipUser(player.SteamID);
            if (user == null) return;

            user.FeatureStates[feature.Key] = state;
            cookieService.SetCookie(player.SteamID, feature.Key, (int)state);
            feature.OnSelectItem?.Invoke(player, state);

            toggle.Tag = new VipFeatureRow(feature.Key);
            change.Session.Invalidate();
        });
    }

    private ValueTask Select(MenuActionContext ctx, Feature feature, Action refresh)
    {
        var player = ctx.Player;
        var menu = ctx.Menu;
        feature.OnSelectItem?.Invoke(player, views.State(player, feature.Key));

        // Modules without options apply the choice on their next tick; read it back after that.
        kit.Core.Scheduler.NextTick(() => kit.Core.Scheduler.NextTick(() =>
        {
            if (!player.IsValid) return;
            refresh();
            menu.InvalidateSessions();
        }));

        return ValueTask.CompletedTask;
    }

    private ValueTask Choose(MenuActionContext ctx, Feature feature, VipFeatureOption option, List<(ButtonComponent Row, VipFeatureOption Option)> rows)
    {
        var player = ctx.Player;
        var menu = ctx.Menu;
        if (!views.Choose(player, feature, option.Id)) return ValueTask.CompletedTask;

        Refresh();
        // Read back again next tick, for modules that apply a choice there.
        kit.Core.Scheduler.NextTick(Refresh);
        return ValueTask.CompletedTask;

        void Refresh()
        {
            if (!player.IsValid) return;
            Mark(feature.Key, rows, views.Selected(player, feature, rows.Select(entry => entry.Option).ToList()));
            menu.InvalidateSessions();
        }
    }

    private void Refresh(IPlayer player, Feature feature, TextComponent row)
    {
        var view = views.View(player, feature);
        row.Text = PerkText(player, feature, view);
        row.Comment = view.Description;
        row.Tag = new VipFeatureRow(feature.Key);
    }

    private string PerkText(IPlayer player, Feature feature, VipFeatureView view)
    {
        if (view.Value != null) return $"{view.Name} [{view.Value}]";

        return feature.Options == null && feature.FeatureType == FeatureType.Selectable
            ? $"{view.Name} [{kit.Loc(player)["menu.Off"]}]"
            : view.Name;
    }

    private static void Mark(string featureKey, List<(ButtonComponent Row, VipFeatureOption Option)> rows, VipFeatureOption? selected)
    {
        foreach (var (row, option) in rows)
        {
            var chosen = selected != null && option.Id == selected.Id;
            row.Tag = new VipOptionRow(featureKey, option, chosen);
            row.Text = MenuKit.Mark(option.Label, chosen);
        }
    }
}

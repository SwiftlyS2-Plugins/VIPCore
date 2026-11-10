using System.Collections.Concurrent;
using System.Globalization;
using SwiftlyS2.Shared.Menu;
using SwiftlyS2.Shared.Menu.Components;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.Translation;
using VIPCore.Contract;

namespace VIP_GunMenu;

public partial class VIP_GunMenu
{
    private const string PageId = "vipcore.gun-menu";
    private const string PrimaryGroup = "primary";
    private const string PistolGroup = "pistol";

    private readonly ConcurrentDictionary<ulong, Loadout> _lastLoadouts = new();
    private readonly ConcurrentDictionary<ulong, GunPage> _pages = new();

    private string? Describe(IPlayer player)
    {
        var config = ReadGroupConfig(player);
        if (config == null || config.MaxUsesPerPlayerPerRound <= 0) return null;

        var localizer = Core.Translation.GetPlayerLocalizer(player);
        var alias = _pluginConfig.CommandAliases.FirstOrDefault(a => !string.IsNullOrWhiteSpace(a)) ?? "gunmenu";
        return config.MaxUsesPerPlayerPerRound == 1
            ? localizer["vip.gun_menu.description.one", alias]
            : localizer["vip.gun_menu.description", config.MaxUsesPerPlayerPerRound.ToString(CultureInfo.InvariantCulture), alias];
    }

    private IMenu BuildPage(IPlayer player)
    {
        var localizer = Core.Translation.GetPlayerLocalizer(player);
        var config = ReadGroupConfig(player);
        var (primaries, pistols) = GunsOnOffer(config);

        var summary = new TextComponent
        {
            Tag = new VipActionRow(VipAction.Summary),
            Comment = localizer[config?.ReplaceCurrentWeapons == false ? "vip.gun_menu.adds" : "vip.gun_menu.replaces"],
        };
        var give = new ButtonComponent(localizer["vip.gun_menu.give"])
        {
            Tag = new VipActionRow(VipAction.Submit),
            CloseOnSelect = true,
        };
        var page = new GunPage(localizer, summary, give);
        give.OnSelect = ctx =>
        {
            GiveLoadout(page, ctx.Player);
            return ValueTask.CompletedTask;
        };
        give.EnabledWhen = session => CanGive(page, session.Player);

        var builder = Core.Menu.CreateMenu(PageId)
            .AddHeader(new TitleComponent(localizer["vip.gun_menu.title"]) { Overflow = MenuTextOverflow.None });
        AddGuns(builder, page, PrimaryGroup, localizer["vip.gun_menu.primary"], primaries);
        AddGuns(builder, page, PistolGroup, localizer["vip.gun_menu.pistol"], pistols);

        if (_lastLoadouts.TryGetValue(player.SteamID, out var last))
        {
            foreach (var row in page.Rows)
                row.SetSelected(row.Gun.WeaponName == (row.Group == PrimaryGroup ? last.Primary : last.Pistol));
        }

        summary.Text = SummaryText(page);
        builder.Add(summary);
        builder.Add(give);
        builder.AddFooter(new CountdownComponent(GetWindowEnd, localizer["vip.gun_menu.closes_in"], localizer["vip.gun_menu.closed"]));

        var menu = builder.Build();
        menu.Tag = GetLimit(player, config);
        page.Menu = menu;
        _pages[player.SteamID] = page;
        return menu;
    }

    private (List<Gun> Primaries, List<Gun> Pistols) GunsOnOffer(GroupConfig? config)
    {
        if (config == null) return ([], []);

        List<Gun> primaries = !IsPistolRound() || config.GivePrimariesOnPistolRound
            ? Offered(config.AvailablePrimaryGuns, GroupConfig.DefaultPrimaryGuns)
            : [];
        // Without GiveSecondariesAfterPrimaries the chained menus offered pistols only when there were no primaries.
        List<Gun> pistols = config.GiveSecondariesAfterPrimaries || primaries.Count == 0
            ? Offered(config.AvailableSecondaryGuns, GroupConfig.DefaultSecondaryGuns)
            : [];
        return (primaries, pistols);
    }

    private static List<Gun> Offered(List<Gun>? configured, List<Gun> defaults) =>
        (configured is { Count: > 0 } ? configured : defaults).DistinctBy(g => g.WeaponName).ToList();

    private void AddGuns(IMenuBuilder builder, GunPage page, string group, string heading, List<Gun> guns)
    {
        if (guns.Count == 0) return;

        builder.Add(new TextComponent(heading) { Tag = new VipSectionRow(FeatureKey, group) });
        foreach (var gun in guns)
        {
            var row = new GunRow(gun, group);
            row.Button.OnSelect = ctx =>
            {
                Pick(page, row, ctx.Session);
                return ValueTask.CompletedTask;
            };
            page.Rows.Add(row);
            builder.Add(row.Button);
        }
    }

    // Picks a gun for its group, or clears the group when that gun was the pick.
    private void Pick(GunPage page, GunRow picked, IMenuSession session)
    {
        var clear = picked.Selected;
        foreach (var row in page.Rows.Where(r => r.Group == picked.Group))
            row.SetSelected(!clear && row == picked);

        // Fresh records tell renderers that draw from Tags that these rows changed too.
        page.Summary.Text = SummaryText(page);
        page.Summary.Tag = new VipActionRow(VipAction.Summary);
        page.Give.Tag = new VipActionRow(VipAction.Submit);
        session.Invalidate();
    }

    private static string SummaryText(GunPage page)
    {
        var picks = page.Rows.Where(r => r.Selected).Select(r => r.Gun.DisplayName).ToList();
        return picks.Count == 0
            ? page.Localizer["vip.gun_menu.loadout.empty"]
            : page.Localizer["vip.gun_menu.loadout", string.Join(" + ", picks)];
    }

    private bool CanGive(GunPage page, IPlayer player)
    {
        if (_vipApi == null || !page.Rows.Any(r => r.Selected) || !IsCommandWindowOpen()) return false;
        if (!player.IsValid || !player.IsAlive || _gameRulesProxy?.GameRules?.WarmupPeriod == true) return false;
        if (!_vipApi.IsClientVip(player) || _vipApi.GetPlayerFeatureState(player, FeatureKey) != FeatureState.Enabled) return false;

        var config = ReadGroupConfig(player);
        return config != null && UsesLeft(player, config) > 0;
    }

    private void GiveLoadout(GunPage page, IPlayer player)
    {
        var config = ReadGroupConfig(player);
        if (config == null || !CanGive(page, player)) return;

        _gunMenuUsed.AddOrUpdate(player.SteamID, 1, (_, used) => used + 1);
        foreach (var row in page.Rows.Where(r => r.Selected))
            GivePlayerWeapon(player, row.Gun, config.ReplaceCurrentWeapons);

        _lastLoadouts[player.SteamID] = new Loadout(page.Picked(PrimaryGroup), page.Picked(PistolGroup));
    }

    private VipFeatureLimit GetLimit(IPlayer player, GroupConfig? config) =>
        new(config == null ? 0 : UsesLeft(player, config), config?.MaxUsesPerPlayerPerRound ?? 0, GetWindowEnd());

    private int UsesLeft(IPlayer player, GroupConfig config) =>
        Math.Max(0, config.MaxUsesPerPlayerPerRound - (_gunMenuUsed.TryGetValue(player.SteamID, out var used) ? used : 0));

    private GroupConfig? ReadGroupConfig(IPlayer player)
    {
        try
        {
            return _vipApi?.GetFeatureValue<GroupConfig>(player, FeatureKey);
        }
        catch
        {
            return null;
        }
    }

    // A page left open into a new round takes that round's uses and window, and is rebuilt when its guns change.
    private void RefreshPages()
    {
        List<IPlayer>? rebuild = null;
        foreach (var (steamId, page) in _pages)
        {
            if (page.Menu is not { Sessions.Count: > 0 } menu)
            {
                _pages.TryRemove(steamId, out _);
                continue;
            }

            var session = menu.Sessions[0];
            var config = ReadGroupConfig(session.Player);
            var (primaries, pistols) = GunsOnOffer(config);
            if (!page.Offers(PrimaryGroup, primaries) || !page.Offers(PistolGroup, pistols))
            {
                (rebuild ??= []).Add(session.Player);
                continue;
            }

            menu.Tag = GetLimit(session.Player, config);
            session.Invalidate();
        }

        if (rebuild == null) return;
        foreach (var player in rebuild)
            _vipApi?.OpenMenu(player, PageId);
    }

    private sealed record Loadout(string? Primary, string? Pistol);

    private sealed class GunPage(ILocalizer localizer, TextComponent summary, ButtonComponent give)
    {
        public ILocalizer Localizer { get; } = localizer;
        public TextComponent Summary { get; } = summary;
        public ButtonComponent Give { get; } = give;
        public List<GunRow> Rows { get; } = [];
        public IMenu? Menu { get; set; }

        public string? Picked(string group) => Rows.FirstOrDefault(r => r.Group == group && r.Selected)?.Gun.WeaponName;

        public bool Offers(string group, List<Gun> guns) =>
            Rows.Where(r => r.Group == group).Select(r => r.Gun.WeaponName).SequenceEqual(guns.Select(g => g.WeaponName));
    }

    private sealed class GunRow
    {
        public GunRow(Gun gun, string group)
        {
            Gun = gun;
            Group = group;
            Option = new VipFeatureOption(gun.WeaponName, gun.DisplayName, group, gun.Category);
            Button = new ButtonComponent(gun.DisplayName) { Tag = new VipOptionRow(FeatureKey, Option, false) };
        }

        public Gun Gun { get; }
        public string Group { get; }
        public VipFeatureOption Option { get; }
        public ButtonComponent Button { get; }
        public bool Selected { get; private set; }

        public void SetSelected(bool selected)
        {
            if (selected == Selected) return;

            Selected = selected;
            Button.Tag = new VipOptionRow(FeatureKey, Option, selected);
            Button.Text = selected ? Gun.DisplayName + " *" : Gun.DisplayName;
        }
    }

    // Counts down to the end of the command window, asking for a frame whenever the shown second changes.
    private sealed class CountdownComponent : TextComponent
    {
        private readonly Func<DateTimeOffset?> _until;

        public CountdownComponent(Func<DateTimeOffset?> until, string closesIn, string closed)
        {
            _until = until;
            TextProvider = _ =>
            {
                var left = SecondsLeft(DateTime.UtcNow);
                return left > 0
                    ? string.Format(CultureInfo.InvariantCulture, closesIn, string.Create(CultureInfo.InvariantCulture, $"{left / 60}:{left % 60:00}"))
                    : closed;
            };
        }

        public override bool IsVisible(IMenuSession session) => base.IsVisible(session) && _until() is not null;

        public override bool NeedsRedraw(IMenuSession session, DateTime now)
        {
            var shown = session.GetState<ShownSeconds>(this);
            var left = SecondsLeft(now);
            if (left == shown.Value) return base.NeedsRedraw(session, now);

            shown.Value = left;
            return true;
        }

        private int SecondsLeft(DateTime now) =>
            _until() is { } until ? Math.Max(0, (int)Math.Ceiling((until.UtcDateTime - now).TotalSeconds)) : 0;

        private sealed class ShownSeconds
        {
            public int Value { get; set; }
        }
    }
}

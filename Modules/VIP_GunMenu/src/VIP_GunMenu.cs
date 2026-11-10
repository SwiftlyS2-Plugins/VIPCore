using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Commands;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.Plugins;
using SwiftlyS2.Shared.SchemaDefinitions;
using System.Collections.Concurrent;
using VIPCore.Contract;

namespace VIP_GunMenu;

[PluginMetadata(Id = "VIP_GunMenu", Version = "1.1.1", Name = "VIP_GunMenu", Author = "shmitz", Description = "Allows players to equip a weapon for free")]
public partial class VIP_GunMenu : BasePlugin
{
    private const string FeatureKey = "vip.gun_menu";

    private IVipCoreApiV1? _vipApi;
    private bool _isFeatureRegistered;

    private CCSGameRulesProxy? _gameRulesProxy;
    private int _maxRounds = 30;
    private ConcurrentDictionary<ulong, int> _gunMenuUsed = new();
    private PluginConfig _pluginConfig = new();
    private bool _commandEnabled = false;
    private int _roundId = 0;

    public VIP_GunMenu(ISwiftlyCore core) : base(core)
    {
    }

    public override void ConfigureSharedInterface(IInterfaceManager interfaceManager)
    {
    }

    public override void UseSharedInterface(IInterfaceManager interfaceManager)
    {
        _vipApi = null;
        _isFeatureRegistered = false;

        try
        {
            if (interfaceManager.HasSharedInterface("VIPCore.Api.v1"))
                _vipApi = interfaceManager.GetSharedInterface<IVipCoreApiV1>("VIPCore.Api.v1");

            RegisterVipFeaturesWhenReady();
        }
        catch (Exception ex)
        {
            Console.WriteLine("VIP_GunMenu: VIPCore API was not loaded due to error.\n {Error}", ex.ToString());
        }
    }

    public override void Load(bool hotReload)
    {
        Core.Event.OnMapLoad += _ =>
        {
            _lastLoadouts.Clear();
            _pages.Clear();
            Core.Scheduler.DelayBySeconds(1.0f, () => RefreshGameRulesAndMaxRounds());
        };

        if (hotReload)
        {
            RefreshGameRulesAndMaxRounds();
        }

        LoadPluginConfig();
        RegisterAliases();
        RegisterVipFeaturesWhenReady();
    }

    private void LoadPluginConfig()
    {
        Core.Configuration
        .InitializeJsonWithModel<PluginConfig>("config.jsonc", "GunMenu")
        .Configure(builder =>
        {
            var configPath = Core.Configuration.GetConfigPath("config.jsonc");
            builder.AddJsonFile(configPath, optional: false, reloadOnChange: true);
        });

        _pluginConfig = ReadPluginConfig(Core.Configuration.Manager.GetSection("GunMenu"));
    }

    // Bound onto an empty list, since the binder appends to a non-empty default.
    private static PluginConfig ReadPluginConfig(IConfigurationSection section)
    {
        var config = new PluginConfig { CommandAliases = [] };
        section.Bind(config);
        if (!section.GetSection(nameof(PluginConfig.CommandAliases)).Exists())
            config.CommandAliases = [.. PluginConfig.DefaultCommandAliases];

        return config;
    }

    private void RegisterAliases()
    {
        foreach (var alias in _pluginConfig.CommandAliases)
        {
            Core.Command.RegisterCommandAlias("gunmenu", alias);
        }
    }

    private void RefreshGameRulesAndMaxRounds()
    {
        _gameRulesProxy = Core.EntitySystem.GetAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").FirstOrDefault();

        var maxRoundsCvar = Core.ConVar.Find<int>("mp_maxrounds");
        if (maxRoundsCvar != null && maxRoundsCvar.Value > 0)
            _maxRounds = maxRoundsCvar.Value;
    }

    private void RegisterVipFeaturesWhenReady()
    {
        if (_vipApi == null) return;

        if (_vipApi.IsCoreReady())
            RegisterVipFeatures();
        else
            _vipApi.OnCoreReady += RegisterVipFeatures;
    }

    private void RegisterVipFeatures()
    {
        if (_vipApi == null || _isFeatureRegistered) return;

        _vipApi.RegisterFeature(
            FeatureKey,
            FeatureType.Toggle,
            null,
            displayNameResolver: p => Core.Translation.GetPlayerLocalizer(p)["vip.gun_menu"]
        );
        _vipApi.DescribeFeature(FeatureKey, Describe);
        _vipApi.RegisterMenuPage(new VipMenuPage(
            PageId,
            10,
            p => Core.Translation.GetPlayerLocalizer(p)["vip.gun_menu.title"],
            IsAvailable: p => _vipApi is { } api && api.IsClientVip(p) && api.GetPlayerFeatureState(p, FeatureKey) != FeatureState.NoAccess,
            Build: p => BuildPage(p)));

        _isFeatureRegistered = true;
    }

    [Command("gunmenu")]
    public void OnGunMenuCommand(ICommandContext context)
    {
        if (_vipApi == null) return;
        var player = context.Sender;
        if (!context.IsSentByPlayer)
        {
            context.Reply(Core.Localizer["vip.gun_menu.error.command_only_for_players"]);
            return;
        }

        if (player == null || player.PlayerPawn == null) return;

        if (_gameRulesProxy?.GameRules?.WarmupPeriod == true) return;

        if (!_vipApi.IsClientVip(player)) return;
        if (_vipApi.GetPlayerFeatureState(player, FeatureKey) != FeatureState.Enabled)
            return;

        var config = _vipApi.GetFeatureValue<GroupConfig>(player, FeatureKey);
        if (config == null) return;

        var previousUses = _gunMenuUsed.GetOrAdd(player.SteamID, 0);
        if (previousUses >= config.MaxUsesPerPlayerPerRound || !_commandEnabled)
        {
            player.SendChat(Core.Localizer["vip.gun_menu.error.limit_exceeded"]);
            return;
        }

        _vipApi.OpenMenu(player, PageId);
    }

    private bool IsPistolRound()
    {
        if (_gameRulesProxy == null || _gameRulesProxy.GameRules == null) return false;
        if (_gameRulesProxy.GameRules.WarmupPeriod) return false;

        var totalRounds = _gameRulesProxy.GameRules.TotalRoundsPlayed;

        if (totalRounds == 0) return true;

        var maxRounds = _maxRounds;
        var cvarMaxRounds = Core.ConVar.Find<int>("mp_maxrounds");
        if (cvarMaxRounds != null && cvarMaxRounds.Value > 0)
            maxRounds = cvarMaxRounds.Value;

        var half = maxRounds / 2;
        if (half <= 0) return false;

        // Second half start
        if (totalRounds == half) return true;

        // Overtime: detect each OT half-switch
        if (totalRounds >= maxRounds)
        {
            var overtimeMaxRounds = 6;
            var cvarOtMaxRounds = Core.ConVar.Find<int>("mp_overtime_maxrounds");
            if (cvarOtMaxRounds != null && cvarOtMaxRounds.Value > 0)
                overtimeMaxRounds = cvarOtMaxRounds.Value;

            var otHalf = overtimeMaxRounds / 2;
            if (otHalf <= 0) return false;

            var roundsIntoOvertime = totalRounds - maxRounds;
            return (roundsIntoOvertime % otHalf) == 0;
        }

        return false;
    }

    private void GivePlayerWeapon(IPlayer? player, Gun gun, bool replaceCurrentWeapon)
    {
        if (player == null
            || player.PlayerPawn == null
            || player.PlayerPawn.ItemServices == null
            || player.PlayerPawn.WeaponServices == null) return;

        Core.Scheduler.NextTick(() =>
        {
            if (replaceCurrentWeapon)
            {
                var slot = GetWeaponSlot(gun.Category);
                player.PlayerPawn.WeaponServices.RemoveWeaponBySlot(slot);
            }

            Core.Scheduler.DelayBySeconds(0.1f, () =>
            {
                player.PlayerPawn.ItemServices.GiveItemAsync(gun.WeaponName);
            });
        });
    }

    private gear_slot_t GetWeaponSlot(string category)
    {
        return category switch
        {
            "Rifle" => gear_slot_t.GEAR_SLOT_RIFLE,
            "Sniper" => gear_slot_t.GEAR_SLOT_RIFLE,
            "SMG" => gear_slot_t.GEAR_SLOT_RIFLE,
            "Heavy" => gear_slot_t.GEAR_SLOT_RIFLE,
            "Shotgun" => gear_slot_t.GEAR_SLOT_RIFLE,
            "Pistol" => gear_slot_t.GEAR_SLOT_PISTOL,
            _ => gear_slot_t.GEAR_SLOT_INVALID
        };
    }

    public override void Unload()
    {
        if (_vipApi != null)
        {
            _vipApi.OnCoreReady -= RegisterVipFeatures;
            if (_isFeatureRegistered)
            {
                _vipApi.UnregisterMenuPage(PageId);
                _vipApi.UnregisterFeature(FeatureKey);
            }
        }
    }
}

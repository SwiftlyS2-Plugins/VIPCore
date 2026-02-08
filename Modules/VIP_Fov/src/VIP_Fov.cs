using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using SwiftlyS2.Core.Menus.OptionsBase;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Menus;
using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.Plugins;
using SwiftlyS2.Shared.SchemaDefinitions;
using VIPCore.Contract;

namespace VIP_Fov;

[PluginMetadata(Id = "VIP_Fov", Version = "1.0.0", Name = "VIP_Fov", Author = "aga", Description = "Allows VIPs to change their FOV.")]
public partial class VIP_Fov : BasePlugin {
    private const uint DefaultFov = 90;
    private const string FeatureKey = "vip.fov";
    private static readonly string FeatureValueCookieKey = FeatureKey + ".value";

    private static readonly uint[] AllowedFovs = { 90, 100, 110, 120 };

    private IVipCoreApiV1? _vipApi;
    private bool _isFeatureRegistered;
    private readonly uint[] _fovSettings = new uint[65];

    public VIP_Fov(ISwiftlyCore core) : base(core)
    {
    }

    public override void ConfigureSharedInterface(IInterfaceManager interfaceManager) {
    }

    public override void UseSharedInterface(IInterfaceManager interfaceManager) {
        _vipApi = null;

        if (interfaceManager.HasSharedInterface("VIPCore.Api.v1"))
            _vipApi = interfaceManager.GetSharedInterface<IVipCoreApiV1>("VIPCore.Api.v1");

        RegisterVipFeaturesWhenReady();
    }

    public override void Load(bool hotReload) {
        for (var i = 0; i < _fovSettings.Length; i++)
            _fovSettings[i] = DefaultFov;

        Core.Event.OnClientConnected += OnClientConnected;
        Core.Event.OnClientDisconnected += OnClientDisconnected;

        RegisterVipFeaturesWhenReady();
    }

    public override void Unload() {
        Core.Event.OnClientConnected -= OnClientConnected;
        Core.Event.OnClientDisconnected -= OnClientDisconnected;

        if (_vipApi != null)
        {
            if (_isFeatureRegistered)
                _vipApi.UnregisterFeature(FeatureKey);
                
            _vipApi.OnPlayerSpawn -= OnPlayerSpawn;
            _vipApi.PlayerLoaded -= OnPlayerLoaded;
        }
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

        _vipApi.RegisterFeature(FeatureKey, FeatureType.Selectable, (player, _) =>
        {
            Core.Scheduler.NextTick(() => CycleFov(player));
        },
        displayNameResolver: p => Core.Translation.GetPlayerLocalizer(p)["vip.fov"]);
        
        _isFeatureRegistered = true;
        _vipApi.OnPlayerSpawn += OnPlayerSpawn;
        _vipApi.PlayerLoaded += OnPlayerLoaded;
    }

    private void OnPlayerLoaded(IPlayer player, string group)
    {
        if (_vipApi == null) return;
        if (!player.IsValid || player.IsFakeClient) return;
        if (!_vipApi.PlayerHasFeature(player, FeatureKey))
            return;

        LoadFovFromCookie(player);
        ApplyFov(player);
    }

    private void CycleFov(IPlayer player)
    {
        if (_vipApi == null) return;
        if (!player.IsValid) return;

        var localizer = Core.Translation.GetPlayerLocalizer(player);

        var current = _fovSettings[player.Slot];

        var cycle = AllowedFovs.Where(f => f > DefaultFov).ToArray();
        if (cycle.Length == 0) return;

        uint nextValue;
        if (current == DefaultFov)
        {
            nextValue = cycle[0];
        }
        else
        {
            var idx = Array.IndexOf(cycle, current);
            if (idx < 0)
            {
                nextValue = cycle[0];
            }
            else if (idx >= cycle.Length - 1)
            {
                nextValue = DefaultFov;
            }
            else
            {
                nextValue = cycle[idx + 1];
            }
        }

        if (nextValue == DefaultFov)
        {
            SetFov(player, DefaultFov, save: true);
            player.SendMessage(MessageType.Chat, localizer["fov.Off"]);
        }
        else
        {
            SetFov(player, nextValue, save: true);
            player.SendMessage(MessageType.Chat, localizer["fov.On", nextValue]);
        }
    }

    private void OpenFovMenu(IPlayer player)
    {
        var localizer = Core.Translation.GetPlayerLocalizer(player);
        var builder = Core.MenusAPI.CreateBuilder();
        builder.Design.SetMenuTitle(localizer["fov.MenuTitle"]);

        foreach (var fov in AllowedFovs)
        {
            var isCurrent = _fovSettings[player.Slot] == fov;
            var option = new ButtonMenuOption(localizer["fov.Option", fov] + (isCurrent ? " *" : ""));
            option.Click += (sender, args) =>
            {
                Core.Scheduler.NextTick(() =>
                {
                    SetFov(player, fov, save: true);
                    player.SendMessage(MessageType.Chat, localizer["fov.On", fov]);
                    // Re-open menu to show current selection
                    OpenFovMenu(player);
                });
                return ValueTask.CompletedTask;
            };
            builder.AddOption(option);
        }

        var disableOption = new ButtonMenuOption(localizer["fov.Disable"]);
        disableOption.Click += (sender, args) =>
        {
            Core.Scheduler.NextTick(() =>
            {
                SetFov(player, DefaultFov, save: true);
                player.SendMessage(MessageType.Chat, localizer["fov.Off"]);
                OpenFovMenu(player);
            });
            return ValueTask.CompletedTask;
        };
        builder.AddOption(disableOption);

        Core.MenusAPI.OpenMenuForPlayer(player, builder.Build());
    }

    private void OnClientConnected(SwiftlyS2.Shared.Events.IOnClientConnectedEvent @event)
    {
        if (@event.PlayerId < 0 || @event.PlayerId >= _fovSettings.Length) return;
        _fovSettings[@event.PlayerId] = DefaultFov;
    }

    private void OnClientDisconnected(SwiftlyS2.Shared.Events.IOnClientDisconnectedEvent @event)
    {
        if (@event.PlayerId < 0 || @event.PlayerId >= _fovSettings.Length) return;
        _fovSettings[@event.PlayerId] = DefaultFov;
    }

    private void OnPlayerSpawn(IPlayer player)
    {
        if (_vipApi == null) return;
        if (!player.IsValid || player.IsFakeClient) return;

        LoadFovFromCookie(player);
        ApplyFov(player);
    }

    private void LoadFovFromCookie(IPlayer player)
    {
        if (_vipApi == null) return;
        if (!player.IsValid) return;
        if (player.Slot < 0 || player.Slot >= _fovSettings.Length) return;

        int raw = 0;
        try
        {
            raw = _vipApi.GetPlayerCookie<int>(player, FeatureValueCookieKey);
        }
        catch
        {
            raw = 0;
        }

        if (raw <= 0)
        {
            _fovSettings[player.Slot] = DefaultFov;
            return;
        }

        var value = (uint)raw;
        var finalFov = AllowedFovs.Contains(value) ? value : DefaultFov;
        _fovSettings[player.Slot] = finalFov;
    }

    private void SetFov(IPlayer player, uint fov, bool save)
    {
        if (!player.IsValid) return;
        if (player.Slot < 0 || player.Slot >= _fovSettings.Length) return;

        _fovSettings[player.Slot] = fov;
        ApplyFov(player);

        if (!save) return;

        if (_vipApi == null) return;
        var storeValue = fov == DefaultFov ? 0 : (int)fov;
        _vipApi.SetPlayerCookie(player, FeatureValueCookieKey, storeValue);
    }

    private void ApplyFov(IPlayer player)
    {
        var controller = player.Controller as CCSPlayerController;
        if (controller == null || !controller.IsValid)
            return;

        uint fov = _fovSettings[player.Slot];
        controller.DesiredFOV = fov;
        controller.DesiredFOVUpdated();
    }
}

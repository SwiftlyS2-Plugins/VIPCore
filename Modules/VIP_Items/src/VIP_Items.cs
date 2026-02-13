using System;
using SwiftlyS2.Shared.Plugins;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.GameEventDefinitions;
using SwiftlyS2.Shared.Misc;
using VIPCore.Contract;
using SwiftlyS2.Shared.GameEvents;
using SwiftlyS2.Shared.Players;

namespace VIP_Items;

[PluginMetadata(Id = "VIP_Items", Version = "1.0.0", Name = "VIP_Items", Author = "shmitz", Description = "Gives items when player spawns")]
public partial class VIP_Items : BasePlugin
{
    private const string FeatureKey = "vip.items";

    private IVipCoreApiV1? _vipApi;
    private bool _isFeatureRegistered;

    public VIP_Items(ISwiftlyCore core) : base(core)
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
        catch
        {
        }
    }

    public override void Load(bool hotReload)
    {
        RegisterVipFeaturesWhenReady();
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
            displayNameResolver: p => Core.Translation.GetPlayerLocalizer(p)["vip.items"]
        );

        _isFeatureRegistered = true;
    }

    [GameEventHandler(HookMode.Post)]
    public HookResult OnPlayerSpawn(EventPlayerSpawn @event)
    {
        if (_vipApi == null) return HookResult.Continue;
        var player = @event.UserIdPlayer;
        if (player == null || player.PlayerPawn == null) return HookResult.Continue;

        if (!_vipApi.IsClientVip(player)) return HookResult.Continue;
        if (_vipApi.GetPlayerFeatureState(player, FeatureKey) != FeatureState.Enabled) return HookResult.Continue;

        var config = _vipApi.GetFeatureValue<ItemsConfig>(player, FeatureKey);
        if (config == null) return HookResult.Continue;

        List<string> weaponsList = [];

        if (player.PlayerPawn.Team == Team.CT)
            weaponsList = config.CT;
        else if (player.PlayerPawn.Team == Team.T)
            weaponsList = config.T;

        if (weaponsList.Count == 0) return HookResult.Continue;

        GivePlayerWeapons(player, weaponsList);
        return HookResult.Continue;
    }

    private void GivePlayerWeapons(IPlayer? player, List<string> weaponsList)
    {
        if (player == null
            || player.PlayerPawn == null
            || player.PlayerPawn.ItemServices == null
            || player.PlayerPawn.WeaponServices == null) return;

        if (weaponsList.Count == 0) return;

        foreach (var weapon in weaponsList)
        {
            var userWeapons = player.PlayerPawn.WeaponServices.MyValidWeapons.ToList();
            foreach(var item in userWeapons)
            {
                if (item.DesignerName == weapon)
                {
                    return;
                }
            }
            player.PlayerPawn.ItemServices.GiveItem(weapon);
        }
    }

    public override void Unload()
    {
        if (_vipApi != null)
        {
            _vipApi.OnCoreReady -= RegisterVipFeatures;
            if (_isFeatureRegistered)
                _vipApi.UnregisterFeature(FeatureKey);
        }
    }
}

public class ItemsConfig
{
    public List<string> CT { get; set; } = new List<string>();
    public List<string> T { get; set; } = new List<string>();
}
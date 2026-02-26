using Microsoft.Extensions.DependencyInjection;
using SwiftlyS2.Shared.Plugins;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.GameEventDefinitions;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.SchemaDefinitions;
using SwiftlyS2.Shared.Misc;
using VIPCore.Contract;

namespace VIP_Money;

[PluginMetadata(Id = "VIP_Money", Version = "1.0.0", Name = "VIP_Money", Author = "aga", Description = "Gives money to VIP players")]
public partial class VIP_Money : BasePlugin {
  private const string FeatureKey = "vip.money";
  
  private IVipCoreApiV1? _vipApi;
  private bool _isFeatureRegistered;
  private CCSGameRulesProxy? _gameRulesProxy;
  private int _maxRounds = 30;

  public VIP_Money(ISwiftlyCore core) : base(core)
  {
  }

  public override void ConfigureSharedInterface(IInterfaceManager interfaceManager) {
  }

  public override void UseSharedInterface(IInterfaceManager interfaceManager) {
    _vipApi = null;
    _isFeatureRegistered = false;

    try
    {
      if (!interfaceManager.HasSharedInterface("VIPCore.Api.v1"))
        return;

      _vipApi = interfaceManager.GetSharedInterface<IVipCoreApiV1>("VIPCore.Api.v1");

      RegisterVipFeaturesWhenReady();
    }
    catch
    {
    }
  }

  private void RefreshGameRulesAndMaxRounds()
  {
    _gameRulesProxy = Core.EntitySystem.GetAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").FirstOrDefault();

    var maxRoundsCvar = Core.ConVar.Find<int>("mp_maxrounds");
    if (maxRoundsCvar != null && maxRoundsCvar.Value > 0)
      _maxRounds = maxRoundsCvar.Value;
  }

  public override void Load(bool hotReload) {
    Core.Event.OnMapLoad += _ =>
    {
      Core.Scheduler.DelayBySeconds(1.0f, () => RefreshGameRulesAndMaxRounds());
    };

    if (hotReload)
    {
      RefreshGameRulesAndMaxRounds();
    }

    Core.GameEvent.HookPost<EventPlayerSpawn>(OnPlayerSpawn);
    RegisterVipFeaturesWhenReady();
  }

  public override void Unload() {
    if (_vipApi != null)
    {
      _vipApi.OnCoreReady -= RegisterVipFeatures;
      _vipApi.OnPlayerSpawn -= OnVipPlayerSpawn;

      if (_isFeatureRegistered)
        _vipApi.UnregisterFeature(FeatureKey);
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

    _vipApi.RegisterFeature(
      FeatureKey,
      FeatureType.Toggle,
      null,
      displayNameResolver: p => Core.Translation.GetPlayerLocalizer(p)["vip.money"]
    );

    _isFeatureRegistered = true;
    _vipApi.OnPlayerSpawn += OnVipPlayerSpawn;
  }

  private void OnVipPlayerSpawn(IPlayer player)
  {
    TryApplyMoney(player);
  }

  private HookResult OnPlayerSpawn(EventPlayerSpawn @event)
  {
    var player = @event.UserIdPlayer;
    if (player == null) return HookResult.Continue;

    TryApplyMoney(player);
    return HookResult.Continue;
  }

  private void TryApplyMoney(IPlayer player)
  {
    if (_vipApi == null) return;
    if (player.IsFakeClient || !player.IsValid) return;
    if (!_vipApi.IsClientVip(player)) return;
    if (_vipApi.GetPlayerFeatureState(player, FeatureKey) != FeatureState.Enabled) return;

    if (_gameRulesProxy != null && _gameRulesProxy.GameRules != null && !_gameRulesProxy.GameRules.WarmupPeriod)
    {
      var gameRules = _gameRulesProxy.GameRules;
      var totalRounds = gameRules.TotalRoundsPlayed;

      var maxRounds = _maxRounds;
      var cvarMaxRounds = Core.ConVar.Find<int>("mp_maxrounds");
      if (cvarMaxRounds != null && cvarMaxRounds.Value > 0)
        maxRounds = cvarMaxRounds.Value;

      var half = maxRounds / 2;

      var isPistolRound = totalRounds == 0 || (half > 0 && totalRounds > 0 && (totalRounds % half) == 0);

      if (isPistolRound)
        return;
    }

    var moneyValue = "";
    try
    {
      var config = _vipApi.GetFeatureValue<MoneyConfig>(player, FeatureKey);
      moneyValue = config?.Money ?? "";
    }
    catch
    {
      moneyValue = "";
    }

    if (string.IsNullOrWhiteSpace(moneyValue)) return;

    Core.Scheduler.NextTick(() =>
    {
      var controller = player.Controller as CCSPlayerController;
      if (controller == null || !controller.IsValid) return;

      var moneyServices = controller.InGameMoneyServices;
      if (moneyServices == null) return;

      int maxMoney = 16000;
      var mpMaxMoneyCvar = Core.ConVar.Find<int>("mp_maxmoney");
      if (mpMaxMoneyCvar != null)
      {
        maxMoney = mpMaxMoneyCvar.Value;
      }

      if (moneyValue.Contains("++"))
      {
          var parts = moneyValue.Split("++");
          if (parts.Length > 1 && int.TryParse(parts[1], out int moneyToAdd))
          {
              if (moneyServices.Account + moneyToAdd > maxMoney)
                  moneyServices.Account = maxMoney;
              else
                  moneyServices.Account += moneyToAdd;
          }
      }
      else
      {
          if (int.TryParse(moneyValue, out int moneySet))
          {
              moneyServices.Account = moneySet > maxMoney ? maxMoney : moneySet;
          }
      }

      // We do not have Utilities.SetStateChanged in SwiftlyS2, updating the property should be enough or handled by the engine natively.
    });
  }
}

public class MoneyConfig
{
  public string Money { get; set; } = "";
}
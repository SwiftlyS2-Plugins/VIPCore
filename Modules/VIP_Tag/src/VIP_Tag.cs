using Microsoft.Extensions.Logging;
using SwiftlyS2.Core.Menus.OptionsBase;
using SwiftlyS2.Shared.Plugins;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.SchemaDefinitions;
using VIPCore.Contract;

namespace VIP_Tag;

[PluginMetadata(Id = "VIP_Tag", Version = "1.0.0", Name = "VIP_Tag", Author = "aga", Description = "No description.")]
public partial class VIP_Tag : BasePlugin {
  private const string FeatureKey = "vip.tag";
  private const string FeatureValueCookieKey = FeatureKey + ".value";

  private const int ApplyMaxAttempts = 5;
  private const float ApplyRetryDelaySeconds = 0.1f;

  private IVipCoreApiV1? _vipApi;
  private bool _isFeatureRegistered;

  private readonly int[] _selectedTagIndices = new int[65];

  public VIP_Tag(ISwiftlyCore core) : base(core)
  {
  }

  public override void ConfigureSharedInterface(IInterfaceManager interfaceManager) {
  }

  public override void UseSharedInterface(IInterfaceManager interfaceManager) {
    _vipApi = null;
    _isFeatureRegistered = false;

    if (interfaceManager.HasSharedInterface("VIPCore.Api.v1"))
      _vipApi = interfaceManager.GetSharedInterface<IVipCoreApiV1>("VIPCore.Api.v1");

    RegisterVipFeaturesWhenReady();
  }

  public override void Load(bool hotReload) {
    for (var i = 0; i < _selectedTagIndices.Length; i++)
      _selectedTagIndices[i] = 0;

    Core.Event.OnClientDisconnected += OnClientDisconnected;
    RegisterVipFeaturesWhenReady();
  }

  public override void Unload() {
    Core.Event.OnClientDisconnected -= OnClientDisconnected;

    if (_vipApi != null)
    {
      _vipApi.OnCoreReady -= RegisterVipFeatures;
      _vipApi.OnPlayerSpawn -= OnPlayerSpawn;
      _vipApi.PlayerLoaded -= OnPlayerLoaded;
      _vipApi.PlayerRemoved -= OnPlayerRemoved;

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

    _vipApi.RegisterFeature(FeatureKey, FeatureType.Selectable, (player, state) =>
    {
      Core.Scheduler.NextTick(() => CycleTag(player));
    },
    displayNameResolver: p => "Tag");

    _isFeatureRegistered = true;
    _vipApi.OnPlayerSpawn += OnPlayerSpawn;
    _vipApi.PlayerLoaded += OnPlayerLoaded;
    _vipApi.PlayerRemoved += OnPlayerRemoved;
  }

  private void CycleTag(IPlayer player)
  {
    if (_vipApi == null) return;
    if (!player.IsValid || player.IsFakeClient) return;
    if (player.Slot < 0 || player.Slot >= _selectedTagIndices.Length) return;
    if (!_vipApi.PlayerHasFeature(player, FeatureKey)) return;

    var tags = _vipApi.GetFeatureValue<List<string>>(player, FeatureKey) ?? [];
    var current = _selectedTagIndices[player.Slot];

    var next = 0;
    if (tags.Count > 0)
    {
      if (current <= 0)
        next = 1;
      else if (current >= tags.Count)
        next = 0;
      else
        next = current + 1;
    }

    SetTagIndex(player, next, save: true);

    if (next <= 0)
    {
      player.SendMessage(MessageType.Chat, "Tag: disabled");
    }
    else
    {
      var tag = tags[next - 1] ?? string.Empty;
      player.SendMessage(MessageType.Chat, $"Tag: {tag}");
    }
  }

  private void SetTagIndex(IPlayer player, int indexValue, bool save)
  {
    if (!player.IsValid) return;
    if (player.Slot < 0 || player.Slot >= _selectedTagIndices.Length) return;

    _selectedTagIndices[player.Slot] = indexValue;
    ScheduleApplyAttempt(player, attempt: 1);

    if (!save) return;
    if (_vipApi == null) return;
    _vipApi.SetPlayerCookie(player, FeatureValueCookieKey, indexValue);
  }

  private void OnClientDisconnected(IOnClientDisconnectedEvent @event)
  {
    if (@event.PlayerId < 0 || @event.PlayerId >= _selectedTagIndices.Length) return;
    _selectedTagIndices[@event.PlayerId] = 0;
  }

  private void OnPlayerLoaded(IPlayer player, string group)
  {
    if (_vipApi == null) return;
    if (!player.IsValid || player.IsFakeClient) return;
    if (!_vipApi.PlayerHasFeature(player, FeatureKey)) return;

    LoadTagIndexFromCookie(player);
    ScheduleApplyAttempt(player, attempt: 1);
  }

  private void OnPlayerRemoved(IPlayer player, string group)
  {
    if (!player.IsValid || player.IsFakeClient) return;

    if (player.Slot >= 0 && player.Slot < _selectedTagIndices.Length)
      _selectedTagIndices[player.Slot] = 0;

    ScheduleApplyAttempt(player, attempt: 1);
  }

  private void OnPlayerSpawn(IPlayer player)
  {
    if (_vipApi == null) return;
    if (!player.IsValid || player.IsFakeClient) return;
    if (player.Slot < 0 || player.Slot >= _selectedTagIndices.Length) return;

    if (!_vipApi.PlayerHasFeature(player, FeatureKey))
      _selectedTagIndices[player.Slot] = 0;
    else
      LoadTagIndexFromCookie(player);

    ScheduleApplyAttempt(player, attempt: 1);
  }

  private void LoadTagIndexFromCookie(IPlayer player)
  {
    if (_vipApi == null) return;
    if (!player.IsValid) return;
    if (player.Slot < 0 || player.Slot >= _selectedTagIndices.Length) return;

    try
    {
      var idx = _vipApi.GetPlayerCookie<int>(player, FeatureValueCookieKey);
      _selectedTagIndices[player.Slot] = idx;
    }
    catch
    {
      _selectedTagIndices[player.Slot] = 0;
    }
  }

  private void ScheduleApplyAttempt(IPlayer player, int attempt)
  {
    Core.Scheduler.NextTick(() =>
    {
      if (TryApplyTag(player)) return;
      if (attempt >= ApplyMaxAttempts) return;

      Core.Scheduler.DelayBySeconds(ApplyRetryDelaySeconds, () => ScheduleApplyAttempt(player, attempt + 1));
    });
  }

  private bool TryApplyTag(IPlayer player)
  {
    if (!player.IsValid) return false;
    if (player.IsFakeClient) return false;
    if (player.Controller == null || !player.Controller.IsValid) return false;
    if (player.Slot < 0 || player.Slot >= _selectedTagIndices.Length) return false;

    var controller = player.Controller as CCSPlayerController;
    if (controller == null || !controller.IsValid) return false;

    var tag = string.Empty;
    if (_vipApi != null)
    {
      var tags = _vipApi.GetFeatureValue<List<string>>(player, FeatureKey);
      var idx = _selectedTagIndices[player.Slot];
      if (tags != null && idx > 0)
      {
        var listIndex = idx - 1;
        if (listIndex >= 0 && listIndex < tags.Count)
          tag = tags[listIndex] ?? string.Empty;
      }
    }

    controller.Clan = tag;
    controller.ClanUpdated();

    return true;
  }
}
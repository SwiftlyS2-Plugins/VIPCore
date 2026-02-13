using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Players;

namespace VIPCore.Contract;

public abstract class VipFeatureBase : IVipFeature
{
    public abstract string Feature { get; }
    protected readonly IVipCoreApiV1 _api;
    protected readonly ISwiftlyCore Core;

    IVipCoreApiV1 IVipFeature.Api
    {
        get => _api;
        set => throw new NotSupportedException("Api is provided via constructor and cannot be reassigned.");
    }

    protected VipFeatureBase(IVipCoreApiV1 api, ISwiftlyCore core)
    {
        _api = api;
        Core = core;

        _api.OnPlayerSpawn += OnPlayerSpawn;
        _api.PlayerLoaded += OnPlayerLoaded;
        _api.PlayerRemoved += OnPlayerRemoved;
    }
    public virtual void OnPlayerSpawn(IPlayer player)
    {
    }

    public virtual void OnPlayerLoaded(IPlayer player, string group)
    {
    }

    public virtual void OnPlayerRemoved(IPlayer player, string group)
    {
    }

    public virtual void OnSelectItem(IPlayer player, FeatureState state)
    {
    }

    public void RegisterFeature(FeatureType featureType = FeatureType.Toggle, Func<IPlayer, string>? displayNameResolver = null)
    {
        _api.RegisterFeature(Feature, featureType, OnSelectItem, displayNameResolver);
    }

    public void UnregisterFeature()
    {
        _api.OnPlayerSpawn -= OnPlayerSpawn;
        _api.PlayerLoaded -= OnPlayerLoaded;
        _api.PlayerRemoved -= OnPlayerRemoved;
        _api.UnregisterFeature(Feature);
    }
}
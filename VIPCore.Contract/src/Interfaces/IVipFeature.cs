namespace VIPCore.Contract;

public interface IVipFeature
{
    public string Feature { get; }
    public IVipCoreApiV1 Api { get; set; }
}
using VIPCore.Contract;

namespace VIP_Endurance;

public static class EndurancePolicy
{
    public static bool Enabled(bool ready, bool isVip, FeatureState original, FeatureState legacy)
    {
        if (!ready || !isVip) return false;
        // The explicitly available original feature wins, including its Disabled state.
        return (original != FeatureState.NoAccess ? original : legacy) == FeatureState.Enabled;
    }
    public static bool NeedsReset(float value) => float.IsFinite(value) && value < 1.0f;
}

namespace VIP_AntiFlash;

public static class AntiFlashPolicy
{
    /// <summary>True means clear flash duration; false means leave the game's flash untouched.</summary>
    public static bool ShouldBlock(int mode, bool isSelf, bool sameTeam) => mode switch
    {
        0 => true,
        1 => sameTeam && !isSelf,
        2 => isSelf,
        3 => sameTeam || isSelf,
        _ => false // Fail closed: invalid settings do not grant immunity.
    };
}

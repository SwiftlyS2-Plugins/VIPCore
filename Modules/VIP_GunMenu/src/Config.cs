namespace VIP_GunMenu;

public class PluginConfig
{
    public static readonly List<string> DefaultCommandAliases = ["guns", "gm"];

    public List<string> CommandAliases { get; set; } = [.. DefaultCommandAliases];
    public bool DisableCommandAfterRoundStarts { get; set; } = true;
    public int CommandDisableDelayAfterRoundStarts { get; set; } = 20;
}

// The gun lists default to null: the binder appends to a non-empty default, so a group could only ever add guns.
public class GroupConfig
{
    public bool GivePrimariesOnPistolRound { get; set; } = false;
    public bool GiveSecondariesAfterPrimaries { get; set; } = true;
    public bool ReplaceCurrentWeapons { get; set; } = true;
    public int MaxUsesPerPlayerPerRound { get; set; } = 3;

    public List<Gun>? AvailablePrimaryGuns { get; set; }
    public List<Gun>? AvailableSecondaryGuns { get; set; }

    public static readonly List<Gun> DefaultPrimaryGuns =
    [
        new Gun() { DisplayName = "AWP", WeaponName = "weapon_awp", Category = "Sniper" },
        new Gun() { DisplayName = "AK-47", WeaponName = "weapon_ak47", Category = "Rifle" },
        new Gun() { DisplayName = "M4A4", WeaponName = "weapon_m4a1", Category = "Rifle" },
        new Gun() { DisplayName = "M4A1-S", WeaponName = "weapon_m4a1_silencer", Category = "Rifle" },
        new Gun() { DisplayName = "SSG 08", WeaponName = "weapon_ssg08", Category = "Sniper" },
        new Gun() { DisplayName = "P90", WeaponName = "weapon_p90", Category = "SMG" }
    ];

    public static readonly List<Gun> DefaultSecondaryGuns =
    [
        new Gun() { DisplayName = "Desert Eagle", WeaponName = "weapon_deagle", Category = "Pistol" },
        new Gun() { DisplayName = "Tec-9", WeaponName = "weapon_tec9", Category = "Pistol" },
        new Gun() { DisplayName = "P250", WeaponName = "weapon_p250", Category = "Pistol" },
        new Gun() { DisplayName = "Glock-18", WeaponName = "weapon_glock", Category = "Pistol" },
        new Gun() { DisplayName = "USP-S", WeaponName = "weapon_usp_silencer", Category = "Pistol" },
        new Gun() { DisplayName = "P2000", WeaponName = "weapon_hkp2000", Category = "Pistol" },
        new Gun() { DisplayName = "Five-SeveN", WeaponName = "weapon_fiveseven", Category = "Pistol" },
        new Gun() { DisplayName = "CZ75-Auto", WeaponName = "weapon_cz75a", Category = "Pistol" },
        new Gun() { DisplayName = "R8 Revolver", WeaponName = "weapon_revolver", Category = "Pistol" }
    ];
}

public class Gun
{
    public string DisplayName { get; set; } = "AK-47";
    public string WeaponName { get; set; } = "weapon_ak47";
    public string Category { get; set; } = "Rifle";
}

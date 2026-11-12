<div align="center">
  <h1>🔫 VIP_GunMenu</h1>
  <h3>Interactive weapon selection menu for VIP players</h3>
  <p>A VIPCore module that provides VIP players with an in-game menu to select and equip primary and secondary weapons</p>
</div>

<p align="center">
  <img src="https://img.shields.io/badge/build-passing-brightgreen" alt="Build Status">
  <img src="https://img.shields.io/badge/.NET-10.0-512BD4" alt=".NET 10">
  <img src="https://img.shields.io/badge/SwiftlyS2-compatible-blue" alt="SwiftlyS2">
  <img src="https://img.shields.io/github/license/shmitzas/VIPCore" alt="License">
</p>

---

## 📖 Description

**VIP_GunMenu** is a VIPCore module that gives VIP players a free loadout. Its **Gun menu** page sits in the VIP menu (`!vip`) and `!gunmenu` opens it directly: the player picks a primary and a pistol (or just one) and presses **Give loadout**. Each loadout given counts as one use; opening the page is free.

The page shows the uses left, the loadout, whether giving replaces or adds to the guns held, and a countdown to when it stops taking uses after the round starts. It preselects the last loadout the player took on the current map. In the VIP menu the perk reads, for example, *3 picks a round, or !guns* (translation keys `vip.gun_menu.description*`, naming the first configured alias).

## 📦 Installation

1. Download the latest release or build from source
2. Place `VIP_GunMenu.dll` in `addons/swiftlys2/plugins/VIP_GunMenu/`
3. Place the `resources/` folder in `addons/swiftlys2/plugins/VIP_GunMenu/`
4. Configure the feature in your `vip_groups.jsonc` (see below)
5. Restart the server or use `sw2 plugins reload VIP_GunMenu`

## 🎮 Commands

| Command | Aliases | Description | Permission |
|---------|---------|-------------|------------|
| `!gunmenu` | `!guns`, `!gm` | Opens the Gun menu page of the VIP menu | VIP with feature enabled |

## ⚙️ Configuration

### Plugin Configuration (`config.jsonc`)

Located in `addons/swiftlys2/configs/plugins/VIP_GunMenu/config.jsonc`:

```jsonc
{
  "GunMenu": {
	"CommandAliases": ["guns", "gm"]
	"DisableCommandAfterRoundStarts": true,
	"CommandDisableDelayAfterRoundStarts": 20,
  }
}
```

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `CommandAliases` | string[] | `["guns", "gm"]` | Additional command aliases for `!gunmenu`; the first one is named in the perk's description |
| `DisableCommandAfterRoundStarts` | bool | `true` | Stop the command and **Give loadout** a while after the round starts |
| `CommandDisableDelayAfterRoundStarts` | int | `20` | Seconds after round start before they stop; the page counts them down |

### VIP Group Configuration (`vip_groups.jsonc`)

Add the `vip.gun_menu` feature to your VIP groups configuration:

```jsonc
{
  "vip_groups": {
	"Groups": {
	  "GOLD": {
		"Values": {
		  "vip.gun_menu": {
			"GivePrimariesOnPistolRound": false,
			"GiveSecondariesAfterPrimaries": true,
			"ReplaceCurrentWeapons": true,
			"MaxUsesPerPlayerPerRound": 3,
			"AvailablePrimaryGuns": [
			  {
				"DisplayName": "AWP",
				"WeaponName": "weapon_awp",
				"Category": "Sniper"
			  },
			  {
				"DisplayName": "AK-47",
				"WeaponName": "weapon_ak47",
				"Category": "Rifle"
			  },
			  {
				"DisplayName": "M4A4",
				"WeaponName": "weapon_m4a1",
				"Category": "Rifle"
			  },
			  {
				"DisplayName": "M4A1-S",
				"WeaponName": "weapon_m4a1_silencer",
				"Category": "Rifle"
			  },
			  {
				"DisplayName": "SSG 08",
				"WeaponName": "weapon_ssg08",
				"Category": "Sniper"
			  },
			  {
				"DisplayName": "P90",
				"WeaponName": "weapon_p90",
				"Category": "SMG"
			  }
			],
			"AvailableSecondaryGuns": [
			  {
				"DisplayName": "Desert Eagle",
				"WeaponName": "weapon_deagle",
				"Category": "Pistol"
			  },
			  {
				"DisplayName": "Tec-9",
				"WeaponName": "weapon_tec9",
				"Category": "Pistol"
			  },
			  {
				"DisplayName": "P250",
				"WeaponName": "weapon_p250",
				"Category": "Pistol"
			  },
			  {
				"DisplayName": "Glock-18",
				"WeaponName": "weapon_glock",
				"Category": "Pistol"
			  },
			  {
				"DisplayName": "USP-S",
				"WeaponName": "weapon_usp_silencer",
				"Category": "Pistol"
			  },
			  {
				"DisplayName": "P2000",
				"WeaponName": "weapon_hkp2000",
				"Category": "Pistol"
			  },
			  {
				"DisplayName": "Five-SeveN",
				"WeaponName": "weapon_fiveseven",
				"Category": "Pistol"
			  },
			  {
				"DisplayName": "CZ75-Auto",
				"WeaponName": "weapon_cz75a",
				"Category": "Pistol"
			  },
			  {
				"DisplayName": "R8 Revolver",
				"WeaponName": "weapon_revolver",
				"Category": "Pistol"
			  }
			]
		  }
		}
	  },
	  "PLATINUM": {
		"Values": {
		  "vip.gun_menu": {
			"GivePrimariesOnPistolRound": true,
			"MaxUsesPerPlayerPerRound": 5,
			"AvailablePrimaryGuns": [
			  {
				"DisplayName": "AWP",
				"WeaponName": "weapon_awp",
				"Category": "Sniper"
			  },
			  {
				"DisplayName": "AK-47",
				"WeaponName": "weapon_ak47",
				"Category": "Rifle"
			  }
			],
			"AvailableSecondaryGuns": [
			  {
				"DisplayName": "Desert Eagle",
				"WeaponName": "weapon_deagle",
				"Category": "Pistol"
			  }
			]
		  }
		}
	  }
	}
  }
}
```

### Configuration Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `GivePrimariesOnPistolRound` | bool | `false` | List primary weapons on pistol rounds (first round & second half first round) |
| `GiveSecondariesAfterPrimaries` | bool | `true` | List pistols under the primaries. When `false`, pistols are only listed when no primaries are (e.g. on a pistol round) |
| `ReplaceCurrentWeapons` | bool | `true` | Remove the weapon in the same slot before giving a new one; the page notes whether giving replaces or adds to the guns held |
| `MaxUsesPerPlayerPerRound` | int | `3` | Loadouts a player can take per round |
| `AvailablePrimaryGuns` | Gun[] | *(see Config.cs)* | Primary weapons on the page |
| `AvailableSecondaryGuns` | Gun[] | *(see Config.cs)* | Pistols on the page |

### Gun Object Structure

Each weapon in the lists has the following properties:

```jsonc
{
  "DisplayName": "AWP",          // Display name shown in the menu
  "WeaponName": "weapon_awp",    // CS2 weapon class name
  "Category": "Sniper"           // Weapon category (Rifle, Sniper, SMG, Pistol, etc.)
}
```

## 🔫 Available Weapons

Complete list of all weapons that can be configured in the gun menu:

**Snipers:**
- `weapon_awp` - AWP
- `weapon_g3sg1` - G3SG1
- `weapon_scar20` - SCAR-20
- `weapon_ssg08` - SSG 08

**Rifles:**
- `weapon_ak47` - AK-47
- `weapon_aug` - AUG
- `weapon_famas` - FAMAS
- `weapon_galilar` - Galil AR
- `weapon_m4a1` - M4A4
- `weapon_m4a1_silencer` - M4A1-S
- `weapon_sg553` - SG 553

**Heavy:**
- `weapon_m249` - M249
- `weapon_negev` - Negev

**Shotguns:**
- `weapon_mag7` - MAG-7
- `weapon_nova` - Nova
- `weapon_sawedoff` - Sawed-Off
- `weapon_xm1014` - XM1014

**SMGs:**
- `weapon_bizon` - PP-Bizon
- `weapon_mac10` - MAC-10
- `weapon_mp5sd` - MP5-SD
- `weapon_mp7` - MP7
- `weapon_mp9` - MP9
- `weapon_p90` - P90
- `weapon_ump45` - UMP-45

**Pistols:**
- `weapon_cz75a` - CZ75-Auto
- `weapon_deagle` - Desert Eagle
- `weapon_elite` - Dual Berettas
- `weapon_fiveseven` - Five-SeveN
- `weapon_glock` - Glock-18
- `weapon_p250` - P250
- `weapon_hkp2000` - P2000
- `weapon_revolver` - R8 Revolver
- `weapon_tec9` - Tec-9
- `weapon_usp_silencer` - USP-S

## Features

- **One-Page Loadout**: The Gun menu page lists primaries and pistols together; pick one of each (pick again to clear) and give them in one go
- **Part of the VIP Menu**: The page sits in the VIP menu next to VIPCore's own pages, and `!gunmenu` opens it directly
- **Usage Limits**: Configurable number of loadouts per player per round; opening the page is free
- **Round Window**: The page counts down to when the command and **Give loadout** stop taking uses
- **Remembered Loadout**: The last loadout a player took on the current map is picked in advance
- **Pistol Round Detection**: Automatically detects pistol rounds and can restrict primary weapons
- **Weapon Replacement**: Automatically removes current weapon before giving a new one
- **Per-Group Configuration**: Each VIP group can have different weapon lists and settings
- **Custom Display Names**: Weapons shown with friendly names (e.g., "Desert Eagle" instead of "weapon_deagle")
- **Command Aliases**: Support for multiple command aliases (e.g., !gunmenu, !guns, !gm)
- **Player Freeze**: Players are frozen while browsing the menu for better selection experience

## Building

- Open the project in your preferred .NET IDE (e.g., Visual Studio, Rider, VS Code).
- Build the project. The output DLL and resources will be placed in the `build/` directory.
- The publish process will also create a zip file for easy distribution.

## Publishing

- Use the `dotnet publish -c Release` command to build and package your plugin.
- Distribute the generated zip file or the contents of the `build/publish` directory.

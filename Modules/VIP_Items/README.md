<div align="center">
  <img src="https://pan.samyyc.dev/s/VYmMXE" />
  <h2><strong>VIP_Items</strong></h2>
  <h3>Gives VIP players custom weapons/items on spawn.</h3>
</div>

<p align="center">
  <img src="https://img.shields.io/badge/build-passing-brightgreen" alt="Build Status">
  <img src="https://img.shields.io/github/downloads/aga/VIP_Items/total" alt="Downloads">
  <img src="https://img.shields.io/github/stars/aga/VIP_Items?style=flat&logo=github" alt="Stars">
  <img src="https://img.shields.io/github/license/aga/VIP_Items" alt="License">
</p>

## Description

VIP_Items is a VIPCore module that automatically gives VIP players a customizable list of weapons and items when they spawn. The items list is configurable per VIP group, and the plugin prevents duplicate items from being given.

## Installation

1. Place `VIP_Items.dll` in `(swRoot)/plugins/VIP_Items/`
2. Add the feature to your `vip_groups.jsonc` configuration (see below)
3. Restart the server or hot-reload the plugin

## VIP Group Configuration

Add the items feature to your `vip_groups.jsonc` file:

```jsonc
{
  "vip_groups": {
    "Groups": {
      "GOLD": {
        "Values": {
          "vip.items": {
            "CT": [
                "weapon_smokegrenade",
			    "weapon_flashbang",
			    "weapon_molotov",
			    "weapon_hegrenade",
			    "weapon_healthshot"
            ],
            "T": [
                "weapon_smokegrenade",
			    "weapon_flashbang",
			    "weapon_molotov",
			    "weapon_hegrenade",
			    "weapon_healthshot"
            ]
          }
        }
      }
    }
  }
}
```

### Configuration Options

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Weapons` | array | [] | List of weapon/item class names to give on spawn |

### Weapon/Item Examples

Common weapon and item class names you can use:

**Rifles:**
- `weapon_ak47`
- `weapon_m4a1`
- `weapon_m4a1_silencer`
- `weapon_awp`
- `weapon_ssg08`

**Pistols:**
- `weapon_deagle`
- `weapon_glock`
- `weapon_usp_silencer`
- `weapon_p250`

**Grenades:**
- `weapon_hegrenade`
- `weapon_flashbang`
- `weapon_smokegrenade`
- `weapon_molotov`
- `weapon_incgrenade`

**Equipment:**
- `item_defuser`
- `item_kevlar`
- `item_assaultsuit`

### Features

- **Automatic Weapon Distribution**: Weapons are given automatically when a VIP player spawns
- **Duplicate Prevention**: The plugin checks if a player already has a weapon before giving it
- **Per-Group Configuration**: Each VIP group can have its own custom weapon list

## Building

- Open the project in your preferred .NET IDE (e.g., Visual Studio, Rider, VS Code).
- Build the project. The output DLL and resources will be placed in the `build/` directory.
- The publish process will also create a zip file for easy distribution.

## Publishing

- Use the `dotnet publish -c Release` command to build and package your plugin.
- Distribute the generated zip file or the contents of the `build/publish` directory.
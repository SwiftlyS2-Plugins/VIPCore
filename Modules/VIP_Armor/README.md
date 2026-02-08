<div align="center">
  <img src="https://pan.samyyc.dev/s/VYmMXE" />
  <h2><strong>VIP_Armor</strong></h2>
  <h3>Gives VIP players armor and helmet on spawn.</h3>
</div>

<p align="center">
  <img src="https://img.shields.io/badge/build-passing-brightgreen" alt="Build Status">
  <img src="https://img.shields.io/github/downloads/aga/VIP_Armor/total" alt="Downloads">
  <img src="https://img.shields.io/github/stars/aga/VIP_Armor?style=flat&logo=github" alt="Stars">
  <img src="https://img.shields.io/github/license/aga/VIP_Armor" alt="License">
</p>

## Description

VIP_Armor is a VIPCore module that gives VIP players armor and a helmet when they spawn. The armor amount is configurable per VIP group.

## Installation

1. Place `VIP_Armor.dll` in `(swRoot)/plugins/VIP_Armor/`
2. Add the feature to your `vip_groups.jsonc` configuration (see below)
3. Restart the server or hot-reload the plugin

## VIP Group Configuration

Add the armor feature to your `vip_groups.jsonc` file:

```jsonc
{
  "vip_groups": {
    "Groups": {
      "GOLD": {
        "Values": {
          "vip.armor": {
            "Armor": 100  // Armor amount (0-100)
          }
        }
      },
      "SILVER": {
        "Values": {
          "vip.armor": {
            "Armor": 50
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
| `Armor` | int | 0 | Amount of armor to give (0-100). Players also receive a helmet. |

## Building

- Open the project in your preferred .NET IDE (e.g., Visual Studio, Rider, VS Code).
- Build the project. The output DLL and resources will be placed in the `build/` directory.
- The publish process will also create a zip file for easy distribution.

## Publishing

- Use the `dotnet publish -c Release` command to build and package your plugin.
- Distribute the generated zip file or the contents of the `build/publish` directory.
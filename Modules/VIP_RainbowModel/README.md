<div align="center">
  <img src="https://pan.samyyc.dev/s/VYmMXE" />
  <h2><strong>VIP_RainbowModel</strong></h2>
  <h3>Cycles the player's model render color (rainbow effect) while the feature is enabled.</h3>
</div>

<p align="center">
  <img src="https://img.shields.io/badge/build-passing-brightgreen" alt="Build Status">
  <img src="https://img.shields.io/github/downloads/aga/VIP_RainbowModel/total" alt="Downloads">
  <img src="https://img.shields.io/github/stars/aga/VIP_RainbowModel?style=flat&logo=github" alt="Stars">
  <img src="https://img.shields.io/github/license/aga/VIP_RainbowModel" alt="License">
</p>

## Overview

This module registers the VIPCore feature key:

- `vip.rainbowmodel`

When enabled for a VIP player, their `CCSPlayerPawn.Render` color is randomized on a fixed interval.

## Configuration (VIP groups)

Add the feature to your server's `vip_groups.jsonc` under the group's `Values`.

### Simple enable (default interval)

```jsonc
{
  "vip_groups": {
    "Groups": {
      "VIP": {
        "Weight": 10,
        "Values": {
          "vip.rainbowmodel": 1
        }
      }
    }
  }
}
```

### Advanced settings

```jsonc
{
  "vip_groups": {
    "Groups": {
      "VIP": {
        "Weight": 10,
        "Values": {
          "vip.rainbowmodel": {
            "Enabled": true,
            "IntervalSeconds": 1.4
          }
        }
      }
    }
  }
}
```

## Notes

- If a player disables the feature in the VIP menu, the module resets their render color back to white.
- The loop is stopped on death and on disconnect.

## Building

- Open the project in your preferred .NET IDE (e.g., Visual Studio, Rider, VS Code).
- Build the project. The output DLL and resources will be placed in the `build/` directory.
- The publish process will also create a zip file for easy distribution.

## Publishing

- Use the `dotnet publish -c Release` command to build and package your plugin.
- Distribute the generated zip file or the contents of the `build/publish` directory.
<div align="center">
  <img src="https://pan.samyyc.dev/s/VYmMXE" />
  <h2><strong>VIP_Flags</strong></h2>
  <h3>VIP_Flags</h3>
</div>

<p align="center">
  <img src="https://img.shields.io/badge/build-passing-brightgreen" alt="Build Status">
  <img src="https://img.shields.io/badge/version-1.0-blue" alt="Version">
</p>

## Description

VIP_Flags is a plugin for Swiftly2 that allows server owners to grant specific permissions to VIP players based on their VIP group configuration. This plugin integrates with the VIP API to check players' VIP status and apply or remove permissions accordingly.

In the VIP menu the perk reads, for example, *Unlocks 2 server permissions*, counted from the group's `Permissions` (translation keys `vip.flags.description*`).

## Installation

1. Place `VIP_Flags.dll` in `(swRoot)/plugins/VIP_Flags/`
2. Add the feature to your `vip_groups.jsonc` configuration (see below)
3. Restart the server or hot-reload the plugin

## VIP Group Configuration

Add the flags feature to your `vip_groups.jsonc` file:

```jsonc
{
  "vip_groups": {
    "Groups": {
      "GOLD": {
        "Values": {
          "vip.flags": {
            "Permissions": "VIP_Godmode,VIP_Super"  // Comma-separated list of permissions
          }
        }
      },
      "SILVER": {
        "Values": {
          "vip.flags": {
            "Permissions": "VIP_Super"  // Comma-separated list of permissions
          }
        }
      }
    }
  }
}
```

## Author
- [SLAYER](https://github.com/zakriamansoor47)
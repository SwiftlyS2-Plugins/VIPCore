<div align="center">
  <img src="https://pan.samyyc.dev/s/VYmMXE" />
  <h2><strong>VIP_FastDefuse</strong></h2>
  <h3>No description.</h3>
</div>

<p align="center">
  <img src="https://img.shields.io/badge/build-passing-brightgreen" alt="Build Status">
  <img src="https://img.shields.io/github/downloads/aga/VIP_FastDefuse/total" alt="Downloads">
  <img src="https://img.shields.io/github/stars/aga/VIP_FastDefuse?style=flat&logo=github" alt="Stars">
  <img src="https://img.shields.io/github/license/aga/VIP_FastDefuse" alt="License">
</p>

## Description

VIP_FastDefuse is a VIPCore module that reduces the bomb defuse time for VIP players.

In the VIP menu the perk reads, for example, *Defuses 2× faster*, or *Defuses in 3 s* when the group sets `Seconds` (translation keys `vip.fastdefuse.description*`).

## Installation

1. Place `VIP_FastDefuse.dll` in `(swRoot)/plugins/VIP_FastDefuse/`
2. Add the feature to your `vip_groups.jsonc` configuration (see below)
3. Restart the server or hot-reload the plugin

## VIP Group Configuration

Add the fastdefuse feature to your `vip_groups.jsonc` file:

```jsonc
{
  "vip_groups": {
    "Groups": {
      "GOLD": {
        "Values": {
          "vip.fastdefuse": {
            "Multiplier": 0.5,
            "Seconds": 0
          }
        }
      },
      "SILVER": {
        "Values": {
          "vip.fastdefuse": 0
        }
      }
    }
  }
}
```

### Configuration Options

| Value | Description |
|-------|-------------|
| `1` | Enabled by default (player can toggle) |
| `0` | Disabled by default (player can toggle if they have access) |

If you use an object value, the following properties are supported:

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Multiplier` | float | `0.5` | Multiplies the remaining defuse time. Must be in `(0, 1]`. Example: `0.5` = 50% faster |
| `Seconds` | float | `0` | Overrides the remaining defuse time directly. If `> 0`, it takes priority over `Multiplier` |

## Building

- Open the project in your preferred .NET IDE (e.g., Visual Studio, Rider, VS Code).
- Build the project. The output DLL and resources will be placed in the `build/` directory.
- The publish process will also create a zip file for easy distribution.

## Publishing

- Use the `dotnet publish -c Release` command to build and package your plugin.
- Distribute the generated zip file or the contents of the `build/publish` directory.
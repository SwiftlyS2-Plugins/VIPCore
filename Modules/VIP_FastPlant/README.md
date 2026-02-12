<div align="center">
  <img src="https://pan.samyyc.dev/s/VYmMXE" />
  <h2><strong>VIP_FastPlant</strong></h2>
  <h3>No description.</h3>
</div>

<p align="center">
  <img src="https://img.shields.io/badge/build-passing-brightgreen" alt="Build Status">
  <img src="https://img.shields.io/github/downloads/aga/VIP_FastPlant/total" alt="Downloads">
  <img src="https://img.shields.io/github/stars/aga/VIP_FastPlant?style=flat&logo=github" alt="Stars">
  <img src="https://img.shields.io/github/license/aga/VIP_FastPlant" alt="License">
</p>

## Description

VIP_FastPlant is a VIPCore module that reduces the bomb planting time for VIP players.

## Installation

1. Place `VIP_FastPlant.dll` in `(swRoot)/plugins/VIP_FastPlant/`
2. Add the feature to your `vip_groups.jsonc` configuration (see below)
3. Restart the server or hot-reload the plugin

## VIP Group Configuration

Add the fastplant feature to your `vip_groups.jsonc` file:

```jsonc
{
  "vip_groups": {
    "Groups": {
      "GOLD": {
        "Values": {
          "vip.fastplant": {
            "Multiplier": 0.5,
            "Duration": 0
          }
        }
      },
      "SILVER": {
        "Values": {
          "vip.fastplant": 0
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
| `Multiplier` | float | `0.5` | Multiplies the default plant time. Must be in `(0, 1]`. Example: `0.5` = 50% faster |
| `Duration` | int | `0` | Overrides the duration directly. If `> 0`, it takes priority over `Multiplier` |

## Building

- Open the project in your preferred .NET IDE (e.g., Visual Studio, Rider, VS Code).
- Build the project. The output DLL and resources will be placed in the `build/` directory.
- The publish process will also create a zip file for easy distribution.

## Publishing

- Use the `dotnet publish -c Release` command to build and package your plugin.
- Distribute the generated zip file or the contents of the `build/publish` directory.
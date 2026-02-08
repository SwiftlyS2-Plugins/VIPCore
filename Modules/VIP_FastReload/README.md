<div align="center">
  <img src="https://pan.samyyc.dev/s/VYmMXE" />
  <h2><strong>VIP_FastReload</strong></h2>
  <h3>Instant weapon reload for VIP players.</h3>
</div>

<p align="center">
  <img src="https://img.shields.io/badge/build-passing-brightgreen" alt="Build Status">
  <img src="https://img.shields.io/github/downloads/aga/VIP_FastReload/total" alt="Downloads">
  <img src="https://img.shields.io/github/stars/aga/VIP_FastReload?style=flat&logo=github" alt="Stars">
  <img src="https://img.shields.io/github/license/aga/VIP_FastReload" alt="License">
</p>

## Description

VIP_FastReload is a VIPCore module that gives VIP players instant weapon reloads. When enabled, reloading or firing an empty weapon will immediately refill the clip to maximum capacity.

## Installation

1. Place `VIP_FastReload.dll` in `(swRoot)/plugins/VIP_FastReload/`
2. Add the feature to your `vip_groups.jsonc` configuration (see below)
3. Restart the server or hot-reload the plugin

## VIP Group Configuration

Add the fastreload feature to your `vip_groups.jsonc` file:

```jsonc
{
  "vip_groups": {
    "Groups": {
      "GOLD": {
        "Values": {
          "vip.fastreload": 1  // 1 = enabled by default, 0 = disabled
        }
      },
      "SILVER": {
        "Values": {
          "vip.fastreload": 0  // Disabled by default
        }
      }
    }
  }
}
```

### Configuration Options

| Value | Description |
|-------|-------------|
| `1` | Feature enabled by default (player can toggle) |
| `0` | Feature disabled by default (player can toggle if they have access) |

## Building

- Open the project in your preferred .NET IDE (e.g., Visual Studio, Rider, VS Code).
- Build the project. The output DLL and resources will be placed in the `build/` directory.
- The publish process will also create a zip file for easy distribution.

## Publishing

- Use the `dotnet publish -c Release` command to build and package your plugin.
- Distribute the generated zip file or the contents of the `build/publish` directory.
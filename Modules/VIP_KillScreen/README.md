<div align="center">
  <img src="https://pan.samyyc.dev/s/VYmMXE" />
  <h2><strong>VIP_KillScreen</strong></h2>
  <h3>Applies a health shot screen effect on kill for VIP players.</h3>
</div>

<p align="center">
  <img src="https://img.shields.io/badge/build-passing-brightgreen" alt="Build Status">
  <img src="https://img.shields.io/github/downloads/aga/VIP_KillScreen/total" alt="Downloads">
  <img src="https://img.shields.io/github/stars/aga/VIP_KillScreen?style=flat&logo=github" alt="Stars">
  <img src="https://img.shields.io/github/license/aga/VIP_KillScreen" alt="License">
</p>

## Description

VIP_KillScreen is a VIPCore module that applies a health shot screen effect to VIP players when they get a kill (excluding suicides). The effect duration is configurable per VIP group.

## Installation

1. Place `VIP_KillScreen.dll` in `(swRoot)/plugins/VIP_KillScreen/`
2. Add the feature to your `vip_groups.jsonc` configuration (see below)
3. Restart the server or hot-reload the plugin

## VIP Group Configuration

Add the killscreen feature to your `vip_groups.jsonc` file:

```jsonc
{
  "vip_groups": {
    "Groups": {
      "GOLD": {
        "Values": {
          "vip.killscreen": {
            "Duration": 1.0  // Effect duration in seconds
          }
        }
      },
      "SILVER": {
        "Values": {
          "vip.killscreen": {
            "Duration": 0.5
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
| `Duration` | float | 1.0 | Duration of the health shot screen effect in seconds |

## Building

- Open the project in your preferred .NET IDE (e.g., Visual Studio, Rider, VS Code).
- Build the project. The output DLL and resources will be placed in the `build/` directory.
- The publish process will also create a zip file for easy distribution.

## Publishing

- Use the `dotnet publish -c Release` command to build and package your plugin.
- Distribute the generated zip file or the contents of the `build/publish` directory.
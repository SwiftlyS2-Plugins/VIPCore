<div align="center">
  <img src="https://pan.samyyc.dev/s/VYmMXE" />
  <h2><strong>VIP_Health</strong></h2>
  <h3>Gives VIP players increased health on spawn.</h3>
</div>

<p align="center">
  <img src="https://img.shields.io/badge/build-passing-brightgreen" alt="Build Status">
  <img src="https://img.shields.io/github/downloads/aga/VIP_Health/total" alt="Downloads">
  <img src="https://img.shields.io/github/stars/aga/VIP_Health?style=flat&logo=github" alt="Stars">
  <img src="https://img.shields.io/github/license/aga/VIP_Health" alt="License">
</p>

## Description

VIP_Health is a VIPCore module that gives VIP players increased health and max health when they spawn. The health amount is configurable per VIP group.

## Installation

1. Place `VIP_Health.dll` in `(swRoot)/plugins/VIP_Health/`
2. Add the feature to your `vip_groups.jsonc` configuration (see below)
3. Restart the server or hot-reload the plugin

## VIP Group Configuration

Add the health feature to your `vip_groups.jsonc` file:

```jsonc
{
  "vip_groups": {
    "Groups": {
      "GOLD": {
        "Values": {
          "vip.health": {
            "Health": 150  // Health amount (e.g., 100, 150, 200)
          }
        }
      },
      "SILVER": {
        "Values": {
          "vip.health": {
            "Health": 120
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
| `Health` | int | 0 | Amount of health and max health to set on spawn |

## Building

- Open the project in your preferred .NET IDE (e.g., Visual Studio, Rider, VS Code).
- Build the project. The output DLL and resources will be placed in the `build/` directory.
- The publish process will also create a zip file for easy distribution.

## Publishing

- Use the `dotnet publish -c Release` command to build and package your plugin.
- Distribute the generated zip file or the contents of the `build/publish` directory.
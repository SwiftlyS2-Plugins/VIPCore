<div align="center">
  <img src="https://pan.samyyc.dev/s/VYmMXE" />
  <h2><strong>VIP_Bhop</strong></h2>
  <h3>Enables bunnyhop (auto-jump) for VIP players.</h3>
</div>

<p align="center">
  <img src="https://img.shields.io/badge/build-passing-brightgreen" alt="Build Status">
  <img src="https://img.shields.io/github/downloads/aga/VIP_Bhop/total" alt="Downloads">
  <img src="https://img.shields.io/github/stars/aga/VIP_Bhop?style=flat&logo=github" alt="Stars">
  <img src="https://img.shields.io/github/license/aga/VIP_Bhop" alt="License">
</p>

## Description

VIP_Bhop is a VIPCore module that enables bunnyhop (automatic jumping) for VIP players. The feature activates after a configurable delay at round start.

## Installation

1. Place `VIP_Bhop.dll` in `(swRoot)/plugins/VIP_Bhop/`
2. Add the feature to your `vip_groups.jsonc` configuration (see below)
3. Restart the server or hot-reload the plugin

## VIP Group Configuration

Add the bhop feature to your `vip_groups.jsonc` file:

```jsonc
{
  "vip_groups": {
    "Groups": {
      "GOLD": {
        "Values": {
          "vip.bhop": {
            "Timer": 5.0,      // Seconds after round start to activate
            "MaxSpeed": 300.0  // Maximum bunnyhop speed
          }
        }
      },
      "SILVER": {
        "Values": {
          "vip.bhop": {
            "Timer": 10.0,
            "MaxSpeed": 250.0
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
| `Timer` | float | 5.0 | Seconds after round start before bunnyhop activates |
| `MaxSpeed` | float | 300.0 | Maximum speed cap while bunnyhopping |

## Building

- Open the project in your preferred .NET IDE (e.g., Visual Studio, Rider, VS Code).
- Build the project. The output DLL and resources will be placed in the `build/` directory.
- The publish process will also create a zip file for easy distribution.

## Publishing

- Use the `dotnet publish -c Release` command to build and package your plugin.
- Distribute the generated zip file or the contents of the `build/publish` directory.
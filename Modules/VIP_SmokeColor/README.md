<div align="center">
  <img src="https://pan.samyyc.dev/s/VYmMXE" />
  <h2><strong>VIP_SmokeColor</strong></h2>
  <h3>Custom smoke grenade colors for VIP players.</h3>
</div>

<p align="center">
  <img src="https://img.shields.io/badge/build-passing-brightgreen" alt="Build Status">
  <img src="https://img.shields.io/github/downloads/aga/VIP_SmokeColor/total" alt="Downloads">
  <img src="https://img.shields.io/github/stars/aga/VIP_SmokeColor?style=flat&logo=github" alt="Stars">
  <img src="https://img.shields.io/github/license/aga/VIP_SmokeColor" alt="License">
</p>

## Description

VIP_SmokeColor is a VIPCore module that gives VIP players custom smoke grenade colors. Each VIP group can have its own smoke color defined by RGB values.

## Installation

1. Place `VIP_SmokeColor.dll` in `(swRoot)/plugins/VIP_SmokeColor/`
2. Add the feature to your `vip_groups.jsonc` configuration (see below)
3. Restart the server or hot-reload the plugin

## VIP Group Configuration

Add the smokecolor feature to your `vip_groups.jsonc` file:

```jsonc
{
  "vip_groups": {
    "Groups": {
      "GOLD": {
        "Values": {
          "vip.smokecolor": [255, 215, 0]  // RGB color (Gold)
        }
      },
      "SILVER": {
        "Values": {
          "vip.smokecolor": [192, 192, 192]  // RGB color (Silver)
        }
      },
      "VIP": {
        "Values": {
          "vip.smokecolor": [-1, -1, -1]  // Random color for each smoke
        }
      }
    }
  }
}
```

### Configuration Options

| Format | Description |
|--------|-------------|
| `[R, G, B]` | RGB values (0-255) for fixed color |
| `[-1, -1, -1]` | Random color for each smoke grenade |

### RGB Color Examples

| Color | RGB Values |
|-------|------------|
| Red | `[255, 0, 0]` |
| Green | `[0, 255, 0]` |
| Blue | `[0, 0, 255]` |
| Yellow | `[255, 255, 0]` |
| Purple | `[128, 0, 128]` |
| Cyan | `[0, 255, 255]` |
| Pink | `[255, 192, 203]` |
| Orange | `[255, 165, 0]` |
| White | `[255, 255, 255]` |
| Random | `[-1, -1, -1]` |

## Building

- Open the project in your preferred .NET IDE (e.g., Visual Studio, Rider, VS Code).
- Build the project. The output DLL and resources will be placed in the `build/` directory.
- The publish process will also create a zip file for easy distribution.

## Publishing

- Use the `dotnet publish -c Release` command to build and package your plugin.
- Distribute the generated zip file or the contents of the `build/publish` directory.
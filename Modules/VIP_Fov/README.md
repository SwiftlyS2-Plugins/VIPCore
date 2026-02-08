<div align="center">
  <img src="https://pan.samyyc.dev/s/VYmMXE" />
  <h2><strong>VIP_Fov</strong></h2>
  <h3>Allows VIP players to customize their Field of View.</h3>
</div>

<p align="center">
  <img src="https://img.shields.io/badge/build-passing-brightgreen" alt="Build Status">
  <img src="https://img.shields.io/github/downloads/aga/VIP_Fov/total" alt="Downloads">
  <img src="https://img.shields.io/github/stars/aga/VIP_Fov?style=flat&logo=github" alt="Stars">
  <img src="https://img.shields.io/github/license/aga/VIP_Fov" alt="License">
</p>

## Description

VIP_Fov is a VIPCore module that allows VIP players to customize their Field of View (FOV). Players can cycle through preset FOV values (90, 100, 110, 120) using the VIP menu.

## Installation

1. Place `VIP_Fov.dll` in `(swRoot)/plugins/VIP_Fov/`
2. Add the feature to your `vip_groups.jsonc` configuration (see below)
3. Restart the server or hot-reload the plugin

## VIP Group Configuration

Add the fov feature to your `vip_groups.jsonc` file:

```jsonc
{
  "vip_groups": {
    "Groups": {
      "GOLD": {
        "Values": {
          "vip.fov": 1  // 1 = enabled by default, 0 = disabled
        }
      },
      "SILVER": {
        "Values": {
          "vip.fov": 0  // Disabled by default
        }
      }
    }
  }
}
```

### Configuration Options

| Value | Description |
|-------|-------------|
| `1` | Feature enabled by default (player can cycle through FOV values) |
| `0` | Feature disabled by default |

### Available FOV Values

Players can cycle through: 90, 100, 110, 120

## Building

- Open the project in your preferred .NET IDE (e.g., Visual Studio, Rider, VS Code).
- Build the project. The output DLL and resources will be placed in the `build/` directory.
- The publish process will also create a zip file for easy distribution.

## Publishing

- Use the `dotnet publish -c Release` command to build and package your plugin.
- Distribute the generated zip file or the contents of the `build/publish` directory.
<div align="center">
  <img src="https://pan.samyyc.dev/s/VYmMXE" />
  <h2><strong>VIP_Zeus</strong></h2>
  <h3>Gives VIP players a Zeus x27 taser on spawn.</h3>
</div>

<p align="center">
  <img src="https://img.shields.io/badge/build-passing-brightgreen" alt="Build Status">
  <img src="https://img.shields.io/github/downloads/aga/VIP_Zeus/total" alt="Downloads">
  <img src="https://img.shields.io/github/stars/aga/VIP_Zeus?style=flat&logo=github" alt="Stars">
  <img src="https://img.shields.io/github/license/aga/VIP_Zeus" alt="License">
</p>

## Description

VIP_Zeus is a VIPCore module that gives VIP players a Zeus x27 taser on player spawn. The taser is only given if the player doesn't already have one.

## Installation

1. Place `VIP_Zeus.dll` in `(swRoot)/plugins/VIP_Zeus/`
2. Add the feature to your `vip_groups.jsonc` configuration (see below)
3. Restart the server or hot-reload the plugin

## VIP Group Configuration

Add the zeus feature to your `vip_groups.jsonc` file:

```jsonc
{
  "vip_groups": {
    "Groups": {
      "GOLD": {
        "Values": {
          "vip.zeus": 1  // 1 = enabled by default, 0 = disabled
        }
      },
      "SILVER": {
        "Values": {
          "vip.zeus": 0  // Disabled by default
        }
      }
    }
  }
}
```

### Configuration Options

| Value | Description |
|-------|-------------|
| `1` | Feature enabled by default (player receives Zeus on spawn) |
| `0` | Feature disabled by default |

## Building

- Open the project in your preferred .NET IDE (e.g., Visual Studio, Rider, VS Code).
- Build the project. The output DLL and resources will be placed in the `build/` directory.
- The publish process will also create a zip file for easy distribution.

## Publishing

- Use the `dotnet publish -c Release` command to build and package your plugin.
- Distribute the generated zip file or the contents of the `build/publish` directory.
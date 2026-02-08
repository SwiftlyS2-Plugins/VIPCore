<div align="center">
  <img src="https://pan.samyyc.dev/s/VYmMXE" />
  <h2><strong>VIP_AntiFlash</strong></h2>
  <h3>Provides VIP players with flashbang immunity.</h3>
</div>

<p align="center">
  <img src="https://img.shields.io/badge/build-passing-brightgreen" alt="Build Status">
  <img src="https://img.shields.io/github/downloads/aga/VIP_AntiFlash/total" alt="Downloads">
  <img src="https://img.shields.io/github/stars/aga/VIP_AntiFlash?style=flat&logo=github" alt="Stars">
  <img src="https://img.shields.io/github/license/aga/VIP_AntiFlash" alt="License">
</p>

## Description

VIP_AntiFlash is a VIPCore module that gives VIP players immunity to flashbang grenades. The immunity can be configured per VIP group with different modes: full immunity, teammates only, self-flash only, or a combination.

## Installation

1. Place `VIP_AntiFlash.dll` in `(swRoot)/plugins/VIP_AntiFlash/`
2. Add the feature to your `vip_groups.jsonc` configuration (see below)
3. Restart the server or hot-reload the plugin

## VIP Group Configuration

Add the antiflash feature to your `vip_groups.jsonc` file:

```jsonc
{
  "vip_groups": {
    "Groups": {
      "GOLD": {
        "Values": {
          "vip.antiflash": 0  // Full immunity (default)
        }
      },
      "SILVER": {
        "Values": {
          "vip.antiflash": 1  // Immune to teammates' flashbangs only
        }
      }
    }
  }
}
```

### Configuration Options

| Value | Mode | Description |
|-------|------|-------------|
| `0` | Full Immunity | Immune to all flashbangs (default behavior) |
| `1` | Teammates Only | Immune only to flashbangs thrown by teammates |
| `2` | Self Only | Immune only to your own flashbangs |
| `3` | Teammates + Self | Immune to both teammates' and your own flashbangs |

### Configuration Examples

```jsonc
{
  "vip_groups": {
    "Groups": {
      "PLATINUM": {
        "Values": {
          "vip.antiflash": 0  // Full immunity - no flashbangs will blind
        }
      },
      "GOLD": {
        "Values": {
          "vip.antiflash": 1  // Team-friendly - immune to teammates only
        }
      },
      "SILVER": {
        "Values": {
          "vip.antiflash": 2  // Self-protection - immune to own flashes only
        }
      }
    }
  }
}
```

## Building

- Open the project in your preferred .NET IDE (e.g., Visual Studio, Rider, VS Code).
- Build the project. The output DLL and resources will be placed in the `build/` directory.
- The publish process will also create a zip file for easy distribution.

## Publishing

- Use the `dotnet publish -c Release` command to build and package your plugin.
- Distribute the generated zip file or the contents of the `build/publish` directory.
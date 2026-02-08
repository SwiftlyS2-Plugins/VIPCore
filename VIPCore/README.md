<div align="center">
  <img src="https://pan.samyyc.dev/s/VYmMXE" />
  <h2><strong>VIPCore</strong></h2>
  <h3>Core VIP management system for SwiftlyS2 CS2 servers.</h3>
</div>

<p align="center">
  <img src="https://img.shields.io/badge/build-passing-brightgreen" alt="Build Status">
  <img src="https://img.shields.io/github/downloads/aga/VIPCore/total" alt="Downloads">
  <img src="https://img.shields.io/github/stars/aga/VIPCore?style=flat&logo=github" alt="Stars">
  <img src="https://img.shields.io/github/license/aga/VIPCore" alt="License">
</p>

## Description

VIPCore is the central VIP management plugin for SwiftlyS2. It provides database-backed VIP storage, group-based feature configuration, and an API for other plugins (VIP modules) to register features that VIP players can use.

## Features

- **Database-backed VIP storage** using FluentMigrator and Dapper
- **Group-based VIP system** - define different VIP tiers (Gold, Silver, etc.)
- **Modular feature system** - extend with custom VIP feature modules
- **Interactive VIP menu** - players access features via `!vip` command
- **Cookie-based preferences** - player feature states persist across sessions
- **Admin management** - add/remove VIPs via commands or admin menu
- **Hot-reload support** - configuration changes apply without restart

## Installation

1. Place `VIPCore.dll` in `(swRoot)/plugins/VIPCore/`
2. The API contract is distributed as a NuGet package: **`SwiftlyS2.VIPCore.Contract`** (use `Version="*"` for latest).
3. Configure `config.jsonc` and `vip_groups.jsonc` in the plugin config folder
4. Ensure the [Cookies](https://github.com/SwiftlyS2-Plugins/Cookies) plugin is installed
5. Restart the server

## Configuration Files

### config.jsonc
Main plugin configuration including database connection and server settings.

### vip_groups.jsonc
Define VIP groups and their features. Example:

```jsonc
{
  "vip_groups": {
    "Groups": {
      "GOLD": {
        "Values": {
          "vip.health": { "Health": 150 },
          "vip.armor": { "Armor": 100 },
          "vip.zeus": 1,
          "vip.bhop": { "Timer": 5.0, "MaxSpeed": 300.0 }
        }
      },
      "SILVER": {
        "Values": {
          "vip.health": { "Health": 120 },
          "vip.armor": { "Armor": 50 }
        }
      }
    }
  }
}
```

## Commands

| Command | Permission | Description |
|---------|-----------|-------------|
| `!vip` | Player | Opens the VIP feature menu |
| `vip_adduser <steamid> <group> <time>` | `vipcore.adduser` | Adds a player to a VIP group |
| `vip_deleteuser <steamid>` | `vipcore.deleteuser` | Removes a player's VIP status |
| `vip_manage` | `vipcore.manage` | Opens the VIP admin management menu |

## Building

```bash
dotnet build -c Release
```

Output will be in `build/` directory.

## Publishing

```bash
dotnet publish -c Release
```

## Dependencies

- [Cookies](https://github.com/SwiftlyS2-Plugins/Cookies) - Required for player preference persistence

## License

MIT License
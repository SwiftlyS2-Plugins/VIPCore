<div align="center">
  <img src="https://pan.samyyc.dev/s/VYmMXE" />
  <h2><strong>VIP_GoldMember</strong></h2>
  <h3>Grants VIP to players who have a specified DNS/tag in their name.</h3>
</div>

<p align="center">
  <img src="https://img.shields.io/badge/build-passing-brightgreen" alt="Build Status">
  <img src="https://img.shields.io/github/downloads/aga/VIP_GoldMember/total" alt="Downloads">
  <img src="https://img.shields.io/github/stars/aga/VIP_GoldMember?style=flat&logo=github" alt="Stars">
  <img src="https://img.shields.io/github/license/aga/VIP_GoldMember" alt="License">
</p>

## Description

VIP_GoldMember is a VIPCore module that automatically grants VIP status to players whose name contains a specified DNS string (e.g., `playername clan.com`). Unlike other VIP modules that provide features to existing VIPs, this module **creates** VIPs based on their name.

**Important:** This module is different from feature modules - it grants VIP status itself, rather than adding features to existing VIP groups.

## Features

- Automatically grants VIP to players whose name contains a specified DNS string
- Configurable VIP group and duration
- Periodic name checking to handle name changes mid-game
- Temporary VIP mode: VIP lasts only while the DNS is in the name (revoked when removed)
- Tracks grants and only revokes VIP that was granted by this module

## Installation

1. Place `VIP_GoldMember.dll` in `(swRoot)/plugins/VIP_GoldMember/`
2. Configure the `config.jsonc` file (see below)
3. Ensure the `VipGroup` you specify exists in your `vip_groups.jsonc`
4. Restart the server or hot-reload the plugin

## Configuration

The plugin auto-generates `config.jsonc` on first load in the plugin's config folder (**NOT** inside `vip_groups.jsonc`).

**Important:** The GoldMember configuration is a separate file from your VIP groups configuration. Do not place it inside `vip_groups.jsonc`.

Here's an example configuration:

```jsonc
{
  "GoldMember": {
    // The DNS/tag string to look for in player names
    "Dns": "example.com",

    // Which VIP group to grant (must match a group in vip_groups.jsonc)
    "VipGroup": "VIP",

    // Duration passed to GiveClientTemporaryVip
    // 0 = temporary (VIP only while DNS is in the name, revoked when removed)
    // Any other value = permanent grant using VIPCore's TimeMode (seconds/minutes/hours/days)
    "Duration": 0,

    // How often (in seconds) to re-check all players' names for the DNS
    "CheckIntervalSeconds": 10.0
  }
}
```

### Example Usage

If your DNS is `myserver.com` and you want to grant the `"Gold"` group:

```jsonc
{
  "GoldMember": {
    "Dns": "myserver.com",
    "VipGroup": "Gold",
    "Duration": 0,
    "CheckIntervalSeconds": 10.0
  }
}
```

With `Duration: 0`, a player named `"PlayerName myserver.com"` gets VIP instantly. If they remove the DNS from their name, VIP is revoked within 10 seconds (the check interval). If they disconnect, it's also revoked immediately.

## Required VIP Group Setup

Before using this module, ensure the `VipGroup` you specify exists in your `vip_groups.jsonc`:

```jsonc
{
  "vip_groups": {
    "Groups": {
      "Gold": {
        "Values": {
          // Add the features you want Gold members to have
          "vip.armor": { "Armor": 100 },
          "vip.health": { "Health": 150 },
          "vip.zeus": 1
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
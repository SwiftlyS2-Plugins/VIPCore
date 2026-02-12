<div align="center">
  <img src="https://pan.samyyc.dev/s/VYmMXE" />
  <h2><strong>VIP_Vampirism</strong></h2>
  <h3>Gives VIP players lifesteal on damage dealt to enemies.</h3>
</div>

<p align="center">
  <img src="https://img.shields.io/badge/build-passing-brightgreen" alt="Build Status">
  <img src="https://img.shields.io/github/downloads/aga/VIP_Vampirism/total" alt="Downloads">
  <img src="https://img.shields.io/github/stars/aga/VIP_Vampirism?style=flat&logo=github" alt="Stars">
  <img src="https://img.shields.io/github/license/aga/VIP_Vampirism" alt="License">
</p>

## Description

VIP_Vampirism is a VIPCore module that allows VIP players to heal themselves by a percentage of the damage they deal to enemies. The heal percentage is configurable per VIP group and is capped at the player's max health.

## Installation

1. Place `VIP_Vampirism.dll` in `(swRoot)/plugins/VIP_Vampirism/`
2. Add the feature to your `vip_groups.jsonc` configuration (see below)
3. Restart the server or hot-reload the plugin

## VIP Group Configuration

Add the vampirism feature to your `vip_groups.jsonc` file:

```jsonc
{
  "vip_groups": {
    "Groups": {
      "GOLD": {
        "Values": {
          "vip.vampirism": {
            "Percent": 25.0  // Lifesteal percentage (e.g., 25.0 for 25%)
          }
        }
      },
      "SILVER": {
        "Values": {
          "vip.vampirism": {
            "Percent": 15.0
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
| `Percent` | float | 0.0 | Percentage of damage dealt to heal (0.0 to 100.0)

## Building

- Open the project in your preferred .NET IDE (e.g., Visual Studio, Rider, VS Code).
- Build the project. The output DLL and resources will be placed in the `build/` directory.
- The publish process will also create a zip file for easy distribution.

## Publishing

- Use the `dotnet publish -c Release` command to build and package your plugin.
- Distribute the generated zip file or the contents of the `build/publish` directory.
<div align="center">
  <img src="https://pan.samyyc.dev/s/VYmMXE" />
  <h2><strong>VIP_Tag</strong></h2>
  <h3>Custom clan tags for VIP players.</h3>
</div>

<p align="center">
  <img src="https://img.shields.io/badge/build-passing-brightgreen" alt="Build Status">
  <img src="https://img.shields.io/github/downloads/aga/VIP_Tag/total" alt="Downloads">
  <img src="https://img.shields.io/github/stars/aga/VIP_Tag?style=flat&logo=github" alt="Stars">
  <img src="https://img.shields.io/github/license/aga/VIP_Tag" alt="License">
</p>

## Description

VIP_Tag is a VIPCore module that allows VIP players to have custom clan tags. Players can cycle through multiple tags defined for their VIP group.

## Installation

1. Place `VIP_Tag.dll` in `(swRoot)/plugins/VIP_Tag/`
2. Add the feature to your `vip_groups.jsonc` configuration (see below)
3. Restart the server or hot-reload the plugin

## VIP Group Configuration

Add the tag feature to your `vip_groups.jsonc` file:

```jsonc
{
  "vip_groups": {
    "Groups": {
      "GOLD": {
        "Values": {
          "vip.tag": [
            "[GOLD]",
            "[VIP]",
            "[ELITE]"
          ]
        }
      },
      "SILVER": {
        "Values": {
          "vip.tag": [
            "[SILVER]",
            "[VIP]"
          ]
        }
      }
    }
  }
}
```

### Configuration Options

| Format | Description |
|--------|-------------|
| `["tag1", "tag2", ...]` | Array of clan tags players can cycle through |

### How It Works

- Players can cycle through the available tags using the VIP menu
- Setting index 0 disables the tag
- The selected tag is saved via cookies and persists across sessions

## Building

- Open the project in your preferred .NET IDE (e.g., Visual Studio, Rider, VS Code).
- Build the project. The output DLL and resources will be placed in the `build/` directory.
- The publish process will also create a zip file for easy distribution.

## Publishing

- Use the `dotnet publish -c Release` command to build and package your plugin.
- Distribute the generated zip file or the contents of the `build/publish` directory.
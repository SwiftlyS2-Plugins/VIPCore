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

VIP_Tag is a VIPCore module that gives VIP players a chat tag, also set as their scoreboard clan tag. In the VIP menu the perk is called **Chat tag** and reads *Before your name in chat* (translation key `vip.tag.description`). Players pick one of their group's tags, or *No tag*, on the **Customize** page of the VIP menu (`!vip`); selecting the perk anywhere else in the VIP menu opens that page.

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
| `["tag1", "tag2", ...]` | Clan tags players can pick from |

### How It Works

- Players pick a tag, or *No tag*, on the VIP menu's Customize page
- A tag may use chat colour markup such as `[gold][PREMIUM]` (the tags listed in VIP_ChatColor's README); the menu lists it without the markup, in the colour most of it is drawn in. The scoreboard clan tag shows the text exactly as written, markup included
- Until a player picks, the first tag is used
- The selected tag is saved via cookies and persists across sessions

## Building

- Open the project in your preferred .NET IDE (e.g., Visual Studio, Rider, VS Code).
- Build the project. The output DLL and resources will be placed in the `build/` directory.
- The publish process will also create a zip file for easy distribution.

## Publishing

- Use the `dotnet publish -c Release` command to build and package your plugin.
- Distribute the generated zip file or the contents of the `build/publish` directory.
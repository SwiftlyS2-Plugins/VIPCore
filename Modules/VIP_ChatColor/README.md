<div align="center">
  <img src="https://pan.samyyc.dev/s/VYmMXE" />
  <h2><strong>VIP_ChatColor</strong></h2>
  <h3>Colored chat names for VIP players.</h3>
</div>

<p align="center">
  <img src="https://img.shields.io/badge/build-passing-brightgreen" alt="Build Status">
  <img src="https://img.shields.io/github/downloads/aga/VIP_ChatColor/total" alt="Downloads">
  <img src="https://img.shields.io/github/stars/aga/VIP_ChatColor?style=flat&logo=github" alt="Stars">
  <img src="https://img.shields.io/github/license/aga/VIP_ChatColor" alt="License">
</p>

## Description

VIP_ChatColor is a VIPCore module that gives VIP players a colored name in chat. Each VIP group can define a global name color as well as separate colors for CT and T players. Players can toggle the feature on/off from the VIP menu.

## Installation

1. Place `VIP_ChatColor.dll` in `(swRoot)/plugins/VIP_ChatColor/`
2. Add the feature to your `vip_groups.jsonc` configuration (see below)
3. Restart the server or hot-reload the plugin

## VIP Group Configuration

Add the chatcolor feature to your `vip_groups.jsonc` file:

```jsonc

"vip.chatcolor": {
  "Global": "[gold]",   // fallback when no team-specific color is set
  "CT": "[blue]",       // color for CT players (overrides Global)
  "T": "[orange]"       // color for T players (overrides Global)
}
```

### Configuration Options

| Field | Description |
|-------|-------------|
| `Global` | Color applied to all players in this group (fallback) |
| `CT` | Color applied only to CT players — overrides `Global` |
| `T` | Color applied only to T players — overrides `Global` |

- `CT` and `T` are optional. Leave them empty or omit them to fall back to `Global`.
- Set `Global` to an empty string to disable coloring for that group entirely.

### Supported Color Tags

| Tag | Tag | Tag |
|-----|-----|-----|
| `[default]` / `[/]` | `[white]` | `[darkred]` |
| `[lightpurple]` | `[green]` | `[olive]` |
| `[lime]` | `[red]` | `[gray]` / `[grey]` |
| `[yellow]` / `[lightyellow]` | `[silver]` / `[bluegrey]` | `[lightblue]` / `[blue]` |
| `[darkblue]` | `[purple]` / `[magenta]` | `[lightred]` |
| `[gold]` / `[orange]` | | |

### How It Works

- When a VIP player with the feature **enabled** sends a chat message, the original message is intercepted and a re-formatted version is broadcast with their name colored.
- Team chat (`(Team)` messages) is only delivered to players on the same team.
- Players can toggle the colored name on or off at any time via the VIP menu (`!vip`).

## Building

- Open the project in your preferred .NET IDE (e.g., Visual Studio, Rider, VS Code).
- Build the project. The output DLL and resources will be placed in the `build/` directory.
- The publish process will also create a zip file for easy distribution.

## Publishing

- Use the `dotnet publish -c Release` command to build and package your plugin.
- Distribute the generated zip file or the contents of the `build/publish` directory.

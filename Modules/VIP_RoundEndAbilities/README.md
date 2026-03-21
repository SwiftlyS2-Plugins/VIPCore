<div align="center">
  <img src="https://pan.samyyc.dev/s/VYmMXE" />
  <h2><strong>VIP_RoundEndAbilities</strong></h2>
  <h3>Applies custom speed and gravity modifiers to VIP players at round end.</h3>
</div>

<p align="center">
  <img src="https://img.shields.io/badge/build-passing-brightgreen" alt="Build Status">
  <img src="https://img.shields.io/github/downloads/aga/VIP_RoundEndAbilities/total" alt="Downloads">
  <img src="https://img.shields.io/github/stars/aga/VIP_RoundEndAbilities?style=flat&logo=github" alt="Stars">
  <img src="https://img.shields.io/github/license/aga/VIP_RoundEndAbilities" alt="License">
</p>

## Description

VIP_RoundEndAbilities is a VIPCore module that automatically applies speed and gravity modifiers to alive VIP players when a round ends. The modifiers are configurable per VIP group, allowing survivors to have enhanced movement abilities during the round end period.

## Installation

1. Place `VIP_RoundEndAbilities.dll` in `(swRoot)/plugins/VIP_RoundEndAbilities/`
2. Add the feature to your `vip_groups.jsonc` configuration (see below)
3. Restart the server or hot-reload the plugin

## VIP Group Configuration

Add the round end abilities feature to your `vip_groups.jsonc` file:

```jsonc
{
  "vip_groups": {
	"Groups": {
	  "GOLD": {
		"Values": {
		  "vip.vip.round_end_abilities": {
			"SpeedModifier": 2.0,
			"GravityModifier": 0.5
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
| `SpeedModifier` | float | 1.5 | Movement speed multiplier applied at round end |
| `GravityModifier` | float | 0.5 | Gravity multiplier applied at round end |

### Modifier Examples

**Speed Modifiers:** [0-9]
- `1.0` - Normal speed
- `1.5` - 50% faster movement (default)
- `2.0` - Double speed
- `3.0` - Triple speed
- `0.5` - Half speed (slower)

**Gravity Modifiers:** [0-9]
- `1.5` - Double gravity (heavier, lower jumps)
- `1.0` - Normal gravity
- `0.5` - Half gravity, higher jumps (default)
- `0.25` - Quarter gravity, moon-like jumps
- `0.1` - Very low gravity
- `0.0` - No gravity (players will float)

### Features

- **Round End Abilities**: Speed and gravity modifiers are applied automatically when the round ends
- **Alive Players Only**: Only affects VIP players who are still alive at round end
- **Per-Group Configuration**: Each VIP group can have its own custom modifier values
- **Toggle Support**: Feature can be enabled/disabled through VIPCore settings
- **Safe Application**: Validates player state before applying modifiers

## Building

- Open the project in your preferred .NET IDE (e.g., Visual Studio, Rider, VS Code).
- Build the project. The output DLL and resources will be placed in the `build/` directory.
- The publish process will also create a zip file for easy distribution.

## Publishing

- Use the `dotnet publish -c Release` command to build and package your plugin.
- Distribute the generated zip file or the contents of the `build/publish` directory.
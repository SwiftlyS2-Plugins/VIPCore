<div align="center">
  <h2><strong>VIP_NightVip</strong></h2>
  <h3>Grants VIP access during specific hours.</h3>
</div>

<p align="center">
  <img src="https://img.shields.io/badge/build-passing-brightgreen" alt="Build Status">
</p>

## Overview

This module grants temporary VIP status to all players on the server during a specified time window (e.g., from 8 PM to 8 AM). 

## Expected Configuration

This module uses a standalone configuration file located at `configs/plugins/VIPCore/Modules/NightVip.jsonc`.
It will be generated automatically on the first run.

```jsonc
{
  "VIPGroup": "VIP",                                        // The VIP group to assign players to
  "PluginStartTime": "20:00:00",                            // Time to start giving VIP
  "PluginEndTime": "08:00:00",                              // Time to stop giving VIP
  "Timezone": "UTC",                                        // Timezone for the check
  "CheckTimer": 10.0,                                       // How often to check for players (in seconds)
  "Tag": "[NightVIP]"
}
```

## Building

- Open the project in your preferred .NET IDE (e.g., Visual Studio, Rider, VS Code).
- Build the project. The output DLL and resources will be placed in the `build/` directory.
- The publish process will also create a zip file for easy distribution.

## Publishing

- Use the `dotnet publish -c Release` command to build and package your plugin.
- Distribute the generated zip file or the contents of the `build/publish` directory.
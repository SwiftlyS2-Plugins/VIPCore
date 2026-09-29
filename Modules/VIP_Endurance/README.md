# VIP Endurance for SwiftlyS2

Prevents weapon hits from slowing VIP players. This is a SwiftlyS2/VIPCore
port of the SourceMod module `VIP_Endurance` by R1KO.

## Requirements

- SwiftlyS2
- VIPCore

## Installation

1. Stop the server.
2. Copy the `VIP_Endurance` directory to:
   `game/csgo/addons/swiftlys2/plugins/`
3. Add the feature to the required group in:
   `game/csgo/addons/swiftlys2/configs/plugins/VIPCore/vip_groups.jsonc`
4. Start the server.

Example:

```jsonc
{
  "VIPGroups": {
    "vip": {
      "Features": {
        "vip.endurance": 1
      }
    }
  }
}
```

Use `1` to enable the feature by default or `0` to let the player enable it
from the VIP menu.

## Translations

The package includes English, Russian, Ukrainian, and Finnish translations.
Translation files are located in:

`VIP_Endurance/resources/translations/`
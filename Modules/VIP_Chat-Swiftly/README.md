# VIP Chat v1.0.1 — SwiftlyS2 / VIPCore

A port of the VIP portion from `cs2-chat-processor-modules-main` (original author Pisex; the source states GPL).
The Metamod Admin and Personal modules are **not included** in this package. This is a separate VIPCore-managed module, not a converted Metamod `.so`. It does not replace VIPCore and does not modify its database.

## Fixes in v1.0.1

* Removed the use of `Scheduler.Delay()` for menu transitions. It now uses two `NextTick()` calls instead.
  The module no longer creates or disposes of a timer `CancellationTokenSource`; this fixes the cause of the `ObjectDisposedException` that could occur after the callback was executed.
* Colors are converted using the standard `Helper.Colored()`, as in `VIP_ChatColor` from the referenced source. Color bytes are placed in `Messagename` (the format string), rather than in the substituted name/text values.
  Both legacy `{GREEN}` and Swiftly tags such as `[green]` are supported.
* The player name and message remain `%s1` / `%s2` parameters, while the prefix is passed as `%s4`.
  Player-provided strings are not passed through `Colored()` and are not interpreted as templates.
* UI operations deferred until the next tick are cancelled when the module is unloaded or its generation changes.

### Updating from v1.0.0

Stop the server, replace the files in `addons/swiftlys2/plugins/VIP_Chat` with the files from the installation archive, then start the server. A full restart is recommended to remove any remaining timers from the old version.

Do not delete `config.jsonc` or `player_settings.json`, and do not replace the VIPCore configuration: selected prefixes/colors and permissions are preserved.

In `list` mode, colors must be selected through `!vipchat`.

For testing, select the name color **Lime**, the text color **Green**, and the prefix color **Purple**.

## Features

* Menu: prefix, name color, text color, prefix color.
* Prefix: `list`, `custom`, a fixed string, or `""` (disabled).
* Colors: `list`, a fixed color (`{GREEN}` etc.), or `""` (disabled).
* Each formatting option can be disabled; disabled values remain saved after reconnecting.
* For `custom` mode: the next regular chat message becomes the prefix and is not published.
  `!cancel` / `/cancel` cancels the input; the default timeout is 60 seconds.
  If the timeout expires, the next message is published normally. Other commands are not saved as prefixes.
* Settings are stored by SteamID in a dedicated JSON file. The module does not require the Cookies plugin for these settings.
* When the player's group changes, the current restrictions are applied: a custom prefix is not transferred to `list` mode unless it matches the allowed list; colors outside the allowed list are not applied.
* Uses the standard `CUserMessageSayText2`: the message format is modified and, when a prefix is present, its fourth parameter is modified as well. Messages are not sent again; the original recipients and channel are preserved.
  Team/death/spectator message markers are reproduced using the module's `Language`, rather than the individual client's language.
  The team-chat location remains the `%s3` parameter.
  Unknown `Cstrike_Chat_*` variants are left unchanged and receive no formatting.

## Requirements

* SwiftlyS2 with an API compatible with `SwiftlyS2.CS2 1.4.12`, .NET 10.
* VIPCore v1.1.3 from the attached archive or a compatible `VIPCore.Api.v1` with `GetFeatureValue<T>`.
* The built-in SwiftlyS2 menu system must be enabled. T3Menu is not required and is not included in the package.
* Metamod Chat Processor, Utils, and Menus are not required for this module.

Compiled with `VIPCore.Contract.dll` **specifically from the attached VIPCore-v1.1.3.zip**.

Do not copy the contract from the source code over the contract used by the running VIPCore.

If your SwiftlyS2 version is older than the specified API and the module does not load, first check the version and loader logs.

## Installation

1. Extract the installation ZIP into the server's `game/csgo` directory.
   The result should be:
   `game/csgo/addons/swiftlys2/plugins/VIP_Chat/VIP_Chat.dll`.

2. In `game/csgo/addons/swiftlys2/configs/plugins/VIPCore/vip_groups.jsonc`, add the following object to the `Values` section of the **required groups**, while keeping the other modules:

```json
"vip.chat": {
  "Prefix": "list",
  "NameColor": "list",
  "TextColor": "list",
  "PrefixColor": "list"
}
```

3. Restart the server. Chat settings can be opened with `!vipchat` / `/vipchat` (console command `sw_vipchat`) or through the **Chat Settings** item in `!vip`.

4. The module configuration is created automatically:
   `game/csgo/addons/swiftlys2/configs/plugins/VIP_Chat/config.jsonc`.

   This file contains the prefix/color lists, language (`ru`, `uk`, `en`), player freeze option, maximum prefix length, and input timeout. After changing the module configuration, restart/reload the module.

5. Player settings:
   `game/csgo/addons/swiftlys2/data/VIP_Chat/player_settings.json`.

   The actual data directory is printed in the log when the module loads. Keep this file when updating.

**Do not replace the entire `vip_groups.jsonc` with the example from the package:** the example only demonstrates the structure. Your existing groups, other modules, and permissions must remain intact.

If `vip.chat` is not present in a group, that group has no access.

Do not use `"vip.chat": true`; an object with parameters is required.

The installation archive does not contain a ready-made global configuration, so it does not overwrite existing settings.

Old values from `Chat_Prefix`, `Chat_NameColor`, `Chat_TextColor`, and `Chat_PrefixColor` must be manually transferred to the fields of the new object.

Old Metamod cookies are not imported automatically.

## Menu Controls

Built-in SwiftlyS2 menu controls:

* `W` — Up
* `S` — Down
* `E` — Select
* `A` — Back
* `R` — Close

On the main screen, `A` returns to the VIP menu. Submenus also contain a **Back** option.

If the server is configured to use a different menu input mode, the global SwiftlyS2 menu settings may take priority.

This is a functional equivalent of the menu shown in the screenshot, not a pixel-perfect copy of Metamod Menus.

## Configuration Examples

Free prefix input with colors selected from lists:

```json
"vip.chat": {
  "Prefix": "custom",
  "NameColor": "list",
  "TextColor": "list",
  "PrefixColor": "list"
}
```

Fixed values (the player can enable/disable them but cannot replace them with other values):

```json
"vip.chat": {
  "Prefix": "[VIP]",
  "NameColor": "{LIME}",
  "TextColor": "{GREEN}",
  "PrefixColor": "{LIGHTPURPLE}"
}
```

Prefix only, without access to colors:

```json
"vip.chat": {
  "Prefix": "list",
  "NameColor": "",
  "TextColor": "",
  "PrefixColor": ""
}
```

For Ukrainian menu labels, set `"Language": "uk"`. Ukrainian color names are provided in `examples/config.uk.jsonc`.

Color values are codes; element names are arbitrary labels.

The following original tokens are supported:

`{TEAM}`, `{DEFAULT}`, `{RED}`, `{DARKRED}`, `{LIGHTRED}`, `{PURPLE}`, `{LIGHTPURPLE}`, `{GREEN}`, `{LIME}`, `{LIGHTGREEN}`, `{GRAY}`, `{OLIVE}`, `{LIGHTOLIVE}`, `{BLUE}`, `{LIGHTBLUE}`, `{GRAYBLUE}`.

`{TEAM}` and `{LIGHTPURPLE}` use the CS2 `0x03` code; their appearance depends on how the game processes the message.

HTML/RGB colors are not supported in the in-game chat.

## Server Testing

Compilation and standalone tests do not replace testing on an actual CS2 server.

On a live server, verify:

* VIP users with access: the menu item appears, all four settings work, and prefixes/colors can be selected.
* Regular players: no access and no formatting is applied.
* `say` / `say_team`: opposing teams do not receive team chat and there are no duplicate messages.
* Dead/spectator players: original message templates and message availability are preserved.
* `custom`: input and cancellation messages are not published; prefixes are limited by length and cannot inject colors/HTML.
* Reconnecting and restarting the server: selected and disabled values remain saved.
* Removing VIP / changing groups: previous permissions do not remain active.
* Reloading the module: no duplicate hooks are created and the menu closes normally.

Do not run another module that formats the name/text of the same chat message at the same time: the result depends on hook order.

If the old Metamod `CP_VIP` is also active, disable it.

Third-party Chat Processor/Admin/Personal modules interact according to their own rules; their compatibility with CS2 has not been tested here.

Do not enable `VIP_ChatColor` and `VIP_Chat` simultaneously.

## Building from Source

```sh
dotnet build VIP_Chat/VIP_Chat.csproj -c Release
dotnet run --project tests/tests.csproj -c Release
```

.NET 10 SDK and access to NuGet are required.

The compilation contract is located in `VIP_Chat/lib`; it is not included in the installation directory because VIPCore exports it itself.

Original logic author: Pisex.

The port retains the GPL attribution of the original module; see `NOTICE.txt`.

The VIP portion of the original C++ module is included with the source package.

Studied color implementation:
https://github.com/SwiftlyS2-Plugins/VIPCore/blob/master/Modules/VIP_ChatColor/src/VIP_ChatColor.cs

Metamod Chat Processor format implementation additionally reviewed:
https://github.com/Pisex/cs2-chat-processor

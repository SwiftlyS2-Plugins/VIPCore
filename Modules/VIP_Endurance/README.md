# VIP Endurance v1.2.0 — SwiftlyS2 / VIPCore

A reworked port of VIP_Endurance by Pisex:
https://github.com/Pisex/cs2-vip-modules/tree/main/VIP_Endurance

The original Metamod module restores `m_flVelocityModifier` to `1.0` after the game frame if the value is below `1.0` and the VIP feature is enabled.

This version uses the same parameter, but the correction is also performed immediately after damage is received and before movement processing.

Weapon/damage type is not checked: the rule is the same for both bullet hits and HE explosive damage.

## What Was Fixed

* Removed the `IPlayer` cache and slot-based access flag. The current pawn and live VIPCore state are used instead.
* The reset after `TakeDamage.Post` now applies to the **player who received the damage**, not the player who dealt it.
* Added additional correction points: `ProcessUsercmds.Pre`, `ProcessMovement.Pre`, `PostThink.Post`, and `OnTick` after the game frame.
* After setting `VelocityModifier = 1.0`, `VelocityModifierUpdated()` is called for network synchronization.
* Both `Endurance` and the legacy `vip.endurance` keys are supported. Use only one key.
* Added the `!endurancecheck` / `/endurancecheck` / `sw_endurancecheck` command.
* Fixed the incorrect configuration schema in the v1.1.0 instructions: `Values` is required, **NOT** `Features`.

Damage, HP, weapon movement speed, MoveType, stamina, jumping, knockback, and camera animations are not modified.

This is **not** god mode, anti-flash, or an increase to normal walking speed.

The module does not modify global cvars and does not enable the feature for all players.

As with the original, it restores any reduced `VelocityModifier` back to `1.0` for VIP players with the feature enabled.

Another module that intentionally uses the same field to slow down VIP players may conflict with this module.

## Requirements

* SwiftlyS2 with an API compatible with `1.4.12`
* .NET 10
* VIPCore with `VIPCore.Api.v1`

The build uses the contract from the previously provided VIPCore v1.1.3 package.

Game hooks/signatures are provided by your SwiftlyS2 installation.

If signature-related errors occur, you need a working SwiftlyS2 version compatible with the installed CS2 version.

The module does not use hardcoded memory addresses.

## Installation / Update

1. Stop the server.

2. Extract the installation archive into `game/csgo`, replacing the contents of:

   `addons/swiftlys2/plugins/VIP_Endurance/`

   Do not leave another copy of the DLL in a separate plugin directory.

3. In:

   `game/csgo/addons/swiftlys2/configs/plugins/VIPCore/vip_groups.jsonc`

   add the following to the **`Values` section of your active VIP group**:

```json
"Endurance": 1
```

Full structure example (**DO NOT replace your entire file with this example**):

```json
{
  "vip_groups": {
    "Groups": {
      "YOUR_EXISTING_GROUP": {
        "Weight": 10,
        "Values": {
          "Endurance": 1
        }
      }
    }
  }
}
```

The previous `VIPGroups > vip > Features` example was incorrect for this version of VIPCore.

If you previously added Endurance there, move it to the `Values` section of your existing active group.

Alternative key for older configurations:

```json
"vip.endurance": 1
```

Use it instead of `"Endurance": 1`.

**Do not specify both keys.**

If both are present, the `Endurance` state takes priority, including when it is disabled.

4. Start the server and check the **Endurance** option in `!vip`. It should be enabled.

   VIPCore may restore a previously saved disabled state even when the configuration contains `1`.

5. While moving, take several bullet hits and HE damage. Then execute:

```text
!endurancecheck
```

The output is also duplicated to the player's console, making it convenient to copy.

VIPCore settings, the VIP database, and cookies are not included in the installation archive and are not overwritten.

## Diagnostics

The `!endurancecheck` command displays only the sender's state:

* `API`, `Ready`, `Registered`, `VIP`, active `Group`;
* `Endurance` and `vip.endurance` states, plus the resulting `Effective` state;
* current `VelocityModifier`;
* number of corrections for the current session, the last corrected value, and the correction point;
* global counters for calls to the module's game hooks.

`Effective=True` means that access is active.

`NoAccess` for both keys means that the feature is not correctly configured in the active group or VIPCore has not loaded the player yet.

`Disabled` for the primary key means that Endurance must be enabled through the menu.

`Ready=False` means that the core is not ready yet or its loading failed.

After an actual slowing hit while access is active, `Your resets` should increase.

Under normal conditions:

```text
VelocityModifier=1.000
```

If movement is still being slowed, send the output of `!endurancecheck` **after taking a bullet/HE hit**, together with the module loading log.

This makes it possible to determine whether:

* the feature is enabled;
* the native hooks are being called;
* the field was actually reduced;
* another mechanism/plugin is responsible for the movement slowdown.

Version numbers alone do not prove that the hooks are functioning correctly.

## Testing and Limitations

The project has been built and standalone access/numerical threshold checks have been performed.

The new version has **not been tested on a live CS2 server**.

Live-server testing is required for:

* bullet damage;
* HE damage;
* enabling/disabling the feature;
* changing VIP groups;
* respawning;
* reconnecting;
* testing a regular non-VIP player.

It cannot be claimed that every visual sensation of movement slowdown has been eliminated without such live testing.

## Building from Source

```sh
dotnet build VIP_Endurance/VIP_Endurance.csproj -c Release
dotnet run --project tests/tests.csproj -c Release
```

.NET 10 SDK and access to NuGet are required.

`lib/VIPCore.Contract.dll` is required only for compilation and is not included in the installation directory because VIPCore exports the contract itself.

The original C++ VIP_Endurance implementation is included in `upstream` for comparison.

Original author: Pisex.

The original plugin specifies its license as `Public`.

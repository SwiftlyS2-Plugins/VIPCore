<div align="center">
  <img src="https://pan.samyyc.dev/s/VYmMXE" />
  <h2><strong>VIPCore</strong></h2>
  <h3>A comprehensive VIP management system for Counter-Strike 2 servers running SwiftlyS2.</h3>
</div>

<p align="center">
  <img src="https://img.shields.io/badge/build-passing-brightgreen" alt="Build Status">
  <img src="https://img.shields.io/github/downloads/SwiftlyS2-Plugins/VIPCore/total" alt="Downloads">
  <img src="https://img.shields.io/github/stars/SwiftlyS2-Plugins/VIPCore?style=flat&logo=github" alt="Stars">
  <img src="https://img.shields.io/github/license/SwiftlyS2-Plugins/VIPCore" alt="License">
</p>

## Overview

VIPCore is a VIP management framework for SwiftlyS2 CS2 servers.
It provides database-backed VIP groups, a shared API for feature modules, and a menu-driven experience so VIP players can enable/disable features that your server grants them.

## Requirements

- SwiftlyS2 for Counter-Strike 2
- A database supported by VIPCore migrations (VIPCore uses FluentMigrator + Dapper)
- The [Cookies](https://github.com/SwiftlyS2-Plugins/Cookies) plugin (used to persist per-player feature preferences)

## Features

- **Database-backed VIP management** with FluentMigrator + Dapper
- **Modular feature system** - easily extend with custom VIP features
- **Group-based permissions** with per-feature configuration
- **Cookie system** for saving player preferences
- **Interactive menu system** for VIP players
- **Hot-reload support** for configuration changes

## Installation

1. Copy the published `VIPCore` plugin folder to your server:
   `(swRoot)/plugins/VIPCore/`
2. Ensure the plugin has its `resources/` folder alongside `VIPCore.dll`.
3. Ensure `VIPCore.Contract.dll` is present at:
   `(swRoot)/plugins/VIPCore/resources/exports/`
4. Configure `vip_groups.jsonc` and `config.jsonc` in your SwiftlyS2 configs folder.
5. Restart the server.

## Commands

| Command | Permission | Description |
| :--- | :--- | :--- |
| `vip` | Player | Opens the VIP menu for the current player. |
| `vip_manage` | `vipcore.manage` | Opens the VIP management menu (in-game). |
| `vip_adduser <steamid> <group> <time>` | `vipcore.adduser` | Adds a SteamID to a VIP group for a duration based on `TimeMode`. Use `0` for permanent. |
| `vip_deleteuser <steamid>` | `vipcore.deleteuser` | Removes VIP status for a SteamID. |

Console usage: SwiftlyS2 console commands are typically exposed with the `sw_` prefix (for example `sw_vip`, `sw_vip_adduser`, etc.).

## Configuration

VIPCore uses two configuration files located in your SwiftlyS2 configs folder.

### Main Settings — `config.jsonc`

Controls how VIPCore operates on your server.

```jsonc
{
  "vip": {
    "Delay": 2.0,                      // Wait time in seconds before applying VIP features
    "DatabaseConnection": "default",   // Database connection name (from SwiftlyS2 database config)
    "TimeMode": 3,                     // Time unit for VIP duration: 0=seconds, 1=minutes, 2=hours, 3=days
    "VipLogging": true                 // Enable detailed logs (useful for troubleshooting)
  }
}
```

**Settings explained:**

| Setting | What it does | Recommended |
| :--- | :--- | :--- |
| `Delay` | How many seconds to wait after spawn before giving VIP features | `1.0` to `5.0` |
| `DatabaseConnection` | Which database to use for storing VIP data (must exist in SwiftlyS2 config) | `"default"` |
| `TimeMode` | Time format when adding VIPs via commands | `3` (days) |
| `VipLogging` | Show detailed logs for debugging VIP system issues | `true` for testing, `false` for production |

---

### VIP Groups — `vip_groups.jsonc`

Defines VIP groups and what features each group receives.

**Simple example** (on/off features only):

```jsonc
{
  "vip_groups": {
    "Groups": {
      "VIP": {
        "Weight": 10,          // Priority (higher number = more important)
        "Values": {
          "vip.zeus": 1,       // 1 = enabled, 0 = disabled
          "vip.bhop": 1,
          "vip.antiflash": 1
        }
      }
    }
  }
}
```

**Advanced example** (features with custom settings):

```jsonc
{
  "vip_groups": {
    "Groups": {
      "VIP": {
        "Weight": 10,
        "Values": {
          "vip.armor": {
            "Armor": 100       // Give 100 armor points on spawn
          },
          "vip.health": {
            "Health": 120      // Give 120 HP on spawn
          },
          "vip.bhop": {
            "Timer": 5.0,      // Activate bhop 5 seconds after round start
            "MaxSpeed": 300.0  // Maximum bhop speed
          }
        }
      },
      "PREMIUM": {
        "Weight": 20,          // Higher weight = overrides "VIP" if player has both
        "Values": {
          "vip.armor": {
            "Armor": 150
          },
          "vip.health": {
            "Health": 150
          },
          "vip.bhop": {
            "Timer": 3.0,
            "MaxSpeed": 350.0
          }
        }
      }
    }
  }
}
```

**How it works:**

- **Group names** (`"VIP"`, `"PREMIUM"`, etc.) can be anything — use them when adding VIPs via commands
- **Weight** = priority level. If a player has multiple VIP groups, the highest weight wins
- **Values** = features granted to this group
- **Feature keys** (`"vip.armor"`, `"vip.bhop"`, etc.) must match installed module names

**Feature value types:**

| Type | Example | Use when |
| :--- | :--- | :--- |
| Simple toggle | `"vip.zeus": 1` | Feature has no extra settings (just on/off) |
| With settings | `"vip.armor": { "Armor": 100 }` | Feature needs customization (amounts, timers, etc.) |

> [!TIP]
> **Module-specific configuration:** Each module has its own configuration options and setup instructions. See the [Modules](Modules/) directory for detailed documentation on each module's settings.

## Included Modules

This repository ships several optional VIP modules (each one is a standalone SwiftlyS2 plugin that registers features into VIPCore):

| Module | Description |
| :--- | :--- |
| `VIP_AntiFlash` | Gives flashbang immunity to VIP players |
| `VIP_Armor` | Gives armor and helmet on spawn |
| `VIP_Bhop` | Enables bunnyhop (auto-jump) for players |
| `VIP_DoubleJump` | Allows players to jump twice in mid-air |
| `VIP_FastDefuse` | Reduces bomb defuse time |
| `VIP_FastPlant` | Reduces bomb plant time |
| `VIP_FastReload` | Gives instant weapon reloads |
| `VIP_Fov` | Allows customizable field of view |
| `VIP_GoldMember` | Auto-grants VIP to players with specific name tags |
| `VIP_Health` | Gives increased health on spawn |
| `VIP_Items` | Gives configured weapons/items on spawn |
| `VIP_KillScreen` | Applies screen effect on kills |
| `VIP_Money` | Gives money to VIP players |
| `VIP_NightVip` | Grants VIP access during specific hours |
| `VIP_NoFallDamage` | Prevents fall damage |
| `VIP_RainbowModel` | Cycles player model render color (rainbow effect) |
| `VIP_SmokeColor` | Custom smoke grenade colors |
| `VIP_Tag` | Custom clan tags for players |
| `VIP_Vampirism` | Heals player by percentage of damage dealt |
| `VIP_Zeus` | Gives Zeus x27 taser on spawn |

## Creating VIP Module Plugins

VIP modules are standalone SwiftlyS2 plugins that extend VIPCore with custom features (e.g., anti-flash, bhop, FOV, armor, etc.).  
The API contract is distributed as a NuGet package: **`SwiftlyS2.VIPCore.Contract`** (use `Version="*"` for latest).

### Prerequisites

- [SwiftlyS2.CS2](https://www.nuget.org/packages/SwiftlyS2.CS2) NuGet package
- [SwiftlyS2.VIPCore.Contract](https://www.nuget.org/packages/SwiftlyS2.VIPCore.Contract) NuGet package

> **Required dependency:** The [Cookies](https://github.com/SwiftlyS2-Plugins/Cookies) plugin must be installed on the server. VIPCore uses it to persist player feature preferences (enable/disable states) across sessions.

### Module Structure

```
VIP_YourFeature/
├── src/
│   └── VIP_YourFeature.cs
├── resources/
│   ├── templates/
│   │   └── template.jsonc        (optional — default config for your feature)
│   └── translations/
│       └── en.jsonc               (module-specific translations)
├── VIP_YourFeature.csproj
```

---

### Step 1 — Create the Project File

Create `VIP_YourFeature.csproj` and add the `SwiftlyS2.VIPCore.Contract` NuGet package:

```xml
<ItemGroup>
  <PackageReference Include="SwiftlyS2.VIPCore.Contract" Version="*" ExcludeAssets="runtime" PrivateAssets="all" />
</ItemGroup>
```

> **Note:** Use `ExcludeAssets="runtime" PrivateAssets="all"` because VIPCore is already loaded on the server at runtime — you only need the contract at compile time.

---

### Step 2 — Write the Plugin Code

Create `src/VIP_YourFeature.cs`:

```csharp
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.Plugins;
using SwiftlyS2.Shared.Misc;
using VIPCore.Contract;
using Microsoft.Extensions.Logging;

namespace VIP_YourFeature;

[PluginMetadata(Id = "VIP_YourFeature", Version = "1.0.0", Name = "[VIP] YourFeature", Author = "YourName")]
public class VIP_YourFeature : BasePlugin
{
    private const string FeatureKey = "vip.yourfeature";
    private IVipCoreApiV1? _vipApi;
    private bool _isFeatureRegistered;

    public VIP_YourFeature(ISwiftlyCore core) : base(core) { }

    // ── 1. Retrieve the VIPCore shared interface ──────────────────────
    public override void UseSharedInterface(IInterfaceManager interfaceManager)
    {
        _vipApi = null;
        _isFeatureRegistered = false;

        try
        {
            if (interfaceManager.HasSharedInterface("VIPCore.Api.v1"))
                _vipApi = interfaceManager.GetSharedInterface<IVipCoreApiV1>("VIPCore.Api.v1");

            RegisterWhenReady();
        }
        catch (Exception ex)
        {
            Core.Logger.LogWarning("[VIP_YourFeature] Failed to get VIPCore API: {Message}", ex.Message);
        }
    }

    // ── 2. Plugin Load ────────────────────────────────────────────────
    public override void Load(bool hotReload)
    {
        // Hook game events, register listeners, etc.
        RegisterWhenReady();
    }

    // ── 3. Register feature (respects VIPCore readiness) ──────────────
    private void RegisterWhenReady()
    {
        if (_vipApi == null) return;

        if (_vipApi.IsCoreReady())
            RegisterVipFeatures();
        else
            _vipApi.OnCoreReady += RegisterVipFeatures;
    }

    private void RegisterVipFeatures()
    {
        if (_vipApi == null || _isFeatureRegistered) return;

        _vipApi.RegisterFeature(FeatureKey, FeatureType.Toggle, (player, state) =>
        {
            Core.Scheduler.NextTick(() =>
            {
                player.SendMessage(MessageType.Chat, $"YourFeature: {state}");
            });
        },
        displayNameResolver: p => Core.Translation.GetPlayerLocalizer(p)["vip.yourfeature"]);

        _isFeatureRegistered = true;

        // Subscribe to VIPCore events
        _vipApi.OnPlayerSpawn += OnVipPlayerSpawn;
        _vipApi.PlayerLoaded += OnVipPlayerLoaded;
    }

    // ── 4. Feature logic ──────────────────────────────────────────────
    private void OnVipPlayerSpawn(IPlayer player)
    {
        if (_vipApi == null || !player.IsValid || player.IsFakeClient) return;
        if (_vipApi.GetPlayerFeatureState(player, FeatureKey) != FeatureState.Enabled) return;

        // Apply your feature effect here
    }

    private void OnVipPlayerLoaded(IPlayer player, string group)
    {
        // Called when a VIP player's data is loaded from the database
    }

    // ── 5. Cleanup ────────────────────────────────────────────────────
    public override void Unload()
    {
        if (_vipApi != null)
        {
            _vipApi.OnCoreReady -= RegisterVipFeatures;
            _vipApi.OnPlayerSpawn -= OnVipPlayerSpawn;
            _vipApi.PlayerLoaded -= OnVipPlayerLoaded;

            if (_isFeatureRegistered)
                _vipApi.UnregisterFeature(FeatureKey);
        }
    }
}
```

---

### Step 3 — Create Translation Files

Each module manages its own translations independently. VIPCore's translation file only contains core keys (menu title, VIP status messages, etc.) — **module-specific display names and messages must live in the module's own translation file.**

Create `resources/translations/en.jsonc`:

```jsonc
{
  "vip.yourfeature": "Your Feature",
  "yourfeature.activated": "Your feature has been activated!",
  "yourfeature.deactivated": "Your feature has been deactivated."
}
```

The key `"vip.yourfeature"` is what gets displayed in the VIP menu as the feature name. It is resolved per-player via the `displayNameResolver` callback you pass during registration:

```csharp
displayNameResolver: p => Core.Translation.GetPlayerLocalizer(p)["vip.yourfeature"]
```

This uses SwiftlyS2's built-in translation system — `GetPlayerLocalizer(player)` automatically resolves the correct language file based on the player's language preference, and it reads from **your module's** `resources/translations/` folder (not VIPCore's).

To support additional languages, add more translation files:

```
resources/translations/
├── en.jsonc    (English — required)
├── de.jsonc    (German)
├── fr.jsonc    (French)
└── ru.jsonc    (Russian)
```

---

### Step 4 — Configure the VIP Group

Add your feature key to `vip_groups.jsonc` on the server:

```jsonc
{
  "vip_groups": {
    "Groups": {
      "VIP": {
        "Values": {
          "vip.yourfeature": 1  // 1 = enabled by default, 0 = disabled
        }
      }
    }
  }
}
```

For features with extra settings, nest an object instead of a simple value:

```jsonc
{
  "vip_groups": {
    "Groups": {
      "VIP": {
        "Values": {
          "vip.bhop": {
            "Timer": 5.0,
            "MaxSpeed": 300.0
          }
        }
      }
    }
  }
}
```

Then read it in your module with a config class:

```csharp
public class YourFeatureConfig
{
    public float Timer { get; set; } = 5.0f;
    public float MaxSpeed { get; set; } = 300.0f;
}

// In your feature logic:
var config = _vipApi.GetFeatureValue<YourFeatureConfig>(player, FeatureKey);
```

---

### Step 5 — Build & Deploy

```bash
dotnet build -c Release
```

Copy the build output to your server:

```
(swRoot)/plugins/VIP_YourFeature/
├── VIP_YourFeature.dll
└── resources/
    └── ...
```

> **Important:** `VIPCore.Contract.dll` must already be present in `(swRoot)/plugins/VIPCore/resources/exports/`. It ships with VIPCore — you do **not** need to include it in your module's output.

Restart the server (or hot-reload if supported).

---

### Key Concepts

#### Shared Interface Retrieval
```csharp
var api = interfaceManager.GetSharedInterface<IVipCoreApiV1>("VIPCore.Api.v1");
```
- Must be done in the `UseSharedInterface()` override
- Always check `HasSharedInterface()` first to avoid exceptions if VIPCore is not loaded

#### Feature Registration Timing
```csharp
if (_vipApi.IsCoreReady())
    RegisterVipFeatures();
else
    _vipApi.OnCoreReady += RegisterVipFeatures;
```
VIPCore loads its database and config asynchronously. Always check `IsCoreReady()` before registering, and subscribe to `OnCoreReady` as a fallback.

#### Feature Key Naming
- Format: `"vip.featurename"` (lowercase, dot-separated)
- Must match the key used in `vip_groups.jsonc`

#### Feature Types
| Type | Description |
|------|-------------|
| `FeatureType.Toggle` | On/off toggle (most common) |
| `FeatureType.Selectable` | Cycles through options when selected in menu |
| `FeatureType.Hide` | Active feature with no menu entry |

#### Main Thread Safety
Game API calls must run on the main thread. Always wrap them in `NextTick`:
```csharp
Core.Scheduler.NextTick(() =>
{
    player.SendMessage(MessageType.Chat, "Hello!");
});
```

#### Player Cookies
Persist per-player preferences across sessions:
```csharp
_vipApi.SetPlayerCookie(player, "vip.yourfeature.value", 120);
int saved = _vipApi.GetPlayerCookie<int>(player, "vip.yourfeature.value");
```

---

### Available API Methods

```csharp
// Feature Management
void RegisterFeature(string featureKey, FeatureType type, Action<IPlayer, FeatureState>? onSelectItem, Func<IPlayer, string>? displayNameResolver = null);
void UnregisterFeature(string featureKey);
IEnumerable<string> GetAllRegisteredFeatures();

// Player State
bool IsClientVip(IPlayer player);
bool PlayerHasFeature(IPlayer player, string featureKey);
FeatureState GetPlayerFeatureState(IPlayer player, string featureKey);
void SetPlayerFeatureState(IPlayer player, string featureKey, FeatureState newState);
string GetClientVipGroup(IPlayer player);
string[] GetVipGroups();

// Feature Config (reads from group config JSON and binds to your class)
T? GetFeatureValue<T>(IPlayer player, string featureKey) where T : class, new();

// Player Cookies
T GetPlayerCookie<T>(IPlayer player, string key);
void SetPlayerCookie<T>(IPlayer player, string key, T value);

// Global Toggle
void DisableAllFeatures();
void EnableAllFeatures();

// Core State
bool IsCoreReady();

// Events
event Action? OnCoreReady;
event Action<IPlayer, string>? PlayerLoaded;
event Action<IPlayer, string>? PlayerRemoved;
event Action<IPlayer>? OnPlayerSpawn;
event Func<IPlayer, string, FeatureState, FeatureType, bool?>? OnPlayerUseFeature;
```

### Feature State Values

```csharp
public enum FeatureState
{
    Enabled = 0,   // Feature is active
    Disabled = 1,  // Feature is inactive (player toggled off)
    NoAccess = 2   // Player doesn't have access to this feature
}
```

## License

MIT License

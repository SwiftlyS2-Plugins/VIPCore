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

VIPCore uses two config files:

- `config.jsonc` (section: `vip`)
- `vip_groups.jsonc` (section: `vip_groups`)

### `config.jsonc` (`vip`)

| Setting | Default | Description |
| :--- | :--- | :--- |
| `Delay` | `2.0` | General delay value (seconds) used by VIPCore where applicable. |
| `DatabaseConnection` | `"default"` | Database connection name from your SwiftlyS2 database configuration. |
| `TimeMode` | `0` | Time unit for `vip_adduser` and management menus: `0=seconds`, `1=minutes`, `2=hours`, `3=days`. |
| `UseCenterHtmlMenu` | `true` | Use the center HTML menu style when available. |
| `ReOpenMenuAfterItemClick` | `false` | If enabled, reopens the VIP menu after selecting a feature item. |
| `VipLogging` | `true` | Enables debug logging related to VIP loading/feature initialization. |

#### Example

```jsonc
{
  "vip": {
    "Delay": 2.0,
    "DatabaseConnection": "default",
    "TimeMode": 0,
    "UseCenterHtmlMenu": true,
    "ReOpenMenuAfterItemClick": false,
    "VipLogging": true
  }
}
```

### `vip_groups.jsonc` (`vip_groups`)

Define groups and the features they grant.

- A group is a named entry under `vip_groups.Groups` (e.g. `VIP`, `MVIP`).
- Features are defined under `Values`.
- A feature value can be:
  - `1` / `0` (simple enabled/disabled defaults for toggle features)
  - An object (feature-specific settings, defined by the module)

#### Example

```jsonc
{
  "vip_groups": {
    "Groups": {
      "VIP": {
        "Values": {
          "vip.zeus": 1,
          "vip.armor": { "Armor": 100 },
          "vip.bhop": { "Timer": 5.0, "MaxSpeed": 300.0 }
        }
      },
      "MVIP": {
        "Values": {
          "vip.armor": { "Armor": 50 }
        }
      }
    }
  }
}
```

## Included Modules

This repository ships several optional VIP modules (each one is a standalone SwiftlyS2 plugin that registers features into VIPCore):

| Module | Description |
| :--- | :--- |
| `VIP_AntiFlash` | Anti-flash feature module |
| `VIP_Armor` | Armor feature module |
| `VIP_Bhop` | Bunnyhop-related feature module |
| `VIP_DoubleJump` | Double jump feature module |
| `VIP_FastReload` | Fast reload feature module |
| `VIP_Fov` | Field-of-view feature module |
| `VIP_GoldMember` | Gold member group/feature module |
| `VIP_Health` | Health-related feature module |
| `VIP_KillScreen` | Kill screen feature module |
| `VIP_NoFallDamage` | No fall damage feature module |
| `VIP_SmokeColor` | Smoke color feature module |
| `VIP_Tag` | Tag feature module |
| `VIP_Zeus` | Zeus feature module |

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

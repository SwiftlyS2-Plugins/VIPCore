<div align="center">
  <img src="https://pan.samyyc.dev/s/VYmMXE" />
  <h2><strong>VIPCore.Contract</strong></h2>
  <h3>API contract for VIPCore module development.</h3>
</div>

<p align="center">
  <img src="https://img.shields.io/badge/build-passing-brightgreen" alt="Build Status">
  <img src="https://img.shields.io/github/downloads/aga/VIPCore.Contract/total" alt="Downloads">
  <img src="https://img.shields.io/github/stars/aga/VIPCore.Contract?style=flat&logo=github" alt="Stars">
  <img src="https://img.shields.io/github/license/aga/VIPCore.Contract" alt="License">
</p>

## Description

VIPCore.Contract ships with VIPCore as `resources/exports/VIPCore.Contract.dll`. VIP modules compile against it and bind to that exported copy at runtime.

The `SwiftlyS2.VIPCore.Contract` NuGet package is no longer published; versions already on NuGet.org are frozen.

## Features

- **API Interface** (`IVipCoreApiV1`) - Complete API for interacting with VIPCore
- **Base Classes** (`VipFeatureBase`) - Simplified module development with built-in event handling
- **Enums** - `FeatureState` and `FeatureType` for feature configuration
- **Ships with VIPCore** - Exported from `resources/exports/`, so every module binds to the same copy

## Usage

Copy `VIPCore.Contract.dll` from a VIPCore release (or server install) into your module and reference it:

```xml
<ItemGroup>
  <Reference Include="VIPCore.Contract">
    <HintPath>lib\VIPCore.Contract.dll</HintPath>
    <Private>false</Private>
  </Reference>
</ItemGroup>
```

Modules inside the VIPCore repository reference the project instead:

```xml
<ItemGroup>
  <ProjectReference Include="..\..\VIPCore.Contract\VIPCore.Contract.csproj" Private="false" />
</ItemGroup>
```

> **Note:** `Private=false` keeps the contract out of your module's output — VIPCore is already loaded on the server and exports the copy every module binds to.

## API Overview

### IVipCoreApiV1 Methods

| Method | Description |
|--------|-------------|
| `RegisterFeature()` | Register a feature with VIPCore |
| `UnregisterFeature()` | Remove a feature from VIPCore |
| `IsClientVip()` | Check if a player has VIP status |
| `PlayerHasFeature()` | Check if a VIP has access to a feature |
| `GetPlayerFeatureState()` | Get enabled/disabled state |
| `SetPlayerFeatureState()` | Update feature state |
| `GetFeatureValue<T>()` | Read feature configuration from group |
| `GetPlayerCookie<T>()` | Retrieve persistent player data |
| `SetPlayerCookie<T>()` | Store persistent player data |
| `GiveClientVip()` | Grant VIP to player |
| `RemoveClientVip()` | Remove VIP from player |

### Menus and read-only views (contract 1.1.0, VIPCore 1.2.0)

| Member | Description |
|--------|-------------|
| `DescribeFeature(key, player => line)` | The line menus show under a feature's name ("120 HP on spawn"); return null when the player's group has no setting |
| `RegisterFeatureOptions(key, options, current, choose)` | Lists a Selectable feature's choices on the Customize page instead of a menu of its own |
| `RegisterMenuPage(VipMenuPage)` / `UnregisterMenuPage(id)` | Adds a module's own page (the gun menu) to the VIP menu |
| `OpenMenu(player, pageId?)` | Opens the VIP menu, or one of its pages (`VipMenuIds` or a module page's id) with the menu as its parent |
| `GetFeatureView(player, key)` | A feature as the player's menu shows it: name, description, current value, state, whether it is locked |
| `GetClientVipGrants(player)` | Every group the player holds, the active one first, with scope, expiry and grant date |
| `GetGroupInfos()` | The configured groups, lowest weight first, with how many features each grants |

A module that calls these needs VIPCore 1.2.0 or later: an older VIPCore exports the 1.0 contract, and the first call into one of them throws `MissingMethodException`.

### Exact-length grants (contract 1.2.0, VIPCore 1.3.0)

| Member | Description |
|--------|-------------|
| `GiveClientVip(player, group, TimeSpan duration)` | Grants for an exact length whatever the server's `TimeMode` is. The `int` overload counts in `TimeMode` units |

`OnPlayerSpawn` is raised after every spawn of a human player from VIPCore 1.3.0; earlier versions raised it once, when the player joined.

### Menu rows

Every row of VIPCore's menus carries a record as its component `Tag`, so a renderer can draw it from data instead of parsing its text: `VipFeatureRow` (a perk), `VipSectionRow` (a heading over a feature's or a group's choices), `VipOptionRow` (one choice, `Selected` when in use), `VipUserRow`, `VipGrantRow`, `VipLengthRow`, `VipPlayerRow`, `VipGroupRow`, `VipScopeRow` and `VipActionRow` (an action or a summary line). A changed row gets a new record and its session is invalidated. A module page built the same way (sections and options, a `VipActionRow` summary and submit, a `VipFeatureLimit` as the menu's `Tag`) is drawn like VIPCore's own.

### Enums

```csharp
public enum FeatureState { Enabled = 0, Disabled = 1, NoAccess = 2 }
public enum FeatureType { Toggle, Selectable, Hide }
```

### Base Class Example

```csharp
public class MyFeature : VipFeatureBase
{
    public override string Feature => "vip.myfeature";
    
    public MyFeature(IVipCoreApiV1 api, ISwiftlyCore core) : base(api, core) { }
    
    public override void OnPlayerSpawn(IPlayer player)
    {
        // Apply feature on spawn
    }
}
```

## Building

```bash
dotnet build -c Release
```

## Distribution

The contract is not packaged separately. Building VIPCore builds this project and copies the DLL into VIPCore's `resources/exports/`, which every VIPCore release zip carries.

## See Also

- [VIPCore Documentation](https://github.com/aga/VIPCore)

## License

MIT License
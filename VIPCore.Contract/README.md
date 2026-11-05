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
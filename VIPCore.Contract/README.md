<div align="center">
  <img src="https://pan.samyyc.dev/s/VYmMXE" />
  <h2><strong>SwiftlyS2.VIPCore.Contract</strong></h2>
  <h3>API contract for VIPCore module development.</h3>
</div>

<p align="center">
  <img src="https://img.shields.io/badge/build-passing-brightgreen" alt="Build Status">
  <img src="https://img.shields.io/github/downloads/aga/VIPCore.Contract/total" alt="Downloads">
  <img src="https://img.shields.io/github/stars/aga/VIPCore.Contract?style=flat&logo=github" alt="Stars">
  <img src="https://img.shields.io/github/license/aga/VIPCore.Contract" alt="License">
</p>

## Description

VIPCore.Contract is now distributed as **`SwiftlyS2.VIPCore.Contract`** on NuGet.

## Features

- **API Interface** (`IVipCoreApiV1`) - Complete API for interacting with VIPCore
- **Base Classes** (`VipFeatureBase`) - Simplified module development with built-in event handling
- **Enums** - `FeatureState` and `FeatureType` for feature configuration
- **NuGet Package** - Easy integration into module projects

## Usage

Add the NuGet package to your module project:

```xml
<ItemGroup>
  <PackageReference Include="SwiftlyS2.VIPCore.Contract" Version="*" ExcludeAssets="runtime" PrivateAssets="all" />
</ItemGroup>
```

> **Note:** Use `ExcludeAssets="runtime" PrivateAssets="all"` because VIPCore is already loaded on the server at runtime.

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
| `GiveClientTemporaryVip()` | Grant temporary VIP to player |
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

## Publishing

The contract is published as a NuGet package to NuGet.org:

```bash
dotnet pack -c Release
dotnet nuget push bin/Release/*.nupkg --api-key YOUR_KEY --source https://api.nuget.org/v3/index.json
```

## See Also

- [VIPCore Documentation](https://github.com/aga/VIPCore)
- [NuGet Package](https://www.nuget.org/packages/SwiftlyS2.VIPCore.Contract)

## License

MIT License
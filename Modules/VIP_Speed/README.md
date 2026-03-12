<div align="center">
  <img src="https://pan.samyyc.dev/s/VYmMXE" />
  <h2><strong>VIP_Speed</strong></h2>
  <h3>SwiftlyS2 VIPCore module that modifies player speed.</h3>
</div>

<p align="center">
  <img src="https://img.shields.io/badge/build-passing-brightgreen" alt="Build Status">
  <img src="https://img.shields.io/github/downloads/aga/VIP_Speed/total" alt="Downloads">
  <img src="https://img.shields.io/github/stars/aga/VIP_Speed?style=flat&logo=github" alt="Stars">
  <img src="https://img.shields.io/github/license/aga/VIP_Speed" alt="License">
</p>

## Installation
1. Install VIPCore and ensure it is working.
2. Download `VIP_Speed.zip` and extract its contents into `addons/swiftly/plugins/VIP_Speed`.
3. Add the module to your `vip_groups.jsonc`.

## Configuration
Add the `"vip.speed"` feature to the VIP groups you want to have this module enabled in `addons/swiftly/configs/plugins/VIPCore/vip_groups.jsonc`.

### Example `vip_groups.jsonc` configuration
```jsonc
{
    "Groups": {
        "VIP1": {
            "Values": {
                // Other features...
                
                "vip.speed": {
                    "Speed": 1.2
                }
            }
        }
    }
}
```
* Note: `Speed` is a float value where `1.0` is normal speed. Values above `1.0` will make the player faster (e.g., `1.2` is 20% faster). Values below `1.0` will make the player slower.

## Building from source
- Open the project in your preferred .NET IDE (e.g., Visual Studio, Rider, VS Code).
- Build the project. The output DLL and resources will be placed in the `build/` directory.
- The publish process will also create a zip file for easy distribution.

## Publishing

- Use the `dotnet publish -c Release` command to build and package your plugin.
- Distribute the generated zip file or the contents of the `build/publish` directory.
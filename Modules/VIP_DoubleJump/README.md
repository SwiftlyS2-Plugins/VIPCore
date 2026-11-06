<div align="center">
  <img src="https://pan.samyyc.dev/s/VYmMXE" />
  <h2><strong>VIP_DoubleJump</strong></h2>
  <h3>No description.</h3>
</div>

<p align="center">
  <img src="https://img.shields.io/badge/build-passing-brightgreen" alt="Build Status">
  <img src="https://img.shields.io/github/downloads/aga/VIP_DoubleJump/total" alt="Downloads">
  <img src="https://img.shields.io/github/stars/aga/VIP_DoubleJump?style=flat&logo=github" alt="Stars">
  <img src="https://img.shields.io/github/license/aga/VIP_DoubleJump" alt="License">
</p>

## Getting Started (delete me)

1. **Edit `PluginMetadata` Attribute**  
   - Set your plugin's `Id`, `Name`, `Version`, `Author` and `Description`.
2. **Edit `VIP_DoubleJump.csproj`**  
   - Set the `<AssemblyName>` property to match your plugin's main class name.
   - Add any additional dependencies as needed.
3. **Implement your plugin logic** in C#.
   - Place your main plugin class in the root of the project.
   - Use the SwiftlyS2 managed API to interact with the game and core.
4. **Add resources**  
   - Place any required files in the `gamedata`, `templates`, or `translations` folders as needed.

## Building

- Open the project in your preferred .NET IDE (e.g., Visual Studio, Rider, VS Code).
- Build the project. The output DLL and resources will be placed in the `build/` directory.
- The publish process will also create a zip file for easy distribution.

## VIPCore VIP group configuration

Add the feature key `vip.doublejump` to your VIP group `values` section.

Example (inside your VIPCore groups config):

```jsonc
{
  "Groups": {
    "VIP": {
      "values": {
        "vip.doublejump": {
          "MaxJumps": 2,
          "Boost": 320.0
        }
      }
    }
  }
}
```

Settings:

- **MaxJumps**
  Total jumps allowed before touching ground again.
  `2` = normal jump + 1 extra jump in air.
- **Boost**
  Upward velocity applied for the extra jump.

In the VIP menu the perk reads *Jump again in mid-air* for `MaxJumps` 2, or *Jump N more times in mid-air* above that (translation keys `vip.doublejump.description*`).

## Publishing

- Use the `dotnet publish -c Release` command to build and package your plugin.
- Distribute the generated zip file or the contents of the `build/publish` directory.
<div align="center">
  <h2><strong>VIP_Money</strong></h2>
  <h3>Gives money to VIP players.</h3>
</div>

<p align="center">
  <img src="https://img.shields.io/badge/build-passing-brightgreen" alt="Build Status">
  <img src="https://img.shields.io/github/downloads/aga/VIP_Money/total" alt="Downloads">
  <img src="https://img.shields.io/github/stars/aga/VIP_Money?style=flat&logo=github" alt="Stars">
  <img src="https://img.shields.io/github/license/aga/VIP_Money" alt="License">
</p>

## Expected Configuration

Add the following to your VIP group configuration in the database or JSON config:

```json
{
  "vip.money": {
    "money": "500" // Adds $500 at the end of each round
  }
}
```

The amount is always added to what the player has, capped at `mp_maxmoney`, and nothing is paid at the round ends the module treats as pistol rounds. The legacy `"++500"` form means the same as `"500"`.

In the VIP menu the perk reads, for example, *+$500 every round* (translation key `vip.money.description`).

## Building

- Open the project in your preferred .NET IDE (e.g., Visual Studio, Rider, VS Code).
- Build the project. The output DLL and resources will be placed in the `build/` directory.
- The publish process will also create a zip file for easy distribution.

## Publishing

- Use the `dotnet publish -c Release` command to build and package your plugin.
- Distribute the generated zip file or the contents of the `build/publish` directory.
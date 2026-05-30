# DiagDump

Diagnostic logging and debugging tool for R.E.P.O. modding.

## Features

- Enable Harmony debug logging for patch inspection
- Automatically dump all Harmony patches on key game methods
- Scan loaded mod assemblies (LevelDisplay, StageTimer, Revive, ShopExpander, etc.)
- Dump shop scene state (ShopManager fields, GameObject hierarchy)
- Log detailed game object structure with components
- Manual patch testing and verification
- One-time diagnostic dump on first RoundDirector.Update

## Installation

1. Install [BepInEx 5.x](https://github.com/BepInEx/BepInEx/releases) for R.E.P.O.
2. Download the latest `DiagDump.dll` from releases
3. Place `DiagDump.dll` in `BepInEx/plugins/`
4. Launch the game and check the BepInEx console/log file

## Configuration

This mod is configuration-free. All diagnostics are automatically triggered on game start.

## Usage

Launch the game and monitor the BepInEx log output (typically `BepInEx/LogOutput.log`). The diagnostic dump will show:

- **HARMONY PATCH LIST**: All applied Harmony patches to key methods
- **ASSEMBLY SCAN**: Discovered patch classes and methods in loaded mods
- **SHOP DUMP HOOK**: Details of ShopManager state and shop scene objects
- **MANUAL PATCH TEST**: Verification of specific patches like LevelDisplay

## Build

```bash
dotnet build -c Release
```

Output: `bin/Release/netstandard2.1/DiagDump.dll`


## AI Disclosure

This mod was developed with the assistance of AI (Claude by Anthropic). All code has been reviewed and tested by the developer.

## License

MIT

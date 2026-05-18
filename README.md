# DiagDump

Diagnostic dump tool for R.E.P.O. modding. Logs game object structures, Harmony patches, and ShopManager state.

R.E.P.O. BepInEx mod. Host-side only.

## Install

Requires [BepInEx 5.x](https://thunderstore.io/c/repo/p/BepInEx/BepInExPack/).

Place `DiagDump.dll` in `BepInEx/plugins/`.

## Configuration

Edit `BepInEx/config/maxenterme.DiagDump.cfg` after first launch.

## Build

```
dotnet build -c Release
```

## License

MIT

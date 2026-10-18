# Contributing and local builds

## Requirements

- A legally installed copy of Valheim.
- BepInExPack Valheim 5.4.2351.
- Mono C# compiler (`mcs`).
- Python 3 with Pillow and NumPy for model validation.
- Blender 4.2 or later only when regenerating the model assets.

Game and BepInEx assemblies are referenced from the local installation and must not be committed or redistributed.

`VALHEIM_DIR` is required so the scripts never assume a platform-specific Steam library. Set `BEPINEX_CORE` when BepInEx is installed outside the selected Valheim directory.

## Build

From the repository root:

```bash
BEPINEX_CORE=/path/to/BepInEx/core VALHEIM_DIR=/path/to/Valheim bash build.sh
python3 tools/validate_model.py
python3 tools/validate_model.py assets/vile
```

The DLL and runtime assets are written to `dist/LoxLorax/`.

Build the two public archives and validate all committed release inputs with:

```bash
python3 tools/build_public_packages.py
python3 tools/validate_release.py
```

The compiled release files are committed so players can install a checkout without obtaining proprietary build dependencies. Maintainers must rebuild them whenever source, assets, metadata, or package documentation changes.

## Release tags

The `v*.*.*` GitHub Actions workflow verifies that the tag matches `manifest.json`, validates the committed packages, and creates a GitHub release with both ZIPs and `SHA256SUMS.txt`. Push the release commit before its tag:

```bash
git push origin main
git push origin v1.0.8
```

## Regenerate models

The authoritative geometry generator is `tools/create_model.py`. It creates the living model by default. Set `LOXLORAX_VARIANT=vile` for the undead variant. Review the Blender files and previews before replacing a release asset.

## Pull requests

Keep changes limited to this mod. Do not add game assemblies, decompiled game source, BepInEx binaries, user saves, logs, or third-party assets without documented redistribution permission. Explain how visual or animation changes were tested.

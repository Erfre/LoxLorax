# Lox Lorax

Replaces living Valheim lox with an animated orange forest creature and Vile (`Unbjorn`) with a separate undead variant. The original AI, attacks, animation controller, hitboxes, drops, taming, and saddle behavior remain in place.

## Features

- Living and undead replacement models.
- Native 17-bone animation retargeting.
- Grounded Vile locomotion adapted to larger paws.
- Visible living-lox bite motion.
- Cosmetic detached-head and sky-launch death effect.
- Press F8 to disable or enable all replacements instantly.

## Requirements and installation

Requires BepInExPack Valheim 5.4.2351. Extract the main ZIP into the Valheim directory or active BepInEx profile. The three mod files install under `BepInEx/plugins/LoxLorax/`.

This is client-side. Everyone who wants to see the models needs the mod, but the server does not.

Valheim 1.0 does not award Steam achievement progress while BepInEx is loaded. Use a vanilla launch when earning achievements; the F8 visual toggle does not unload BepInEx.

## Tested versions

Built and tested with Valheim 1.0.20, Steam build 25909357, and BepInExPack 5.4.2351. Tests cover live enable/disable, model validation, native Vile animation sampling, paw grounding, and death-snapshot geometry.

## Known limits

Windows, full multiplayer, riding alignment, every terrain slope, and every combat transition have not been exhaustively verified. Other lox visual replacers and renamed creature prefabs are unsupported.

## Disclosure

The code and model-development workflow received substantial generative-AI assistance. Apply the Nexus Mods `AI-Generated Content` tag. Promotional screenshots show the actual mod rendered in Valheim.

This is an unofficial, non-commercial fan project and is not affiliated with Iron Gate, Coffee Stain Publishing, Dr. Seuss Enterprises, or their partners. Names, games, characters, and trademarks belong to their respective owners.

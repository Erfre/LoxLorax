# Lox Lorax

Client-side Valheim visual mod that replaces living lox with an animated orange forest creature and Vile (`Unbjorn`) with an undead variant.

## Features

- Original living and undead replacement models.
- Seventeen-bone retargeting driven by Valheim's native creature Animators.
- Ground-contact adaptation for the Vile's larger replacement paws.
- Visible living-lox bite motion.
- Cosmetic detached-head and sky-launch death effect.
- Press **F8** to disable or enable all replacements immediately; the state is saved.

## Requirements

- Valheim 1.0.20 / Steam build 25909357, or a compatible later build.
- BepInExPack Valheim 5.4.2351.

## Manual installation

1. Install BepInExPack Valheim 5.4.2351 through a mod manager or manually.
2. Extract this archive into the Valheim directory or active BepInEx profile.
3. Confirm these files are together:

```text
BepInEx/plugins/LoxLorax/LoxLorax.dll
BepInEx/plugins/LoxLorax/lorax.llmesh
BepInEx/plugins/LoxLorax/vile-lorax.llmesh
```

4. Launch the game with BepInEx enabled and load a world. Existing and newly spawned lox and Vile creatures are replaced automatically.

A dedicated server does not need this mod, but each player who wants to see the models needs it on their client.

## Configuration

Press F8 while playing to toggle the mod. After first launch, edit `BepInEx/config/com.loxlorax.mod.cfg` to change the key, model heights, yaw, vertical offset, animation following, or the Vile replacement.

## Compatibility

The mod retains Valheim's original creature AI, attacks, hitboxes, drops, taming, and saddle behavior. Other lox visual replacers and renamed creature prefabs are unsupported. Windows, full multiplayer, riding alignment, every terrain slope, and every combat transition have not been exhaustively verified.

Valheim 1.0 does not award Steam achievement progress while BepInEx is loaded. Disabling the replacements with F8 does not unload BepInEx; launch Valheim in vanilla mode when earning achievements.

## Disclosure

The code and model-development workflow received substantial generative-AI assistance. Screenshots on the project pages show the actual mod rendered in Valheim.

This is an unofficial, non-commercial fan project. It is not affiliated with Iron Gate, Coffee Stain Publishing, Dr. Seuss Enterprises, or their partners. Names, characters, games, and trademarks belong to their respective owners.

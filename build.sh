#!/usr/bin/env bash
set -euo pipefail
cd -- "$(dirname -- "$0")"
if [[ -z "${VALHEIM_DIR:-}" ]]; then
    echo "Set VALHEIM_DIR to the root of your Valheim installation." >&2
    exit 1
fi
game_dir="$VALHEIM_DIR"
core_dir="${BEPINEX_CORE:-$game_dir/BepInEx/core}"
managed_dir="$game_dir/valheim_Data/Managed"
if [[ ! -d "$managed_dir" ]]; then managed_dir="$game_dir/valheim_server_Data/Managed"; fi
for library in "$managed_dir/assembly_valheim.dll" "$core_dir/BepInEx.dll" "$core_dir/0Harmony.dll"; do
    if [[ ! -f "$library" ]]; then echo "Missing build reference: $library" >&2; exit 1; fi
done
mkdir -p dist/LoxLorax
references=("$managed_dir/assembly_valheim.dll" "$managed_dir/assembly_utils.dll" "$managed_dir/UnityEngine.dll" "$managed_dir/netstandard.dll" "$core_dir/BepInEx.dll" "$core_dir/0Harmony.dll")
for module in CoreModule AnimationModule PhysicsModule ImageConversionModule; do references+=("$managed_dir/UnityEngine.$module.dll"); done
args=()
for library in "${references[@]}"; do args+=("-r:$library"); done
mcs -target:library -optimize+ -warnaserror+ -out:dist/LoxLorax/LoxLorax.dll "${args[@]}" src/*.cs
chmod 0644 dist/LoxLorax/LoxLorax.dll
if [[ -f assets/lorax.llmesh ]]; then cp assets/lorax.llmesh dist/LoxLorax/; fi
if [[ -f assets/vile/lorax.llmesh ]]; then cp assets/vile/lorax.llmesh dist/LoxLorax/vile-lorax.llmesh; fi
echo "Built dist/LoxLorax/LoxLorax.dll"
if [[ ! -f assets/lorax.llmesh ]]; then echo "MODEL PENDING: plugin compiles, but will keep vanilla lox until lorax.llmesh is supplied."; fi

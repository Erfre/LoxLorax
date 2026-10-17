#!/usr/bin/env bash
set -euo pipefail
project_dir="$(cd -- "$(dirname -- "$0")/.." && pwd)"
if [[ -z "${VALHEIM_DIR:-}" ]]; then
    echo "Set VALHEIM_DIR to the root of your Valheim installation." >&2
    exit 1
fi
game_dir="$VALHEIM_DIR"
runtime_dir="${BEPINEX_RUNTIME:-$project_dir/.build/runtime/BepInExPack_Valheim}"
managed_dir="$game_dir/valheim_Data/Managed"
test_dir="$project_dir/.build/world-regression"
mkdir -p "$test_dir" "$runtime_dir/BepInEx/plugins/LoxLorax"
if [[ -f "$runtime_dir/BepInEx/plugins/RuntimeSmoke.dll" ]]; then
    mv "$runtime_dir/BepInEx/plugins/RuntimeSmoke.dll" "$project_dir/.build/RuntimeSmoke.dll.disabled"
fi
cp "$project_dir/dist/LoxLorax/"* "$runtime_dir/BepInEx/plugins/LoxLorax/"
refs=("-r:$project_dir/dist/LoxLorax/LoxLorax.dll" "-r:$runtime_dir/BepInEx/core/BepInEx.dll" "-r:$runtime_dir/BepInEx/core/0Harmony.dll")
for name in assembly_valheim assembly_utils UnityEngine UnityEngine.CoreModule UnityEngine.AnimationModule UnityEngine.PhysicsModule UnityEngine.ImageConversionModule netstandard; do refs+=("-r:$managed_dir/$name.dll"); done
mcs -target:library -warnaserror+ -out:"$runtime_dir/BepInEx/plugins/WorldRegression.dll" "${refs[@]}" "$project_dir/tools/WorldRegression.cs" "$project_dir/tools/AnimationRegression.cs"
test_display="${LOXLORAX_TEST_DISPLAY:-${DISPLAY:-}}"
if [[ -z "$test_display" ]]; then
    echo "Set LOXLORAX_TEST_DISPLAY or DISPLAY to an available X display." >&2
    exit 1
fi
export DISPLAY="$test_display"
export LOXLORAX_TEST_DIR="$test_dir"
export XDG_CONFIG_HOME="$test_dir/config"
export XDG_CACHE_HOME="$test_dir/cache"
export SteamAppId=892970
set +e
timeout 180s bash "$runtime_dir/start_game_bepinex.sh" "$game_dir/valheim.x86_64" -batchmode -force-glcore -screen-width 320 -screen-height 240 -savedir "$test_dir/saves" -logFile "$test_dir/unity.log" > "$test_dir/console.log" 2>&1
result=$?
set -e
rg 'LOXLORAX_ANIMATION_|LOXLORAX_WORLD_|Lox Lorax ready|Could not replace|Could not load Lorax' "$test_dir/console.log" || true
if [[ "$result" -eq 0 && "${LOXLORAX_ANIMATION_TEST:-0}" == 1 ]]; then
    rg -q 'LOXLORAX_ANIMATION_PASS' "$test_dir/console.log"
elif [[ "$result" -eq 0 ]]; then
    [[ -s "$test_dir/world-pass.txt" && -s "$test_dir/world-lox.png" ]]
    rg -q 'LOXLORAX_WORLD_PASS' "$test_dir/console.log"
fi
exit "$result"

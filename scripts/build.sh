#!/usr/bin/env bash
# Quartz build script.
# Usage: ./build.sh [Config]
#   Config: Release (default) | Debug | Debug_IL2CPP | Release_IL2CPP
# Builds Quartz.dll, auto-installs into the game (Mods + UserData/Quartz),
# and writes dist/Quartz.zip.
#
# Default is Release: Quartz is almost entirely per-frame managed code (overlay
# Update loops, change-guards, per-key/per-planet loops), which an UNOPTIMIZED
# Debug build (Optimize=false) runs ~1.5-2x slower — enough to look like a big
# FPS regression vs vanilla. Always test FPS against a Release build.
set -euo pipefail

cd "$(dirname "$0")"

CONFIG="${1:-Release}"

if [[ "$CONFIG" == Debug* ]]; then
    echo ">> NOTE: building $CONFIG — UNOPTIMIZED (Optimize=false). Slower per-frame code;"
    echo ">>       do NOT use for FPS comparisons. Run ./build.sh (Release) for perf testing."
fi

# --- Locate game install (auto-detect, override with GAMEPATH env var) ---
detect_gamepath() {
    if [[ -n "${GAMEPATH:-}" ]]; then
        echo "$GAMEPATH"; return
    fi
    local candidates=(
        "$HOME/Library/Application Support/Steam/steamapps/common/A Dance of Fire and Ice"  # macOS
        "$HOME/.local/share/Steam/steamapps/common/A Dance of Fire and Ice"                 # Linux
        "/c/Program Files (x86)/Steam/steamapps/common/A Dance of Fire and Ice"             # Windows (git-bash)
        "C:/Program Files (x86)/Steam/steamapps/common/A Dance of Fire and Ice"
    )
    for c in "${candidates[@]}"; do
        [[ -d "$c" ]] && { echo "$c"; return; }
    done
    return 1
}

# --- Resolve GameData (where Managed/ lives — differs per OS) ---
resolve_gamedata() {
    local gp="$1"
    if [[ -d "$gp/ADanceOfFireAndIce.app/Contents/Resources/Data/Managed" ]]; then
        echo "ADanceOfFireAndIce.app/Contents/Resources/Data"; return
    fi
    if [[ -d "$gp/A Dance of Fire and Ice_Data/Managed" ]]; then
        echo "A Dance of Fire and Ice_Data"; return
    fi
    return 1
}

# --- Generate Directory.Build.props if absent ---
if [[ ! -f Directory.Build.props ]]; then
    echo ">> Directory.Build.props missing — generating..."
    GP="$(detect_gamepath)" || { echo "ERROR: game install not found. Set GAMEPATH env var."; exit 1; }
    GD="$(resolve_gamedata "$GP")" || { echo "ERROR: Managed/ folder not found under $GP"; exit 1; }
    cat > Directory.Build.props <<EOF
<Project>
    <PropertyGroup>
        <GamePath>$GP</GamePath>
        <GameData>$GD</GameData>
    </PropertyGroup>
</Project>
EOF
    echo ">> wrote Directory.Build.props (GamePath=$GP)"
fi

# --- Verify MelonLoader present (or stood in for by lib/) ---
GP_CHECK="$(grep -oE '<GamePath>[^<]+' Directory.Build.props | sed 's/<GamePath>//')"
if [[ ! -f "$GP_CHECK/MelonLoader/net35/MelonLoader.dll" && ! -f lib/MelonLoader.dll ]]; then
    echo "WARNING: MelonLoader not found at $GP_CHECK/MelonLoader — install MelonLoader,"
    echo "         or drop MelonLoader.dll into lib/ to build without it."
fi

# --- Build (PostBuild targets auto-install into the game) ---
# Second arg picks the loader target(s): ML | UMM | both (default both).
TARGET="${2:-both}"

# The csproj skips the UMM local install when MelonLoader is present, so the two
# loaders never double-load. That silently freezes the UMM core while modules keep
# deploying to both roots, which shows up much later as a MissingMethodException
# from a module calling core API the stale DLL lacks. If a live UMM install is
# already there, it is the one being played -- keep it current.
UMM_FORCE=()
GAMEDIR="${GP:-}"
if [[ -z "$GAMEDIR" && -f Directory.Build.props ]]; then
    GAMEDIR="$(sed -n 's:.*<GamePath>\(.*\)</GamePath>.*:\1:p' Directory.Build.props | head -1)"
fi
if [[ -n "$GAMEDIR" ]] && [[ -f "$GAMEDIR/Mods/Quartz/Info.json" || -f "$GAMEDIR/UMMMods/Quartz/Info.json" ]]; then
    UMM_FORCE=(-p:UmmAutoInstall=true)
fi

build_one() {
    local loader="$1"
    local extra=()
    [[ "$loader" == "UMM" ]] && extra=("${UMM_FORCE[@]}")
    echo ">> building Quartz/Quartz.csproj ($CONFIG, LoaderTarget=$loader)..."
    dotnet build Quartz/Quartz.csproj -c "$CONFIG" -p:AutoInstall=true -p:LoaderTarget="$loader" "${extra[@]}"
}

case "$TARGET" in
    ML)   build_one ML ;;
    UMM)  build_one UMM ;;
    both) build_one ML; build_one UMM ;;
    *)    echo "ERROR: unknown target '$TARGET' (use ML | UMM | both)"; exit 1 ;;
esac

# Features now ship as modules beside the core DLL. Build and deploy them in the
# same pass: a core that has shed a feature, running next to a game folder that
# still lacks that feature's module, simply loses the feature — and a STALE core
# beside fresh modules double-registers its pages. Keep the two in lockstep.
echo ">> building modules ($CONFIG)..."
dotnet build modules/AllModules.proj -c "$CONFIG" -p:DeployToGame=true

# The zips the csproj just wrote predate the modules, so fold them in now. An
# upgrading user's first launch copies out of Module.bundled/ rather than
# downloading — without this the install zips would silently drop every
# already-extracted feature.
tools/bundle-modules.sh

echo ">> done."
[[ "$TARGET" == "ML"  || "$TARGET" == "both" ]] && echo ">> MelonLoader:     Mods/Quartz.Bootstrap.dll + UserData/Quartz/Runtime/* — dist/Quartz.zip"
[[ "$TARGET" == "UMM" || "$TARGET" == "both" ]] && echo ">> UnityModManager: UMMMods/Quartz (or Mods/Quartz) + Quartz/Runtime/* — dist/QuartzUmm.zip"
echo ">> Modules:          <data root>/Module/*.qmod — dist/modules/"

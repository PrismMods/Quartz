#!/usr/bin/env bash
# Build the standalone Key Viewer mod.
#
# Same core as Quartz, built with -p:Flavor=KeyViewer: its own assembly name
# (QuartzKeyViewer), its own data root (UserData/QuartzKeyViewer), the feature
# switches hidden, and only the key viewer (plus the overlay core it needs)
# running. It can sit beside a full Quartz install without sharing a file —
# and refuses to start if one is loaded, so there is never a double menu.
#
# Usage: tools/build-keyviewer.sh [Config] [ML|UMM|both]
#   Config: Release (default) | Debug | Debug_IL2CPP | Release_IL2CPP
#
# Set AUTOINSTALL=1 to also drop it into the local game install for testing.
# Release is the default for the same reason as build.sh: a Debug build runs
# the per-frame overlay code unoptimized and looks like an FPS regression.
set -euo pipefail

cd "$(dirname "$0")/.."

CONFIG="${1:-Release}"
TARGET="${2:-both}"
AUTOINSTALL="${AUTOINSTALL:-0}"

INSTALL_ARGS=(-p:AutoInstall=false)
if [[ "$AUTOINSTALL" == "1" ]]; then
    INSTALL_ARGS=(-p:AutoInstall=true)
fi

build_one() {
    echo ">> building QuartzKeyViewer ($CONFIG, LoaderTarget=$1)..."
    dotnet build Quartz/Quartz.csproj -c "$CONFIG" -p:Flavor=KeyViewer -p:LoaderTarget="$1" "${INSTALL_ARGS[@]}"
}

case "$TARGET" in
    ML)   build_one ML ;;
    UMM)  build_one UMM ;;
    both) build_one ML; build_one UMM ;;
    *)    echo "ERROR: unknown target '$TARGET' (use ML | UMM | both)"; exit 1 ;;
esac

echo ">> done."
[[ "$TARGET" == "ML"  || "$TARGET" == "both" ]] && echo ">> MelonLoader:     dist/QuartzKeyViewer.zip     (Mods/QuartzKeyViewer.Bootstrap.dll + UserData/QuartzKeyViewer/Runtime/)"
[[ "$TARGET" == "UMM" || "$TARGET" == "both" ]] && echo ">> UnityModManager: dist/QuartzKeyViewerUmm.zip  (QuartzKeyViewer/QuartzKeyViewerUmm.Bootstrap.dll + Runtime/)"
exit 0

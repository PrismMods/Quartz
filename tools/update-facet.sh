#!/usr/bin/env bash
# Copy the built Facet engine into lib/ (gitignored) so Quartz and gen-stubs can
# reference it.
#
# Usage: tools/update-facet.sh [--build]
#   --build  run `dotnet build -c Release` in the Facet repo first
#
# The Facet repo is $FACET_DIR if set, else ../../Facet relative to the MAIN
# checkout (found via the git common dir, so this works from a worktree).
set -euo pipefail

cd "$(dirname "$0")/.."

if [[ -n "${FACET_DIR:-}" ]]; then
    FACET="$FACET_DIR"
else
    FACET="$(cd "$(git rev-parse --path-format=absolute --git-common-dir)/.." && pwd)/../../Facet"
fi
[[ -d "$FACET" ]] || { echo "ERROR: Facet repo not found at $FACET. Set FACET_DIR." >&2; exit 1; }

[[ "${1:-}" == "--build" ]] && dotnet build "$FACET/Facet/Facet.csproj" -c Release -v quiet --nologo

DLL="$FACET/Facet/bin/Release/netstandard2.1/Facet.0.1.dll"
[[ -f "$DLL" ]] || { echo "ERROR: $DLL missing. Build Facet first (re-run with --build)." >&2; exit 1; }

mkdir -p lib
cp "$DLL" lib/Facet.0.1.dll
echo "lib/Facet.0.1.dll <- $DLL"

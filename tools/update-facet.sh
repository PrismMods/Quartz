#!/usr/bin/env bash
# Build the Facet engine and copy it into lib/ (gitignored), where Quartz and gen-stubs
# reference it and every build ships it beside Quartz.dll. tools/release.sh runs this.
#
# Usage: [FACET_ROOT=<Facet repo>] [FORCE=1] tools/update-facet.sh
#
# The Facet repo is $FACET_ROOT if set, else ../../Facet-0.2 relative to the MAIN
# checkout (found via the git common dir, so this works from a worktree).
set -euo pipefail

cd "$(dirname "$0")/.."

if [[ -n "${FACET_ROOT:-}" ]]; then
    FACET="$FACET_ROOT"
else
    FACET="$(cd "$(git rev-parse --path-format=absolute --git-common-dir)/.." && pwd)/../../Facet-0.2"
fi
[[ -d "$FACET" ]] || { echo "ERROR: Facet repo not found at $FACET. Set FACET_ROOT." >&2; exit 1; }

# This copy ships with Quartz, so it must be a committed Facet.
if [[ "${FORCE:-}" != 1 && -n "$(git -C "$FACET" status --porcelain)" ]]; then
    echo "$FACET has uncommitted changes; commit them first (or FORCE=1 $0 for a throwaway build)" >&2
    git -C "$FACET" status --short >&2
    exit 1
fi
dotnet build "$FACET/Facet/Facet.csproj" -c Release -v quiet --nologo

DLL="$FACET/Facet/bin/Release/netstandard2.1/Facet.0.2.dll"
mkdir -p lib
cp "$DLL" lib/Facet.0.2.dll
echo "lib/Facet.0.2.dll <- $DLL  md5 $(md5 -q lib/Facet.0.2.dll 2>/dev/null || md5sum lib/Facet.0.2.dll | cut -d' ' -f1)"

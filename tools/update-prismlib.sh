#!/usr/bin/env bash
# Refresh the PrismLib DLLs Quartz builds against and ships, into lib/ (gitignored).
#   PrismLib.dll     the shared half. Shipped inside the zips as PrismLib.bundle: PrismBootstrap
#                    installs it into the shared PrismLib folder when it is newer than what is
#                    there, so a first launch needs no network; the feed updates it afterwards.
#   PrismLib.UI.dll  shipped beside Quartz.dll in the runtime store.
#   Facet.<n>.dll    the engine PrismLib.UI stands on. Feed mode fetches it when the feed lists
#                    one; otherwise any copy already in lib/ is kept (tools/update-facet.sh).
#
# Usage: [DEV=1] [PRISMLIB_ROOT=<PrismLib repo>] tools/update-prismlib.sh
#   (default)  the latest published release, from the feed (prismlib.json); SHA-256 checked per file.
#   DEV=1      copy the local Release build of PrismLib and PrismLib.UI instead ("./dev.sh" in
#              PrismLib builds them); Facet is left to tools/update-facet.sh.
# The PrismLib repo is $PRISMLIB_ROOT if set, else ../../PrismLib-suite relative to the MAIN
# checkout (found via the git common dir, so this works from a worktree).
set -euo pipefail

cd "$(dirname "$0")/.."
mkdir -p lib
md5f() { md5 -q "$1" 2>/dev/null || md5sum "$1" | cut -d' ' -f1; }

if [[ "${DEV:-}" == 1 ]]; then
    if [[ -n "${PRISMLIB_ROOT:-}" ]]; then
        ROOT="$PRISMLIB_ROOT"
    else
        ROOT="$(cd "$(git rev-parse --path-format=absolute --git-common-dir)/.." && pwd)/../../PrismLib-suite"
    fi
    A="$ROOT/PrismLib/bin/Release/PrismLib.dll"
    B="$ROOT/PrismLib.UI/bin/Release/PrismLib.UI.dll"
    if [[ ! -f "$A" || ! -f "$B" ]]; then
        echo "ERROR: $A or $B is missing: run ./dev.sh in PrismLib first (REPOS=\"\" to skip the mod repos)." >&2
        exit 1
    fi
    cp "$A" lib/PrismLib.dll
    cp "$B" lib/PrismLib.UI.dll
    for f in PrismLib.dll PrismLib.UI.dll; do echo "lib/$f  md5 $(md5f "lib/$f")"; done
    # vendor/ holds verbatim copies of PrismLib's bootstrap sources: a local build must match them.
    if [[ -x "$ROOT/tools/check-vendored.sh" ]]; then
        "$ROOT/tools/check-vendored.sh" vendor/*.cs || { echo "ERROR: copy $ROOT/bootstrap/ over the STALE vendor/ files" >&2; exit 1; }
    fi
    exit 0
fi

FEED=$(curl -fsS https://raw.githubusercontent.com/PrismMods/PrismLib/main/prismlib.json)
VER=$(jq -r .version <<<"$FEED")
FACET=$(jq -r '.facet.name // empty' <<<"$FEED")
# The feed's name becomes a path here: only Facet.<major>.<minor>.dll, never one that leaves lib/.
[ -z "$FACET" ] || [[ "$FACET" =~ ^Facet\.[0-9]+\.[0-9]+\.dll$ ]] || { echo "feed $VER names Facet '$FACET'; refusing" >&2; exit 1; }

# The floor (tools/prismlib-floor.txt) is the oldest PrismLib Quartz may ship against. It must be
# raised to the first release with Layout and Facet 0.2 before a Quartz release, so a feed behind it
# is refused here rather than shipped. DEV=1 skips this gate.
FLOOR=$(tr -d '[:space:]' < tools/prismlib-floor.txt)
if [ "$(printf '%s\n%s\n' "$VER" "$FLOOR" | sort -V | head -n1)" != "$FLOOR" ]; then
    echo "feed $VER < floor $FLOOR: publish PrismLib first (tools/prismlib-floor.txt is the floor)" >&2
    exit 1
fi

# Every file is fetched and checked against the feed before any replaces the old one: this is code
# that ships to players, and a half-updated lib/ would pair one release's PrismLib.UI with
# another's Facet.
cd lib
trap 'rm -f PrismLib.dll.tmp PrismLib.UI.dll.tmp ${FACET:+"$FACET.tmp"}' EXIT
fetch() { # url sha256 file
    curl -fsSLo "$3.tmp" "$1"
    if [ "$(shasum -a 256 "$3.tmp" | cut -d' ' -f1)" != "$2" ]; then
        echo "SHA-256 mismatch for $3 (feed $VER); kept the old copies" >&2
        exit 1
    fi
}
fetch "$(jq -r .url <<<"$FEED")" "$(jq -r .sha256 <<<"$FEED")" PrismLib.dll
fetch "$(jq -r .ui.url <<<"$FEED")" "$(jq -r .ui.sha256 <<<"$FEED")" PrismLib.UI.dll
[ -z "$FACET" ] || fetch "$(jq -r .facet.url <<<"$FEED")" "$(jq -r .facet.sha256 <<<"$FEED")" "$FACET"
for f in PrismLib.dll PrismLib.UI.dll ${FACET:+"$FACET"}; do mv "$f.tmp" "$f"; done
if [ -n "$FACET" ]; then
    echo "PrismLib + PrismLib.UI + Facet now $VER"
else
    echo "PrismLib + PrismLib.UI now $VER; the feed has no Facet yet (kept any lib/Facet.*.dll)"
fi

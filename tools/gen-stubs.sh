#!/usr/bin/env bash
# Regenerate stubs/ — the compile-only stand-ins CI builds the mod against.
#
# Usage: tools/gen-stubs.sh [--check]
#   --check  compare against the committed stubs and exit 1 on any difference,
#            writing nothing. This is the drift gate: the stubs are what CI
#            compiles against, so if they disagree with the installed game then
#            CI is proving something about an API that no longer exists.
#
# Needs a real game install (located exactly like build.sh does) plus a local
# build of the mod and every module, since the generator derives the stub set
# from the compiled IL's reference tables rather than from the sources.
set -euo pipefail

cd "$(dirname "$0")/.."

MODE="${1:-}"

# --- Locate the game (same probe order as build.sh) ---
detect_gamepath() {
    if [[ -n "${GAMEPATH:-}" ]]; then echo "$GAMEPATH"; return; fi
    if [[ -f Directory.Build.props ]]; then
        local fromprops
        fromprops="$(grep -oE '<GamePath>[^<]+' Directory.Build.props | sed 's/<GamePath>//' || true)"
        [[ -n "$fromprops" && -d "$fromprops" ]] && { echo "$fromprops"; return; }
    fi
    local candidates=(
        "$HOME/Library/Application Support/Steam/steamapps/common/A Dance of Fire and Ice"
        "$HOME/.local/share/Steam/steamapps/common/A Dance of Fire and Ice"
        "/c/Program Files (x86)/Steam/steamapps/common/A Dance of Fire and Ice"
        "C:/Program Files (x86)/Steam/steamapps/common/A Dance of Fire and Ice"
    )
    for c in "${candidates[@]}"; do [[ -d "$c" ]] && { echo "$c"; return; }; done
    return 1
}

resolve_managed() {
    local gp="$1"
    for d in "ADanceOfFireAndIce.app/Contents/Resources/Data" "A Dance of Fire and Ice_Data"; do
        [[ -d "$gp/$d/Managed" ]] && { echo "$gp/$d/Managed"; return; }
    done
    return 1
}

GP="$(detect_gamepath)" || { echo "ERROR: game install not found. Set GAMEPATH."; exit 1; }
MANAGED="$(resolve_managed "$GP")" || { echo "ERROR: Managed/ not found under $GP"; exit 1; }

# --- Build everything so there is IL to read ---
# BOTH loader targets: LoaderUmm.cs only compiles under QUARTZ_UMM, so a stub set
# derived from the MelonLoader build alone has no UnityModManager surface in it and
# the UMM build then fails against the stubs.
echo ">> building mod (ML + UMM) + modules (Release) to derive the stub set from..."
dotnet build Quartz/Quartz.csproj -c Release -v quiet --nologo >/dev/null
dotnet build Quartz/Quartz.csproj -c Release -p:LoaderTarget=UMM -v quiet --nologo >/dev/null
dotnet build modules/AllModules.proj -c Release -v quiet --nologo >/dev/null

INPUTS=(--input "Quartz/bin/Release/netstandard2.1/Quartz.dll")
[[ -f Quartz/bin/umm/Release/netstandard2.1/QuartzUmm.dll ]] \
    && INPUTS+=(--input "Quartz/bin/umm/Release/netstandard2.1/QuartzUmm.dll")
for dll in modules/*/bin/Release/Quartz.Module.*.dll; do
    INPUTS+=(--input "$dll")
done
echo ">> reading $(( ${#INPUTS[@]} / 2 )) assemblies"

LIBS=(--lib lib)
[[ -d "$GP/MelonLoader/net35" ]] && LIBS+=(--lib "$GP/MelonLoader/net35")
[[ -d "$MANAGED/UnityModManager" ]] && LIBS+=(--lib "$MANAGED/UnityModManager")

# --source feeds the nameof/Harmony-string scan: those names are compile-time only
# and leave no trace in the IL, so they cannot be recovered from --input alone.
dotnet run --project tools/StubGen/StubGen.csproj -c Release -v quiet --nologo -- \
    --game "$MANAGED" \
    "${LIBS[@]}" \
    --source Quartz --source modules --source sdk \
    "${INPUTS[@]}" \
    --out stubs \
    ${MODE:+"$MODE"}

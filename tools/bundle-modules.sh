#!/usr/bin/env bash
# Bundle the built modules into the two install zips, under the data root's
# Module.bundled/ folder.
#
# This is what makes the first-launch migration (ModuleMigration) work offline:
# an upgrading user's core reads their existing per-feature settings, then COPIES
# the modules they had enabled out of Module.bundled/ into Module/. It never
# downloads. Transitional only — Phase 6 drops this step so a fresh install is
# core-only and the zip finally gets small.
#
# Layouts differ per loader:
#   Quartz.zip     Mods/Quartz.dll  + UserData/Quartz/...   (data root = UserData/Quartz)
#   QuartzUmm.zip  Quartz/QuartzUmm.dll + Quartz/UserData/... (data root = Quartz/UserData)
set -euo pipefail

cd "$(dirname "$0")/.."

BUNDLE_DIR="Module.bundled"
modules=(dist/modules/*.qmod)
if [ ! -e "${modules[0]}" ]; then
    echo ">> no modules in dist/modules — nothing to bundle"
    exit 0
fi

command -v zip >/dev/null || { echo "ERROR: 'zip' is required to bundle modules" >&2; exit 1; }

bundle_into() {
    local zip_path="$1" inner="$2"
    [ -f "$zip_path" ] || { echo ">> $zip_path missing — skipping bundle"; return 0; }
    local stage abs_zip
    abs_zip="$PWD/$zip_path"
    stage="$(mktemp -d)"
    mkdir -p "$stage/$inner/$BUNDLE_DIR"
    cp dist/modules/*.qmod dist/modules/*.qmod.json "$stage/$inner/$BUNDLE_DIR/"
    # Append into the existing zip; the top-level dir name is the first path
    # segment of $inner, which is already what the zip stores.
    (cd "$stage" && zip -q -X -r "$abs_zip" "${inner%%/*}")
    rm -rf "$stage"
    echo ">> bundled $(ls dist/modules/*.qmod | wc -l | tr -d ' ') module(s) into $zip_path ($inner/$BUNDLE_DIR)"
}

bundle_into "dist/Quartz.zip" "UserData/Quartz"
bundle_into "dist/QuartzUmm.zip" "Quartz/UserData"

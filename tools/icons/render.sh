#!/usr/bin/env bash
# Renders the nav icon set (24px grid, 2px round stroke) into the embedded 128px PNGs.
set -euo pipefail
cd "$(dirname "$0")"
out=../../Quartz/Resource/Embedded/Image
for svg in *.svg; do
    rsvg-convert -w 128 -h 128 "$svg" -o "$out/${svg%.svg}.png"
done

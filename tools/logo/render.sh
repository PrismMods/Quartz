#!/usr/bin/env bash
# Renders the Quartz logo: the full-colour PNG plus four white layer masks the UI
# tints from the accent (face, shade = shadow + rim, tint = pale facet, shard).
set -euo pipefail
cd "$(dirname "$0")"
out=../../Quartz/Resource/Embedded/Image
rsvg-convert -w 1000 -h 1000 QuartzLogo.svg -o "$out/QuartzLogo.png"
for layer in Face Shade Tint Shard; do
    rsvg-convert -w 512 -h 512 "QuartzLogo$layer.svg" -o "$out/QuartzLogo$layer.png"
done

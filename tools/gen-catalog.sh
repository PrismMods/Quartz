#!/usr/bin/env bash
# gen-catalog.sh — build dist/modules.json from the manifests in dist/modules/.
#
# Every <id>.qmod.json emitted by a module build is copied into the catalog and
# annotated with what only the release knows: the asset size, its sha256, and
# the absolute download URL pinned to THIS tag.
#
# The URL points at the concrete tag rather than the rolling latest-<channel>
# pointer on purpose: the rolling release then only has to carry modules.json
# itself, instead of re-uploading every module asset on each publish.
#
# Output is sorted by module id so re-cutting the same build is byte-identical.
#
# Usage: tools/gen-catalog.sh <tag> [output]
set -euo pipefail

cd "$(dirname "$0")/.."

tag="${1:-}"
out="${2:-dist/modules.json}"
[ -n "$tag" ] || { echo "usage: tools/gen-catalog.sh <tag> [output]" >&2; exit 1; }

command -v jq >/dev/null || { echo "jq is required" >&2; exit 1; }

INFO="Quartz/Core/Info.cs"
owner=$(grep -oE 'RepoOwner\s*=\s*"[^"]+"' "$INFO" | head -1 | sed -E 's/.*"([^"]+)".*/\1/')
repo=$(grep -oE 'RepoName\s*=\s*"[^"]+"' "$INFO" | head -1 | sed -E 's/.*"([^"]+)".*/\1/')
abi=$(grep -oE 'ModuleAbi\s*=\s*[0-9]+' "$INFO" | head -1 | grep -oE '[0-9]+')
base="https://github.com/${owner}/${repo}/releases/download/${tag}"

sha256() {
  if command -v shasum >/dev/null; then shasum -a 256 "$1" | cut -d' ' -f1
  else sha256sum "$1" | cut -d' ' -f1; fi
}
filesize() {
  if stat -f%z "$1" >/dev/null 2>&1; then stat -f%z "$1"; else stat -c%s "$1"; fi
}

entries="[]"
core_version=""
shopt -s nullglob
for manifest in dist/modules/*.qmod.json; do
  id=$(jq -r '.id' "$manifest")
  binary="dist/modules/${id}.qmod"
  [ -f "$binary" ] || { echo "catalog: ${id} has a manifest but no .qmod — aborting" >&2; exit 1; }
  [ "$(jq -r '.coreAbi' "$manifest")" = "$abi" ] || {
    echo "catalog: ${id} targets module ABI $(jq -r '.coreAbi' "$manifest") but this build is ${abi} — aborting" >&2
    exit 1
  }
  [ -z "$core_version" ] && core_version=$(jq -r '.version' "$manifest")
  entries=$(jq \
    --slurpfile m "$manifest" \
    --arg url "${base}/${id}.qmod" \
    --arg murl "${base}/${id}.qmod.json" \
    --arg sha "$(sha256 "$binary")" \
    --arg msha "$(sha256 "$manifest")" \
    --argjson size "$(filesize "$binary")" \
    '. + [$m[0] + {assetName: ($m[0].id + ".qmod"), url: $url, manifestUrl: $murl,
                   size: $size, sha256: $sha, manifestSha256: $msha}]' \
    <<<"$entries")
done
shopt -u nullglob

mkdir -p "$(dirname "$out")"
jq -S --sort-keys \
  --arg version "$core_version" \
  --arg tag "$tag" \
  --argjson abi "$abi" \
  --argjson modules "$(jq 'sort_by(.id)' <<<"$entries")" \
  '{schema: 1, core: {version: $version, abi: $abi, tag: $tag}, groups: ., modules: $modules}' \
  tools/module-groups.json > "$out"

echo "Catalog: ${out} ($(jq '.modules | length' "$out") module(s), tag ${tag})"

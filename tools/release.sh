#!/usr/bin/env bash
# Build a Release and publish it to GitHub with Quartz.zip + Quartz.dll +
# QuartzUmm.zip (UnityModManager) + QuartzModules.zip (every module) attached.
# Pre-release channels (alpha/beta/rc) are flagged as pre-releases.
#
# Release identity (Version + Channel) comes from Quartz/Core/Info.cs — set those
# there first. The per-(version, channel) build number is auto-incremented and
# tracked in build.json (repo root, source-only — never shipped). build.json is
# the only source of truth: the csproj's GenerateBuildInfo target reads it at
# compile time and bakes BuildInfo.Number into the DLL. This never edits Info.cs.
#
# A "full" release also carries a NAME (codename) and a CHANGELOG body:
#
#   -n, --name TEXT        one-line codename, appended to the title after an em
#                          dash:  "v2.0.0-alpha-29 — Editor Readout & BGA"
#   -m, --notes TEXT       changelog body (markdown). Overrides auto-generation.
#   -F, --notes-file PATH  read the changelog body from a file ("-" = stdin).
#       --no-auto          don't fall back to a git-log changelog when no notes
#                          are given (publish with just the build footer).
#       --dry-run          print tag / title / range / body and exit. No build
#                          number bump, no build, no publish — safe to preview.
#   -h, --help             this help.
#
# With no notes flag the script auto-drafts a changelog from the Conventional
# Commit subjects since the previous build's tag, so a bare `./tools/release.sh`
# still ships real notes. NOTE: tags in this repo can sit on older commits and
# builds are sometimes re-cut from one commit, so that range OVER-counts (it can
# include already-released work). For curated, de-duplicated notes use the agent
# flow in agents/release.md — it cross-checks prior release bodies.
#
# Every publish also rolls a "latest-<channel>" tag + pre-release (latest-alpha
# today) onto the build just published, so download links in docs and chat never
# go stale. That tag is intentionally not SemVer-parseable — which is what keeps
# the in-game updater from ever seeing the pointer as a version to offer.
#
# The mod links the game's managed DLLs, so it can't build in CI — run locally.
# Requires `gh` (authenticated), `jq`, and `git`.
set -euo pipefail

cd "$(dirname "$0")/.."

command -v gh  >/dev/null || { echo "gh required: brew install gh (then 'gh auth login')" >&2; exit 1; }
command -v jq  >/dev/null || { echo "jq required: brew install jq" >&2; exit 1; }
command -v git >/dev/null || { echo "git required" >&2; exit 1; }

usage() { sed -n '2,36p' "$0" | sed 's/^# \{0,1\}//'; }

# --- Parse flags ---------------------------------------------------------
name="" ; notes="" ; notes_file="" ; auto=1 ; dry_run=0
while [ $# -gt 0 ]; do
  case "$1" in
    -n|--name)       name="${2:?--name needs a value}"; shift 2 ;;
    -m|--notes)      notes="${2:?--notes needs a value}"; shift 2 ;;
    -F|--notes-file) notes_file="${2:?--notes-file needs a value}"; shift 2 ;;
    --no-auto)       auto=0; shift ;;
    --dry-run)       dry_run=1; shift ;;
    -h|--help)       usage; exit 0 ;;
    *) echo "unknown arg: $1 (try --help)" >&2; exit 2 ;;
  esac
done

INFO="Quartz/Core/Info.cs"
BUILDS="build.json"
[ -f "$BUILDS" ] || echo '{}' > "$BUILDS"

ver=$(grep -E 'const string Version' "$INFO" | sed -E 's/.*"([^"]+)".*/\1/')
chan=$(grep -E 'const string Channel' "$INFO" | sed -E 's/.*"([^"]+)".*/\1/')
owner=$(grep -E 'const string RepoOwner' "$INFO" | sed -E 's/.*"([^"]+)".*/\1/')
repo=$(grep -E 'const string RepoName' "$INFO" | sed -E 's/.*"([^"]+)".*/\1/')

# --- Compute tag, release flags, and the previous tag (changelog base) ----
if [ "$chan" = "stable" ]; then
  tag="v${ver}"
  blurb="Stable release ${ver}."
  flags="--latest"
  bumped=0
  # Best-effort previous release (newest published), used only as a diff base.
  prev_tag=$(gh release list --limit 1 --json tagName -q '.[0].tagName' 2>/dev/null || true)
else
  cur=$(jq -r --arg v "$ver" --arg c "$chan" '.[$v][$c] // 0' "$BUILDS")
  next=$((cur + 1))
  tag="v${ver}-${chan}-${next}"
  blurb="${chan} build ${next} of ${ver}."
  flags="--prerelease --latest=false"
  bumped=1
  # Previous build of THIS lineage is deterministic from the counter.
  if [ "$cur" -gt 0 ]; then prev_tag="v${ver}-${chan}-${cur}"; else
    prev_tag=$(gh release list --limit 1 --json tagName -q '.[0].tagName' 2>/dev/null || true)
  fi
fi

# Only diff from a base we actually have locally.
if [ -n "${prev_tag:-}" ] && git rev-parse -q --verify "refs/tags/${prev_tag}" >/dev/null 2>&1; then
  range="${prev_tag}..HEAD"
else
  prev_tag="" ; range="HEAD"
fi

# --- Auto-draft a changelog from Conventional Commit subjects -------------
gen_changelog() {
  local subj type desc
  local -a feats=() fixes=() perfs=() others=()
  while IFS= read -r subj; do
    [ -n "$subj" ] || continue
    type=$(printf '%s' "$subj" | sed -E 's/^([a-z]+)(\([^)]*\))?!?:.*/\1/')
    desc=$(printf '%s' "$subj" | sed -E 's/^[a-z]+(\([^)]*\))?!?:[[:space:]]*//')
    case "$type" in
      feat) feats+=("$desc") ;;
      fix)  fixes+=("$desc") ;;
      perf) perfs+=("$desc") ;;
      build|chore|ci|docs|style|test|refactor) : ;;  # housekeeping — omit
      *)    others+=("$desc") ;;
    esac
  done < <(git log --no-merges --pretty=%s "$range")

  [ ${#feats[@]}  -gt 0 ] && { printf '### New\n';         printf -- '- %s\n' "${feats[@]}";  printf '\n'; }
  [ ${#fixes[@]}  -gt 0 ] && { printf '### Fixed\n';       printf -- '- %s\n' "${fixes[@]}";  printf '\n'; }
  [ ${#perfs[@]}  -gt 0 ] && { printf '### Performance\n'; printf -- '- %s\n' "${perfs[@]}";  printf '\n'; }
  [ ${#others[@]} -gt 0 ] && { printf '### Other\n';       printf -- '- %s\n' "${others[@]}"; printf '\n'; }
  return 0
}

# --- Assemble the release body -------------------------------------------
if   [ -n "$notes_file" ]; then
  if [ "$notes_file" = "-" ]; then changelog=$(cat); else changelog=$(cat "$notes_file"); fi
elif [ -n "$notes" ]; then
  changelog="$notes"
elif [ "$auto" -eq 1 ]; then
  changelog=$(gen_changelog)
  [ -n "$changelog" ] || changelog="_No notable changes._"
else
  changelog=""
fi

footer="$blurb"
if [ -n "$prev_tag" ]; then footer="${footer}  ·  ${prev_tag}…${tag}"; fi

if [ -n "$changelog" ]; then
  body=$(printf '%s\n\n---\n%s\n' "$changelog" "$footer")
else
  body="$footer"
fi

title="$tag"
if [ -n "$name" ]; then title="${tag} — ${name}"; fi

# --- Dry run: show everything, mutate nothing ----------------------------
if [ "$dry_run" -eq 1 ]; then
  printf '== DRY RUN (no bump, no build, no publish) ==\n'
  printf 'tag:    %s\ntitle:  %s\nrange:  %s\nflags:  %s\n\n----- body -----\n%s\n' \
    "$tag" "$title" "$range" "$flags" "$body"
  exit 0
fi

# --- Commit the build-number bump (prerelease only) ----------------------
if [ "$bumped" -eq 1 ]; then
  tmp=$(mktemp)
  jq --arg v "$ver" --arg c "$chan" --argjson n "$next" '.[$v][$c] = $n' "$BUILDS" > "$tmp" && mv "$tmp" "$BUILDS"
  echo "Build number: ${cur} -> ${next}  (${ver} ${chan})"
fi

echo "Building ${tag} (MelonLoader) ..."
dotnet build Quartz/Quartz.csproj -c Release
echo "Building ${tag} (UnityModManager) ..."
dotnet build Quartz/Quartz.csproj -c Release -p:LoaderTarget=UMM
echo "Building ${tag} (modules) ..."
rm -rf dist/modules
dotnet build modules/AllModules.proj -c Release

# Quartz.zip is the full MelonLoader install (DLL + lang + fonts), built by the
# csproj PostBuild target. Ship the bare DLL alongside it too, so anyone still
# running an old updater (which only looks for a "Quartz.dll" asset) can update.
# QuartzUmm.zip is the self-contained UnityModManager mod folder — the UMM build's
# updater downloads THIS asset (UpdateAssetName), so it must ship every release.
quartz_dll="Quartz/bin/Release/netstandard2.1/Quartz.dll"
quartz_zip="dist/Quartz.zip"
quartz_umm_zip="dist/QuartzUmm.zip"
[ -f "$quartz_dll" ] || { echo "build output missing — aborting" >&2; exit 1; }
[ -f "$quartz_zip" ] || { echo "dist/Quartz.zip missing — aborting" >&2; exit 1; }
[ -f "$quartz_umm_zip" ] || { echo "dist/QuartzUmm.zip missing — aborting" >&2; exit 1; }

# The addon SDK reference assembly, regenerated by the build's PublishAddonSdk
# target. It is NOT committed: it is emitted fresh on every build with a new
# MVID, so tracking it left the working tree dirty after any build. Releases are
# how modders get it — the latest-alpha roll below gives them a stable URL:
#   .../releases/download/latest-alpha/QuartzAddon.dll
# Keep it attached to every release; sdk/QuartzAddon.props errors out without a
# QuartzAddon.dll beside it, so a missing asset means nobody can build a .qaddon.
sdk_dll="sdk/QuartzAddon.dll"
[ -f "$sdk_dll" ] || { echo "sdk/QuartzAddon.dll missing — aborting" >&2; exit 1; }

# Modules ship as ONE asset: QuartzModules.zip, every .qmod and .qmod.json in a
# flat zip. Attaching them loose put 50+ files on the release page and buried the
# two zips a human actually downloads; the whole set is smaller than one install
# zip, so the installer fetches it once and extracts what it needs. The catalog
# is generated from whatever dist/modules/ holds and then checked against it —
# deliberately NOT a hand-maintained per-module asset list, which is the thing
# the module system exists to delete.
catalog="dist/modules.json"
modules_zip="dist/QuartzModules.zip"
on_disk=$(find dist/modules -name '*.qmod' 2>/dev/null | wc -l | tr -d ' ')
[ "$on_disk" -gt 0 ] || { echo "dist/modules holds no modules — the module build failed; aborting" >&2; exit 1; }
rm -f "$modules_zip"
(cd dist/modules && zip -q -X "../$(basename "$modules_zip")" ./*.qmod ./*.qmod.json)
[ -f "$modules_zip" ] || { echo "dist/QuartzModules.zip missing — aborting" >&2; exit 1; }
tools/gen-catalog.sh "$tag" "$modules_zip" "$catalog"
jq -e . "$catalog" >/dev/null || { echo "dist/modules.json is not valid JSON — aborting" >&2; exit 1; }
in_catalog=$(jq '.modules | length' "$catalog")
[ "$on_disk" = "$in_catalog" ] || {
  echo "catalog lists ${in_catalog} module(s) but dist/modules has ${on_disk} — aborting" >&2; exit 1; }
sha_of() { (shasum -a 256 "$1" 2>/dev/null || sha256sum "$1") | cut -d' ' -f1; }
while read -r id want; do
  [ "$(sha_of "dist/modules/${id}.qmod")" = "$want" ] \
    || { echo "catalog sha256 for ${id} does not match the file — aborting" >&2; exit 1; }
done < <(jq -r '.modules[] | "\(.id) \(.sha256)"' "$catalog")
# The installer verifies this before it trusts a single byte out of the zip.
[ "$(sha_of "$modules_zip")" = "$(jq -r '.bundle.sha256' "$catalog")" ] \
  || { echo "catalog sha256 for the module bundle does not match the zip — aborting" >&2; exit 1; }
# The installer looks each module up inside the zip by "<id>.qmod" / "<id>.qmod.json".
# A catalog entry with no matching zip member is a module nobody can install.
zip_members=$(unzip -Z1 "$modules_zip")
while read -r id; do
  for member in "${id}.qmod" "${id}.qmod.json"; do
    grep -qxF "$member" <<<"$zip_members" \
      || { echo "the module bundle has no '${member}' but the catalog lists it — aborting" >&2; exit 1; }
  done
done < <(jq -r '.modules[].id' "$catalog")
module_assets=("$modules_zip")
module_bundle=("$modules_zip")
# Fold the verified modules into both install zips under the data root's
# Module.bundled/. That is the offline source the first-launch migration
# copies from, so an upgrading user keeps every feature they had enabled
# without needing the network. Transitional — dropped in Phase 6.
tools/bundle-modules.sh
# Every module that ships must have a migration rule, or upgrading users
# silently lose that feature on first launch.
while read -r id; do
  grep -q "Id = \"${id}\"" Quartz/Modules/ModuleMigration.cs \
    || { echo "module '${id}' has no rule in ModuleMigration.Table — aborting" >&2; exit 1; }
done < <(jq -r '.modules[].id' "$catalog")

# gh wants the body from a file (preserves markdown + newlines).
notes_tmp=$(mktemp)
printf '%s\n' "$body" > "$notes_tmp"
trap 'rm -f "$notes_tmp"' EXIT

echo "Publishing ${title} ..."
if gh release view "$tag" >/dev/null 2>&1; then
  # Re-publish: refresh title + notes, then replace the assets.
  gh release edit "$tag" --title "$title" --notes-file "$notes_tmp"
  gh release upload "$tag" "$quartz_zip" "$quartz_dll" "$quartz_umm_zip" "$sdk_dll" "$catalog" "${module_assets[@]}" --clobber
else
  # shellcheck disable=SC2086
  gh release create "$tag" "$quartz_zip" "$quartz_dll" "$quartz_umm_zip" "$sdk_dll" "$catalog" "${module_assets[@]}" --title "$title" --notes-file "$notes_tmp" $flags
fi

# --- Roll this channel's "latest" pointer --------------------------------
# A tag + pre-release that always names the newest build of this channel, so
# links in docs and chat never go stale:
#   .../releases/download/latest-alpha/Quartz.zip
#
# The tag is deliberately NOT SemVer-parseable, and that is load-bearing: the
# in-game updater walks the whole releases feed and skips every tag that
# SemVer.TryParse rejects (UpdateService.FetchLatest), so this pointer can never
# be offered as an upgrade or re-nag anyone. Keep it that way — a name like
# "v2.0.0-latest" WOULD parse, and would break exactly that.
#
# Never --latest: GitHub's "Latest" badge belongs on the real release, not on a
# pointer to it.
roll_tag="latest-${chan}"
roll_flags="--latest=false"
[ "$chan" = "stable" ] || roll_flags="--prerelease --latest=false"

roll_pointer() {
  # Point the tag at the commit the real release was cut from rather than at the
  # local HEAD: gh created "$tag" server-side, and a local bump commit or a
  # concurrent session can leave HEAD somewhere else entirely.
  git fetch --tags --force --quiet origin "refs/tags/${tag}:refs/tags/${tag}" || return 1
  git tag -f "$roll_tag" "${tag}^{commit}" >/dev/null || return 1
  # Scoped to this one ref on purpose — a rolling pointer is the only thing in
  # this repo that is ever meant to move.
  git push --force --quiet origin "refs/tags/${roll_tag}" || return 1

  local roll_title roll_notes
  roll_title="Latest ${chan}: ${title}"
  roll_notes=$(mktemp)
  {
    printf 'Rolling pointer to the newest **%s** build — right now that is [%s](https://github.com/%s/%s/releases/tag/%s).\n\n' \
      "$chan" "$title" "$owner" "$repo" "$tag"
    printf 'These links always serve the newest %s build:\n\n' "$chan"
    printf -- '- [Quartz.zip](https://github.com/%s/%s/releases/download/%s/Quartz.zip) — MelonLoader\n' "$owner" "$repo" "$roll_tag"
    printf -- '- [QuartzUmm.zip](https://github.com/%s/%s/releases/download/%s/QuartzUmm.zip) — UnityModManager\n' "$owner" "$repo" "$roll_tag"
    printf -- '- [modules.json](https://github.com/%s/%s/releases/download/%s/modules.json) — module catalog\n' "$owner" "$repo" "$roll_tag"
    printf -- '- [QuartzAddon.dll](https://github.com/%s/%s/releases/download/%s/QuartzAddon.dll) — addon SDK reference assembly\n\n' "$owner" "$repo" "$roll_tag"
    printf -- '---\n\n%s\n' "$body"
  } > "$roll_notes"

  if gh release view "$roll_tag" >/dev/null 2>&1; then
    gh release edit "$roll_tag" --title "$roll_title" --notes-file "$roll_notes" \
      && gh release upload "$roll_tag" "$quartz_zip" "$quartz_dll" "$quartz_umm_zip" "$sdk_dll" "$catalog" "${module_bundle[@]}" --clobber
  else
    # shellcheck disable=SC2086
    gh release create "$roll_tag" "$quartz_zip" "$quartz_dll" "$quartz_umm_zip" "$sdk_dll" "$catalog" "${module_bundle[@]}" \
      --title "$roll_title" --notes-file "$roll_notes" $roll_flags
  fi
  local rc=$?
  rm -f "$roll_notes"
  return $rc
}

echo "Rolling ${roll_tag} -> ${tag} ..."
if roll_pointer; then
  echo "Rolled: ${roll_tag}"
else
  # The real release is already public by now; a stale pointer is a nuisance,
  # not a reason to report the release itself as failed.
  echo "WARNING: could not update ${roll_tag} — ${tag} itself published fine. Re-run to retry." >&2
fi

echo "Done: ${title}"
if [ "$bumped" -eq 1 ]; then
  echo "Commit the bump:  git add ${BUILDS} && git commit -m \"build: bump ${chan} to ${next}\""
fi

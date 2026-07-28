<p align="center">
  <a href="README.md">🇺🇸 English</a> |
  <a href="README.kr.md">🇰🇷 한국어</a> |
  <a href="CREDITS.md">⭐️ Credits</a> |
  <a>📜 Third-party notices</a>
</p>

# Third-party notices

Quartz is distributed under the [GNU General Public License v3](LICENSE). It
incorporates work from the projects listed below. Each entry names the upstream
author, where the original lives, the licence it was released under, and what
Quartz changed — the notice GPLv3 §5(a) asks a modified work to carry.

Nothing here is vendored verbatim: every port is rewritten against Quartz's
module, settings, localization, and diagnostics layers. The behaviour is the
upstream behaviour; the code around it is not the upstream code.

Quartz ports other mods' features besides the ones listed here. This file only
lists entries whose upstream licence has been checked against the actual
repository, so it is not yet a complete inventory.

## DecoPreview

- **Author:** rdzip
- **Source:** https://github.com/rdzip/DecoPreview
- **Licence:** GNU General Public License v3
- **Lives in Quartz as:** the Editor module's *Decoration Preview* page
  (`modules/Editor/EditorDecoPreview.cs`)

Shows each decoration's own image in the level editor's decoration list instead
of the generic per-type icon.

**Modified by the Quartz project on 2026-07-28:**

- Rewritten as a Quartz module feature — the standalone UMM entry point,
  `Harmony` instance, `AssemblyInfo`, and per-mod toggle plumbing are gone;
  patching, unpatching, and lifecycle are the module context's job.
- Put behind an off-by-default `DecoPreview` setting with a localized settings
  page (en-US, ko-KR, zh-CN) instead of being unconditional while loaded.
- Added a restore path: every row Quartz touched is tracked and reverted to its
  captured default size, tag sprite, and vanilla tint when the feature is
  switched off or the mod is unloaded. Upstream relies on `UnpatchAll` and
  leaves already-resized rows resized.
- Added null and zero-size guards around the decoration lookup, the sprite
  bounds division, and the tag image, which upstream dereferences unchecked.
- Dropped the `RDTools` dependency: the `SizeDeltaX`/`SizeDeltaY` extensions it
  called are not present in every supported game build, so the size is assigned
  directly.
- Custom-particle image lookup goes through Quartz's version-tolerant
  `GameApi.EventGet<T>` rather than a direct `LevelEvent` indexer cast.
- Exceptions are routed to `Diag.Ignore` per the repo's swallow convention.

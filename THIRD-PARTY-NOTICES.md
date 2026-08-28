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

Feature ports are rewritten against Quartz's module, settings, localization,
and diagnostics layers. Binary dependencies, where an entry says one is
vendored, are shipped unchanged under their upstream licence.

Quartz ports other mods' features besides the ones listed here. This file only
lists entries whose upstream licence has been checked against the actual
repository, so it is not yet a complete inventory.

## KeyViewer JavaScript engine

The KeyViewer module embeds these unmodified runtime dependencies in its
`.qmod`, so JavaScript plugins work without loose DLLs:

- **Jint 4.16.0**, Copyright (c) 2013 Sebastien Ros, BSD 2-Clause.
  Source: https://github.com/sebastienros/jint/tree/v4.16.0
- **Acornima 1.7.0**, Copyright (c) Adam Simon, BSD 3-Clause.
  Source: https://github.com/adams85/acornima/tree/v1.7.0
- **System.Runtime.CompilerServices.Unsafe 6.0.0**, Copyright (c) .NET
  Foundation and Contributors, MIT.
  Source: https://github.com/dotnet/runtime/tree/v6.0.0

The complete licence texts are embedded beside the binaries from
`modules/KeyViewer/libs/THIRD-PARTY-NOTICES.md`.

## Minecraft module browser engine

The Minecraft module embeds these unmodified runtime dependencies in its
`.qmod`, so the out-of-process browser engine can be driven without loose DLLs:

- **VoltRpc 3.2.1**, Copyright (c) Voltstro, MIT.
  Source: https://github.com/Voltstro-Studios/VoltRpc
- **VoltstroStudios.UnityWebBrowser.Shared 2.2.8**, Copyright (c)
  Voltstro-Studios, MIT.
  Source: https://github.com/Voltstro-Studios/UnityWebBrowser

The browser engine itself is **not** shipped inside the `.qmod` — it is about
133 MB, far past what a single-assembly module can carry. Quartz downloads it on
request from Voltstro's package registry and stores it beside the settings
folder. That payload contains:

- **UnityWebBrowser CEF Engine 2.2.8**, Copyright (c) Voltstro-Studios, MIT.
  Source: https://github.com/Voltstro-Studios/UnityWebBrowser
- **Chromium Embedded Framework**, Copyright (c) Marshall A. Greenblatt,
  BSD 3-Clause. Source: https://bitbucket.org/chromiumembedded/cef
- **Chromium**, Copyright (c) The Chromium Authors, BSD 3-Clause, together with
  the licences of its bundled components, redistributed unchanged inside the
  engine bundle.

The complete licence texts are embedded beside the binaries from
`modules/Minecraft/libs/THIRD-PARTY-NOTICES.md`.

Note that `classic.minecraft.net` is a third-party *service*, not incorporated
code, so it is not a licence entry here — see CREDITS.md for its attribution.

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

## Iridium

- **Author:** Xbodwf
- **Source:** https://github.com/adofaiex/Iridium
- **Licence:** GNU Lesser General Public License v3 (relicensed to GPLv3 here,
  as LGPLv3 §2 permits)
- **Lives in Quartz as:** the *Tile Arc* module, in the Visuals group
  (`modules/TileArc/`), and three optimizations on the Optimizer page
  (`modules/Optimizer/IridiumPatches.cs`)

Rounds the outer corner of every tile turn. Vanilla `FloorMesh` only draws the
big rounded corner inside a narrow angle band and renders every other turn as a
sharp point; the port widens both the radius calculation and the arc gate so
obtuse turns get the same rounded corner.

**Modified by the Quartz project on 2026-08-24:**

- Rewritten as a Quartz module feature — Iridium's `IriPatch` attribute, its
  `PatchPaths` tree, `PatchManager`, `SubSettings`/`UISettings` config layer and
  its `.iml` UI markup are gone; registration, patching, unpatching and settings
  are the module context's job.
- Made runtime-toggleable. Upstream applies or skips the whole transpiler at
  patch time based on `enableCircleArc`. Here the `GetPositions` gate constant is
  not overwritten with PI outright — the transpiler leaves the vanilla constant
  on the stack and inserts a call to `TileArc.ArcGate`, which returns PI only
  while the feature is on, so the switch works without re-patching.
- The radius override (`FloorMesh.SmallestAngleBetweenTwoAngles`) returns the
  vanilla result unchanged when the feature is off, for the same reason.
- Toggling clears `FloorMesh.cache` so tiles meshed afterwards pick up the new
  shape; the UI states that tiles already on screen need a level reload.
- Failure to find either IL anchor logs through `MainCore.Log` and returns the
  original instruction stream instead of Iridium's `Main.Logger`.
- Replaced upstream's fixed corner-radius formula with an *Arc Intensity*
  slider. Upstream hard-codes the substituted angle as `minDiff * 5deg`, which
  lands wherever the game's own angle-to-radius curve happens to put it. Quartz
  inverts that curve instead, so the slider is the corner radius as a fraction
  of the tile's width and the number on screen is the thing being set.
- Toggling or moving the slider re-meshes every `FloorMesh` already in the
  scene, so the change is visible immediately instead of only on tiles built
  afterwards. Mesh colliders are deliberately left alone: they drive editor
  picking rather than gameplay, and refreshing them per tile costs far more
  than the shape delta is worth.
- Given its own localized settings page (en-US, ko-KR, zh-CN) and its own
  `TileArc.json`, off by default.

The Optimizer page carries three more patches derived from Iridium's
`v3/Patches/SceneOptimizationPatches.cs` and
`v3/Patches/ParticleOptimizationPatches.cs`, each behind its own switch:
*Skip Redundant Screen Rescales*, *Skip Idle Particle Updates*, and
*Pause Off-Screen Particles*.

**Modified by the Quartz project on 2026-08-24:**

- The screen-rescale patch does not reimplement `scnGame.Update`. Upstream
  replaces the method body and rewrites the scaling itself, which drops the
  `flashEndscreen` quad this game build also scales. Quartz's prefix only
  decides whether the vanilla body runs: it returns false when the camera's
  orthographic size and aspect both match the previous frame, and otherwise
  lets the game do its own work unchanged.
- The frame-3 level-load branch of `scnGame.Update` is detected and always
  allowed through, and the cached camera state is invalidated on every scene
  load and whenever the Optimizer settings are applied.
- The idle-particle patch is rewritten against this game build. Upstream keys
  off `GetVisible()` and its own pooling; here `SetVisible` deactivates the
  GameObject, so the visibility check is dead and only the per-frame
  `shape.scale` / `main.simulationSpeed` writes are worth skipping.
- Added a `ResetParticle` postfix that re-arms the skip. `ResetParticle` writes
  `shape.scale` directly without updating the field `Update` copies from, so
  vanilla relies on the next `Update` to overwrite it; without the re-arm, a
  skipped frame would leave the wrong shape scale in place. Upstream has no
  equivalent.
- Particle updates are never skipped in the level editor, where the vanilla
  body also drives the gizmo.
- Off-screen particle culling applies and reverts live across every
  `scrParticleDecoration` in the scene rather than only on the next event
  reload, and reverts to Unity's `Automatic` rather than being one-way.
- Iridium's DOTween, multithreading, track-event, player-input and
  `RDInput.GetStateKeys` optimizations are deliberately not ported: each either
  reimplements a method Quartz already patches elsewhere or returns a shared
  buffer across Quartz's own input modules.

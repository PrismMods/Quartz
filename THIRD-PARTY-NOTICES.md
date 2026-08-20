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

## enhanced-countdown

- **Author:** IMPL (GitHub: KGH1113)
- **Source:** https://github.com/KGH1113/enhanced-countdown
- **Licence:** none declared — the upstream repository ships no `LICENSE` file.
  Quartz ports it with the author's explicit permission, granted directly to the
  Quartz project rather than through a public licence.
- **Lives in Quartz as:** the *Metronome* mode of the Countdown module, in the
  Gameplay group (`modules/Countdown/`). It is one of two countdown modes and is
  not the default; the default *Haywire* mode is Quartz's own work.

Replaces the countdown and lead-in for level-editor play-tests started from a
middle tile: the run state is loaded, automatic tiles are stepped through, and
the planets are frozen at the next manual tile's Pure Perfect timestamp while a
metronome loops. The first input stops the metronome and resumes the run from
that exact timestamp.

> **Permission:** granted by IMPL (KGH1113) for Quartz to port and ship this
> work. The grant is to Quartz specifically — the upstream repository is still
> unlicensed, so it confers nothing on anyone else. Thanks to IMPL for writing
> the mod and for allowing Quartz to carry it.

**Modified by the Quartz project on 2026-08-02:**

- Rewritten as a Quartz module — the standalone UMM entry point, bootstrap
  launcher, versioned runtime store, and self-update engine
  (`EnhancedCountdown.Bootstrap`, `EnhancedCountdown.UpdateEngine`) are gone;
  loading, patching, unpatching, and updates are Quartz's job.
- The hexagonal port/adapter layer (`Application/Ports/*`, `ModCompositionRoot`,
  `IModLogger`) is collapsed: each port had exactly one implementation, so the
  concrete classes are called directly and logging goes through `MainCore.Log`
  and `Diag`.
- The in-game metronome control panel is rebuilt in code against Quartz's UI
  stack instead of loading a per-platform Unity `AssetBundle`. Upstream ships
  `win`/`mac`/`linux` prefab bundles beside the DLL; a Quartz module packages as
  a single `.qmod`, and a bundle built for one Unity version will not load on
  both game branches Quartz supports. The time-signature dropdowns became
  steppers as part of that rebuild.
- Metronome tempo, meter, volume, and the icon/panel/planet-animation toggles
  are persisted in `Countdown.json` with a localized settings page (en-US,
  ko-KR, zh-CN), instead of living only for the duration of an editor session.
  Turning the metronome off in the in-game panel stays session-scoped and
  restarts the play-test with the game's own countdown, leaving the persisted
  `Enabled` setting untouched.
- The `AsyncInputManager` clock fields used to re-base input timing after the
  freeze are read and written through cached reflection, so a game build that
  renames or drops them degrades to a no-op instead of breaking the patched
  methods at JIT time.
- Exception handling follows the repo's swallow convention: every catch either
  logs through `Diag.Warn` or is an explicit `Diag.Ignore`, and the upstream
  per-step verbose logging is reduced to the state transitions.

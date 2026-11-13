# Project Agent Guide

This is the high-level map for AI agents working in this repo. Read this first, then read the narrower guides in this folder when they match the task:

- `agents/commits.md` — commit format and staging rules.
- `agents/i18n.md` — localization keys, JSON parity, and translation audits.
- `agents/release.md` — full GitHub release flow and changelog de-duplication.
- `agents/docs.md` — documentation site (quartzz.xyz/docs) styling and the per-release docs update.

## What this project is

Quartz is an all-in-one mod for **A Dance of Fire and Ice**. It builds one shared C# runtime for two loader targets:

- **MelonLoader**: primary/recommended build, packaged as `dist/Quartz.zip` with `Mods/Quartz.dll` plus `UserData/Quartz/*`.
- **UnityModManager**: alternate build, packaged as `dist/QuartzUmm.zip` as a self-contained `Quartz/` mod folder — `QuartzUmm.dll` + `Info.json` at the top, everything the mod reads/writes under `Quartz/UserData/*` (the data root, mirroring MelonLoader's split). Pre-`UserData` flat installs are pulled in one level on first launch by `QuartzRuntime.MigrateModRootDataIntoDataRoot`.

The codebase was formerly KorenResourcePack v2 and still contains migration/self-heal code for old `Koren` user data and `Koren.dll` installs. Treat this as intentional compatibility unless the task is explicitly about removing legacy support.

## Tech stack and build model

- Language: C# with `netstandard2.1` for the mod (`Quartz/Quartz.csproj`).
- Tests: small plain .NET console test project targeting `net10.0` (`Quartz.Tests/Quartz.Tests.csproj`).
- SDK pin: `global.json` asks for .NET SDK `10.0.100` with `rollForward: latestFeature`.
- Formatting: `.editorconfig` uses 4-space C# indentation, tabs for project files, 2-space JSON/Markdown.
- Nullable: mod uses `<Nullable>warnings</Nullable>`; tests use nullable enabled.
- C# style seen in repo: file-scoped namespaces, opening braces on the same line, explicit static feature classes, settings objects with public fields and manual JSON serialize/deserialize.

Local game DLL references come from `Directory.Build.props` / `Directory.Build.example.props` and point at the user's ADOFAI install. Do not assume the project builds on a clean CI machine without the game assemblies.

## Important commands

From repo root:

```sh
./test.sh
```

Runs the console tests in Release:

```sh
dotnet run --project Quartz.Tests/Quartz.Tests.csproj -c Release
```

Build and auto-install/package both loader targets:

```sh
./build.sh [Debug|Release|Debug_IL2CPP|Release_IL2CPP] [ML|UMM|both]
```

Defaults are `Release` and `both`. (`build.sh`'s own header comment only documents the first argument — it's stale; the second one works.)

Examples:

```sh
./build.sh Debug ML
./build.sh Release both
```

Direct builds:

```sh
dotnet build Quartz/Quartz.csproj -c Debug -p:LoaderTarget=ML
dotnet build Quartz/Quartz.csproj -c Debug -p:LoaderTarget=UMM
```

Release publishing is handled by `tools/release.sh`; read `agents/release.md` before using it.

## Repo map

Top level:

- `README.md` / `README.kr.md` — user-facing install/readme; screenshots in `readme/`.
- `AGENTS.md`, `CLAUDE.md` — agent entry points that point here.
- `CREDITS.md` — attribution.
- `Quartz.slnx` — solution file (no `.sln`; some older docs said `Koren.slnx` — that never existed).
- `Quartz/` — main mod source.
- `Quartz.Tests/` — plain .NET tests for Unity-free code (see Tests below).
- `sdk/` — addon SDK shipped to modders (see Addons system).
- `lib/` — gitignored per-developer drop folder for reference assemblies the game install lacks (MelonLoader on a UMM-only box, and vice versa). The install always wins; `lib/` is a fallback search path only, and `Assembly-CSharp*` is never taken from it. Builds also need `lib/Facet.0.2.dll` (the Facet UI engine, shipped beside `Quartz.dll`); run `tools/update-facet.sh` first (`tools/release.sh` runs it itself). They also need `lib/PrismLib.dll` and `lib/PrismLib.UI.dll` (PrismLib, shipped as `PrismLib.bundle` and beside `Quartz.dll`): run `tools/update-prismlib.sh` (`DEV=1` copies a local PrismLib build instead of the published release).
- `scripts/` — `i18n_sync.py`, `validate_i18n.py`.
- `tools/release.sh` — GitHub release automation.
- `build.sh` — local build/auto-install/package script.
- `test.sh` — test runner.
- `build.json` — per-version/channel build counter; generated/read by build and release tooling.
- `dist/` — generated release zips; do not hand-edit.
- `agents/` — workflow docs for AI agents.
- `.github/workflows/` — CI plus the `i18n-push` / `i18n-pull` sync jobs (see `agents/commits.md`).

Main `Quartz/` layout:

- `Loader.cs` — MelonLoader entry point, compiled only when `LoaderTarget != UMM`.
- `LoaderUmm.cs` — UnityModManager entry point, compiled only when `LoaderTarget=UMM`.
- `Core/` — runtime lifecycle, version info, keybinds, service/tick orchestration.
- `Compat/` — small host abstraction interfaces used by both loaders.
- `IO/` — settings persistence, atomic files, profile manager, path utilities.
- `Localization/` — language loader and TMP text localization behavior.
- `Resource/` — embedded images/fonts, exported fonts/lang/presets, resource managers.
- `UI/` — in-game settings UI, page factories, reusable controls, drag/resize/reorganize utilities.
- `Features/` — feature modules and their settings/patches/overlays.
- `Addons/` — user addon system (precompiled `.qaddon`/`.dll` loaded from the data root, built against the `sdk/` reference assembly; separate from built-in `Features/`).
- `Update/` — self-update logic against GitHub releases.
- `Async/`, `Tween/`, `GTweens/`, `Utility/` — supporting runtime utilities.

## Runtime architecture

`Loader.cs` and `LoaderUmm.cs` are thin host bridges. Both call `MainCore.Initialize(IQuartzHost)`, which creates a `QuartzRuntime`.

Key files:

- `Quartz/Core/MainCore.cs` — static facade used throughout the mod (`MainCore.Conf`, `MainCore.Tr`, `MainCore.Log`, `MainCore.Root`, etc.). It null-guards teardown paths.
- `Quartz/Core/QuartzRuntime.cs` — owns the lifecycle. It creates paths/config/resources/root object, initializes services, registers ticks, toggles features, and disposes in reverse order.
- `Quartz/Core/RuntimeServices.cs` — service initialize/dispose list with startup timing logs.
- `Quartz/Core/RuntimeTicks.cs` — per-frame tick list.
- `Quartz/Core/FeatureRegistry.cs` — collects per-feature enable/disable steps; `EnableAll()`/`DisableAll()` run them when the mod is toggled on/off.
- `Quartz/Core/Service/*` — `PathService`, `LocalizationService`, `LangUpdateService` (pulls newer lang files from the `Quartz-i18n` repo at runtime), `UIService`, `TweenService`, `HarmonyService`.
- `Quartz/Core/Info.cs` — project identity (`Version`, `Channel`, GitHub repo info). `Build` comes from generated `BuildInfo.g.cs` based on `build.json`.

`QuartzRuntime.Initialize()` is where most global feature setup happens. Before adding another startup hook, check that file for the existing ordering and disposal expectations.

## Loader-specific behavior

MelonLoader (`Loader.cs`):

- Data root is `<game>/UserData/Quartz`.
- Installed DLL is `<game>/Mods/Quartz.dll`.
- Self-update downloads `Quartz.zip` and extracts over the game root.
- Has `[assembly: HarmonyDontPatchAll]`; `HarmonyService` owns patch lifecycle to avoid double patching.

UnityModManager (`LoaderUmm.cs`):

- Data root is the mod's own folder (`.../Quartz/`).
- Build defines `QUARTZ_UMM` and outputs assembly name `QuartzUmm` while keeping root namespace `Quartz`.
- Entry method is `Quartz.LoaderUmm.Load` via `Quartz/Resource/Umm/Info.json`.
- No UMM IMGUI settings panel; settings live in Quartz's own uGUI menu.
- Self-update downloads `QuartzUmm.zip` and extracts over the UMM mods directory.

When changing loader behavior, check both loader files and the packaging targets in `Quartz/Quartz.csproj`.

## Feature modules

Most features live under `Quartz/Features/<Name>/` and commonly have:

- `<Feature>.cs` — runtime/static feature logic.
- `<Feature>Settings.cs` — persisted settings object.
- `<Feature>Patches.cs` or focused patch files — Harmony patches.
- `<Feature>Overlay.cs` — UI shown during gameplay/editor.
- `Page<Feature>.cs` — the feature's settings page, in the feature folder and namespace.

Every file's namespace matches its folder (`Quartz/Features/KeyViewer/Editor/` is `Quartz.Features.KeyViewer.Editor`). Exceptions: `Quartz/GTweens` (vendored), and the `[Obsolete]` forwarding shim `Game/Stats/LegacyStatusShim.cs` (`Quartz.Features.Status`).

Current feature areas include:

- `AutoDeafen` — Discord/RPC-driven deafen behavior.
- `Combo`, `Judgement`, `ProgressBar`, `SongTitle`, `Panels` — gameplay HUD overlays.
- `Editor` — editor-focused tweaks/readouts/BGA/difficulty behavior.
- `EffectRemover`, `PlanetColors`, `UiHider`, `Tweaks` — visual/gameplay presentation tweaks.
- `InGameOverlay` — applies the mod font to three specific pieces of the game's own native HUD text (song title, countdown, per-hit judgement); replaces an earlier scene-scanning `GameOverlayFont` that was removed for performance.
- `KeyLimiter`, `Restriction` — input/gameplay restrictions. `KeyLimiter` also holds the chatter blocker (`ChatterBlocker.json`).
- `Calibration` — input-offset calibration flow: per-device float offsets, on-death calibration popup, and a detailed timing readout.
- `KeyViewer` — key display overlay and DM Note-compatible CSS parser/rendering.
- `Optimizer` — performance/background execution/process priority toggles.
- `OttoIcon` — Otto icon customization.
- `PlayCount` — run/play count tracking service.
- `Status` — live stat calculations used by panels and overlays.
- `Tuf` — in-game browser for TUF community levels and packs. Contains HTTP API clients (`TufApiClient`, `TufPackApiClient`), zip extraction/asset recovery (`TufArchive`), a shared download cache, and launchers that open levels directly into the live editor scene (`TufLevelLauncher`, `TufLevelActionRunner`). It is the only feature that performs network I/O — all requests go through `TufNetworkPolicy` — and it is wired as a runtime service, not a simple patch module. Its pages form their **own** sidebar category (Levels / Packs / Settings); the backing enum members are named `NostalgiaTuf*` for historical reasons but are not children of the Nostalgia category.
- `Interop` — compatibility bridges/importers for other mods such as XPerfect and UMM settings.

Before editing a feature, trace all three places: the feature module, its settings class, and the page factory that exposes it.

## UI system

The in-game menu is built in code under `Quartz/UI`. It is a two-column sidebar (categories → child pages), not a flat row of tabs.

- `Quartz/UI/UICore.cs` creates the top-level canvas, panel, sidebar, first-run helper, reorganize mode, resize handle, tooltips, and global open/close behavior. It also defines `OriginalMenuState`, the ordered list of built-in pages.
- `Quartz/UI/Factory/MenuFactory.cs` groups `OriginalMenuState` values into sidebar categories via `CategoryChildren`; each category's first value is its representative state. The enum is never persisted by name, so pages can be reordered/regrouped without corrupting saved settings.
- `Quartz/UI/Factory/PageFactory.cs` wires each state to its page builder.
- `Quartz/UI/Factory/Page/Page*.cs` are the core settings pages; feature pages live in `Quartz/Features/<Feature>/Page*.cs`. Several build more than one state via static methods (e.g. `PageGameplay`, `PageVisuals`, `PageTweaks`, `PageEditor`), so one file can back a whole sidebar category.
- `Quartz/UI/Panes/PaneHost.cs` — docked context/live-preview panes shown beside a page (currently wired for Key Viewer).
- `Quartz/UI/Generator/GenerateUI*.cs` creates common rows/controls: toggles, buttons, sliders, inputs, dropdowns, color pickers, keybinds, collapsibles.
- `Quartz/UI/Objects/Impl/*` are backing objects for those controls.
- `Quartz/UI/Utility/Reorganizer.cs` and related utility files handle draggable overlay layout.

The new menu (Facet, UI Toolkit; Unity 6 Mono builds only, never IL2CPP or the KeyViewer flavour):

- **The switch and the hotkey.** `Conf.ExperimentalNewMenu` ("Experimental: new menu", `PRISM_NEW_MENU`) turns it on. `UICore.NewMenuWanted` is the switch AND `FacetBridge.Available()`. `UICore.Toggle`, which the menu hotkey calls, opens the reorganize hub either way, as it always has. The hub's Settings button opens the new menu when it is wanted (out of reorganize mode, through the `fromClassic` hand-off), and the classic panel when it is not or the new one failed to build. Closing the new menu, or Escape, goes back to the hub; the hotkey over it shuts everything. With the switch on, the game's start shows the new menu instead of the classic one. Why: the hotkey keeps the layout screen it has always opened, so the new menu sits one button in, where the classic panel sits.
- **Single writers.** Every row that writes the switch calls `UICore.SetNewMenuSwitch` (saves, builds the new menu ahead when on, rebuilds the classic pages). Every row that writes the menu hotkey outside the classic keybind row calls `UICore.TrySetMenuHotkey`: it refuses Prism's chords, bare modifiers and mouse buttons with one log line, else saves and rebinds PrismLib's key registry. Do not write either field anywhere else.
- **The window.** `QuartzShell` (`Quartz/UI/FacetUi/QuartzShell*.cs`) is one Facet window built from `NavRegistry`. Each category is a rail item, in three ruled groups (`ShellModel.RailGroup`), and a category with a submenu is a `Tabs.Pages` row of its pages. `QuartzShell.Go(pageKey)` opens a page. A page is drawn by its builder in `QuartzShell.Moved` (Home, Features, Settings, Effect Remover and the eight Overlay pages today), as a "feature is off" page for a stub, or as a classic card whose "Open" opens that page in the classic menu. Moving another page into the new menu is one more `Moved` entry. Call `QuartzShell.Changed()` when something a page shows changes without a `NavRegistry.Version` bump: the open page rebuilds.
- **Look.** The window's scale is `UIScale / 0.85 * QuartzShell.BaseScale` (1.2): at the classic menu's default it is larger than that menu, whose text it would otherwise match. Each rail entry shows its category's icon as the classic sidebar picks it (`QuartzShell.CategoryIcon`: an addon's `IconAsset` wins over the built-in sprite). The Sidebar setting (`QuartzShell.SidebarMode`, Settings > Appearance) is Toggleable (default: the rail's button folds it to its icons and `menu.compact` keeps the fold), Always visible, or Always hidden (Facet's compact strip, which widens over the page on hover): old Quartz's rail always folded; folding stays the player's choice, not forced. Cursors are the OS's own, as the classic menu's resize cursors were: `QuartzHost` sets Facet's `HostOptions.Cursor` hook to `NativeCursor` (resize arrows, I-beam, arrow) on Windows and macOS, and Facet draws its own elsewhere. NativeCursor is GPL code by another author, so it stays in Quartz and Facet only takes the hook.
- **Spacing** is the suite look's (PrismLib.UI `Suite.cs`), not Quartz's: rows 38 tall with 6 px above and below text that wraps (`Sizes.RowPadY`), 20 between the tab row and the page (`Sizes.TabGap`), and a 64 px value box, because rows read cramped, the page sat tight under its tabs and "1000 px/s" overflowed its box. In a card, labels start and controls end Pad in from the edge, and the page header's switch ends on the same edge; the 22 px arrow slot exists only in a card with a row that opens a page, because an always-empty slot was why the switches never lined up.
- **Scrolling.** The wheel moves the page Scroll Speed (`C.ScrollSpeed`) as far on screen as the classic menu does: `QuartzHost.WheelSpeed` feeds Facet's `HostOptions.WheelSpeed`, asked on every scroll so the setting applies live. Facet's own steps (60 px a notch, a trackpad's fraction ×400) scrolled about 5× the classic distance.
- **Smooth scrolling** is PrismLib's `scroll.smooth`, one switch for every Prism window: `PrismBridge.Tick` copies it into Facet's shared flag each frame outside the UI half (`FacetBridge.SetSmoothScroll`, which never takes the new menu down), and `UIScrollController` jumps when it is off. With no PrismLib, Facet's flag and the classic menu are both on; with one loaded, even one Quartz could not wire to (Register threw, or another mod's copy is below Quartz's floor), the stored value decides both, and if reading it fails (only a PrismLib older than Config, where every mod's read fails alike) both are on. The bridge reads `Config.Get(Prism.SmoothKey)` through `PrismRules.SmoothOn`, never `Prism.SmoothScroll`, because a published PrismLib lacks that member.
- **Feature pages.** `ShellPage.Feature` frames a moved feature page: a header with the feature's switch and a one-line description, then the cards; given an overlay id, two columns: the header and the cards left, so the header's switch ends over the rows' switches, and that overlay's preview right, stacked below 760. `ShellRows` holds the rows the Overlay pages share (switch, slider, text, colour, anchor, font, shadow group, reset position, status line), each the classic row's twin: the same field, then the overlay's own apply and save, bound through `QuartzShell.Writes`. A switch given the classic toggle's id shares its hotkey, set or cleared from the row's right-click menu (`ShellRows.SetHotkey`), so a hotkey bound in either menu fires the classic toggle and the row shows the new value.
- **Overlay pages** (`Quartz/UI/FacetUi/OverlayPages/`, one per tab): General, Key Viewer, Progress Bar, Combo, Judgement, Song Title, Panels and Calibration, row for row as the classic pages. Two are moved in part: the Key Viewer's canvas editor (its inspector, tab and file strips, per-tab names) and the Panels list stay classic, behind a card that opens the classic menu on that page, and their rows stay classic search hits (`PartlyMoved`). Calibrating itself still runs on the game's own screen.
- **Previews.** `OverlayPreview` (`Quartz/Overlay/`; it names only UnityEngine and UIElements types, and every flavour has `Register`/`Unregister`) lends an overlay's own canvas to one shown pane at a time. `Take` switches the canvas to world space, parks it far off-world on a layer no camera draws (31, else the highest spare) and renders it with a private camera into a texture the stage shows; `Release` puts back render mode, transform and layers and destroys the camera and texture. A hidden pane holds nothing and ticks nothing.
  - An overlay registers its canvas, the rects to frame and the object it shows, plus begin/end hooks when it builds on demand (the calibration popup). It calls `Unregister` first in `Dispose`, so a borrowed canvas goes back before the overlay keeps its position or is destroyed. While `OverlayPreview.Is(id)` it shows itself, with sample values outside a level; in a level, paused or not, it shows the real ones. Its end hook hides the element at once where it rests hidden: given back as lent, it would flash on the real screen until the overlay's next Update. Panels frames only the last panel shown, at true size, so the font the page edits stays legible.
  - Safety nets, because a canvas left borrowed is an element gone from the player's screen: the lease gives back on arranging (`UICore.OnArrangingChanged` fires in the same frame), on a scene change, when its canvas is destroyed or has nothing left to frame, and when a begin, canvas or frame call throws (the overlay still hears its end). Objects made under a lent canvas (a shadow switched on) are moved onto the preview layer, and the restore puts back every object on that layer. The pane asks again every frame while it has no lease, with a note under the stage saying why it is empty.
- **Hand-off.** `MenuHandOff.ReturnTo` says which menu a user's close goes back to. Opening one menu from the other sets it, and a new value replaces the old, so at most one hand-back is ever pending. Code that closes the classic menu to get it out of the way (a level launch, an overlay, a shared layout session) calls `UICore.Dismiss()`, never `Close`: it clears the hand-back and hides the new menu too.
- **Stale classic pages.** A classic toggle never reads its field again. A new-menu row that writes a field the classic menu shows binds through `QuartzShell.Writes(get, set)`, which sets `UICore.ClassicStale`; `UICore.FreshenClassic()` rebuilds the classic menu once before it next shows, and any rebuild clears the flag.
- **Saved state** lives in the host store (`QuartzHost.Host.Read`/`Write`, kept in the settings' `FacetWindows` map). `menu.page` is the page the window last showed; a page that is gone opens Home. `menu.sidebar` is the Sidebar setting (missing: Toggleable, "1" Always visible, "2" Always hidden) and `menu.compact` is "1" while Toggleable's fold is kept. `home.switches` is the comma-separated feature ids of Home's quick switches: a missing key gives the defaults, an empty string none, and unknown ids and repeats drop. The rules are BCL-only in `ShellModel` and unit-tested.
- **Home** draws one status row per `HomeCard` that has `Lines` (the classic Home draws the same lines). A `Lines` that throws shows `HOME_CARD_FAILED` and logs once.
- **Edit layout** hides the window and opens PrismLib's shared layout editor; the window comes back when that session ends, and after no other. `LayoutEditorProbe` (`Quartz/UI/`) sees the editor by its `PrismLayoutEditor` UIDocument without naming a PrismLib type; the Ctrl+Z gate in `PrismWindows` asks it too. Edit layout and the Prism · Settings link show only when `PrismBridge.UiAvailable`; without it the window wears Facet's own look instead of the suite's.
- **Search** is Facet's `Nav` index over the window's pages plus one extra source, `QuartzShell.ClassicRows`: the rows of the pages still in the classic menu, and the Updates and Font rows the moved Settings page leaves there, each opening its classic page. Those rows come from `SettingsSearch.Rows()`, which builds every classic page once and keeps the rows until `NavRegistry.Version` moves. So the first search pays about half a second, and so does the first search after anything that bumps the version, such as switching a feature on or off.
- **Strings.** New-menu keys use the `MENU_` prefix (`Quartz/Resource/Export/Lang/KEY_PREFIXES.md`). Labels are Lang keys, translated live by `QuartzHost`.
- All of Quartz's Facet code lives in `Quartz/UI/FacetUi/`, with one exception: `PrismBridge.Menu.cs`'s `WearSuiteImpl` names `Facet.Theme` to hand it to PrismLib.UI's suite skin, reached only past `UiAvailable` (which means Facet loaded). Outside that folder, reach Facet only through `FacetBridge`'s wrappers, which return a safe default when Facet is missing. A direct Facet call in a per-frame path (`UICore.HandleUpdate`) throws at JIT time on an install without Facet and kills the uGUI menu.
- DEBUG builds run an in-game self-test when the marker file `UserData/Quartz/facet-selftest` exists: about 25 s after launch it writes PASS/FAIL/INFO lines and `DONE pass=N fail=M` to `UserData/Quartz/facet-selftest.log`, then quits the game. Delete the marker afterwards. Its sections (`FacetSelfTestChecks.*.cs`, run in this order): Suite (the look), Shell (first open at most 50 ms, closed tick at most 2 µs, rail, pages, hand-off, a feature flipped while open, last page, language, wheel), Effect Remover, the switch's hand-off (HandOff), Search, Home (`Pages`; it also pins that a switched-off feature keeps its name after a language change), Features, Settings, Polish (the default scale, rail icons, the OS cursor, edges, switches that keep still), Previews (the lease on a scratch window), one per Overlay page (`OverlayPages.*`: rows against the classic page, classic-sees, hotkeys on the first of F13-F15 that no bind of the player's uses, the preview pane), then Escape, text focus, the input block, Chinese text and teardown. It powers Quartz on for the run without saving the switch, and turns on the features its checks open (Effect Remover, Otto Icon, Visual Tweaks) through the DEBUG-only `FeatureService.SetEnabledForRun`, which never writes Features.json; it puts every switch back after and checks that the file kept its bytes. Without Facet it logs `MODE facet-absent` and checks that the hotkey keeps the classic menu with the switch on. Arm one marker per launch: a runner that finds another of `facet-selftest`, `prism-selftest` and `facet-level-selftest` armed logs `FAIL one marker per launch` and quits, because two runs interleave and the first DONE quits before the other puts things back.
- The marker `facet-level-selftest` runs on its own (`FacetSelfTestChecks.InLevel.cs`): it enters 1-1 from the level select and checks the Combo preview in a level (the live count, paused too, the element off every camera, back on screen with nothing left on the preview layer a frame after leaving the page). It fails at once unless it starts from the level select. Entering a level counts as a run in Quartz's play stats: the test puts 1-1's `PlayCount.json` entry back itself (DEBUG `PlayCount.Snapshot`/`Restore`) and checks the attempts end as they began, in memory and in the file's bytes. The game's own save still moves (`User/data.sav` and `data.sav.old`, the current level), so put those back after it.
- Prism's shared windows (Alt+D debug, Alt+P settings) are drawn by `Quartz/Prism/PrismWindows.cs`; Quartz keeps their geometry in its own settings file (`FacetWindows`) under the debounced save, not in a file of its own.
- `tools/update-facet.sh` builds Facet (`FACET_ROOT`, else `../../Facet-0.2` from the main checkout, found through the git common dir so it works from a worktree) and copies `Facet.0.2.dll` into `lib/`; it refuses a Facet tree with uncommitted changes unless `FORCE=1`.

PrismLib (the suite's shared library; Unity 6 Mono builds only, never IL2CPP or the KeyViewer flavour):

- Every file that names a PrismLib or PrismLib.UI type lives in `Quartz/Prism/`, and `PrismBridge` is the only door to it from the rest of Quartz. No PrismLib type in a field or a signature of the bridge: the CLR resolves those when the class loads, before the bootstrap has installed PrismLib. Handles are held as `object`, and every PrismLib call sits in a NoInlining `...Impl` method. Check `PrismBridge.Available` (PrismLib.dll loaded, Quartz registered) before PrismLib work and `UiAvailable` (Facet and PrismLib.UI load) before window work; a failure turns only that half off, with one log line. PrismLib's `tools/check-bridge.sh <Quartz.dll> Quartz.Prism.PrismBridge <ungated methods>` proves the bridge loads without PrismLib.
- `PrismLib.UI.dll`, `PrismLib.bundle` (PrismLib.dll under a name no loader picks up) and `Facet.0.2.dll` ship only in the runtime store (`Runtime/versions/<ver>/`), beside `Quartz.dll`, never in `Mods/` or `UserLibs/`. At startup the bootstrap installs the bundle into the shared `PrismLib/` folder of the mods root when it is newer than the copy there.
- Run `tools/update-prismlib.sh` before building. The default fetches the published release and refuses one below `tools/prismlib-floor.txt`; `DEV=1` copies the local build (`PRISMLIB_ROOT`, else `../../PrismLib-suite` from the main checkout) after PrismLib's `./dev.sh`.
- `vendor/PrismBootstrap.cs` is a verbatim copy of PrismLib's `bootstrap/PrismBootstrap.cs`. Change it in PrismLib and copy it over; `cmp` must find the two identical.
- Layout: every element the reorganizer moves is a target in PrismLib's shared layout editor, under the ids in `Quartz/UI/Utility/LayoutIds.cs`. They are stable because another mod's editor keys its undo steps by them; `panel.N` is only a place in the list, and its target finds the panel by its config. `UICore.IsArranging` is reorganize mode or a shared session (`SharedLayoutOpen`), never both at once, and overlays show for either. A session is open while an editor reads Quartz's boxes: every target's `GetBox` calls `PrismBridge.TouchLayout()`, and the session ends `LayoutSession.Grace` frames after the last read. Quartz draws the editor itself (`QuartzLayout`) when it holds the LayoutEditor claim or PrismLib is absent.
- DEBUG builds run the PrismLib self-test when the marker `prism-selftest` exists in Quartz's data root (`UserData/Quartz/` under MelonLoader, `UMMMods/Quartz/UserData/` under UMM): it writes PASS/FAIL/INFO lines (`MODE …`, `[perf] …`) and `DONE pass=N fail=M` to `prism-selftest.log` beside the marker, then quits the game. Delete the marker afterwards.

Localization convention for UI controls is important: many `GenerateUI` controls auto-localize by normalizing the control `id` into a key. Read `agents/i18n.md` before adding or changing user-facing strings.

## Addons system

`Quartz/Addons/` (namespace `Quartz.Addons`) is an addon loader: **precompiled** `.qaddon`/`.dll` assemblies in the data root's `Addons/` folder are loaded as `QuartzAddon` subclasses. Addons are built against `sdk/QuartzAddon.dll` (the mod's public reference assembly); there is no runtime C# compilation. It is registered like a service in `QuartzRuntime` (`AddonService.Service` + `AddonService.Ticker`), not as a `Features/` module.

- `AddonService.cs` — discovery, load/unload lifecycle, import/remove (`AddonsSettings` persists the per-addon enabled map). Precompiled addons are byte-loaded so their file stays unlocked (deletable/reloadable while loaded); the identity resolver unifies the `Quartz`/`QuartzUmm` reference so one `.qaddon` runs on both loaders.
- `QuartzAddon.cs` — base class addon authors subclass.
- `AddonContext.cs`, `AddonEvents.cs`, `AddonTags.cs` — API surface exposed to addons.
- `AddonSettings.cs` — per-addon persisted settings handle.
- `AddonUI.cs` — lets an addon register its own menu pages; these get dynamic menu states past the `OriginalMenuState` enum and appear as children of the Addons sidebar category.
- `sdk/` (repo root) — the addon SDK: `QuartzAddon.props`, `Directory.Build.props.example`, README. Modders build `.qaddon`s against it. `QuartzAddon.dll` (the reference assembly, regenerated by the build via `ProduceReferenceAssembly` + the `PublishAddonSdk` target) lands here too but is **gitignored** — a fresh MVID every build made the tree dirty after any `build.sh`. `tools/release.sh` attaches it to every release, so modders `curl` it from `latest-alpha`; see `sdk/README.md`.

## Settings and data files

Core settings:

- `Quartz/IO/CoreSettings.cs` persists main UI/mod settings to the loader data root.
- `MainCore.Conf` is the active `CoreSettings`; `MainCore.ConfMgr.RequestSave()` schedules writes.

Feature settings:

- Use `SettingsFile<T>` with settings classes implementing `ISettingsFile`.
- Most settings classes expose public fields and implement manual `Serialize()` / `Deserialize(JToken)` using `IOUtils.Read`.
- Save through each feature's `ConfMgr.RequestSave()` or helper, not by writing files directly.

Resources:

- Embedded resources: `Quartz/Resource/Embedded/**` are compiled into the DLL.
- Exported resources: `Quartz/Resource/Export/**` are copied into `UserData/Quartz` or UMM mod folder during packaging.
- Languages: `Quartz/Resource/Export/Lang/` ships `en-US.json`, `ko-KR.json` and `zh-CN.json`, plus a `KEY_PREFIXES.md` legend. Only **en-US ↔ ko-KR** parity is enforced (by test and by `scripts/validate_i18n.py`); other languages are translator-authored and may lag.
- Presets/fonts are shipped under `Quartz/Resource/Export/`.
- FAQ: the Help ▸ FAQ page reads `FAQ.json` from the data root. The questions are **maintainer-authored** — you edit `Quartz/Resource/Export/FAQ.json` in the repo, and the export pipeline ships it to the data root like `Lang/`, so a mod update refreshes it (local user edits are theirs to keep but get replaced on update). That one file is the single source: it is also embedded (`LogicalName` `Quartz.FAQ.Default.json`, in both `Quartz.csproj` and the test csproj) so `FaqDocument.Default` reads the embedded copy as the in-binary fallback when the on-disk file is missing or unreadable — never edit a second copy. `FaqFile` still seeds the embedded default if the on-disk file is absent. A parse error falls back to that default and shows the error on the page. `FaqDocument` is Unity-free so the parser is covered by tests.

## Tests

`Quartz.Tests/Program.cs` is only the **registry + runner** — a list of `(name, Action)` pairs. The test bodies live in `Quartz.Tests/{Core,IO,Localization,Features}/`; `Support/Assert.cs` is the assertion helper. Add a test by writing it in the matching subfolder and registering it in `Program.cs`.

Current coverage:

- `Core/SemVerTests.cs` — version parsing/formatting, channel and prerelease ordering.
- `IO/` — atomic file replacement, profile name sanitization, profile/preset bundle import.
- `Localization/LocalizationParityTests.cs` — `en-US`/`ko-KR` key parity (only those two; `zh-CN` is translator-owned and allowed to lag).
- `Features/KeyViewer/` — CSS parser (base + extended web effects), layout document round-trip, DM Note import contract, editor snap/zoom/pan, undo-redo history.
- `Features/Tuf/` — network policy, archive safety, difficulty/quantum filters, disk-space probe, install index and library-root guards, pack parsing.

Prefer putting tests here for logic that can be kept free of Unity/ADOFAI types. The test project links selected source files from `Quartz/` instead of referencing the whole Unity mod assembly. This is also the only project CI can build — the mod itself needs local game DLLs.

## Localization rules

Read `agents/i18n.md` before touching strings. Short version:

- Add every key to both `Quartz/Resource/Export/Lang/en-US.json` and `ko-KR.json`. Leave `zh-CN.json` alone — translators own it.
- Builder controls often derive keys from IDs; do not assume the visible label string is enough.
- Missing keys silently fall back to English, so tests may pass while Korean leaks English unless you audit.
- Preserve placeholders, TMP tags, acronyms, and brand terms.

## Build and packaging details

`Quartz/Quartz.csproj` contains important custom MSBuild targets:

- `GenerateBuildInfo` reads `Quartz/Core/Info.cs` and `build.json`, then emits `BuildInfo.g.cs` into `obj/`.
- MelonLoader `PostBuild` copies `Quartz.dll`, exported resources, and native files into package staging and writes `dist/Quartz.zip`.
- UMM `PostBuildUmm` creates a self-contained `Quartz/` mod folder with `Info.json`, exported files, native files, and writes `dist/QuartzUmm.zip`.
- UMM uses separate `obj/umm/...` and `bin/umm/...` paths so it does not clobber the MelonLoader output.

Do not hand-edit `build.json` or `Info.cs` for routine release bumps; use `tools/release.sh` per `agents/release.md`.

## Working-tree caution

This repo often has in-progress user edits. Before editing:

```sh
git status --short
git diff --stat
```

Do not overwrite or casually reformat unrelated work. If a file is already modified, inspect the diff before touching it.

## Common agent workflow

1. Read this file plus any task-specific guide in `agents/`.
2. Check `git status --short` and `git diff --stat`.
3. Locate the relevant feature/page/settings files with search before editing.
4. For UI/user-facing strings, update both language JSON files and run the i18n parity check or `./test.sh`.
5. For feature changes, update the feature logic, settings serialization, and UI page together when needed.
6. Run the narrowest useful verification:
   - `./test.sh` for Unity-free logic/localization/parser changes.
   - `dotnet build Quartz/Quartz.csproj -c Debug -p:LoaderTarget=ML` for mod code if local game references are available.
   - Add `-p:LoaderTarget=UMM` when loader/packaging/shared runtime changes may affect UMM.
7. Summarize changed paths and actual verification output. Do not commit or push unless the user asks.

## Quick path lookup

- Project identity/version: `Quartz/Core/Info.cs`, `build.json`.
- Runtime startup/teardown: `Quartz/Core/QuartzRuntime.cs`, `Quartz/Core/MainCore.cs`.
- MelonLoader host: `Quartz/Loader.cs`.
- UMM host: `Quartz/LoaderUmm.cs`, `Quartz/Resource/Umm/Info.json`.
- Packaging/build targets: `Quartz/Quartz.csproj`, `build.sh`.
- Release script: `tools/release.sh`, `agents/release.md`.
- Commit rules: `agents/commits.md`.
- Localization: `Quartz/Localization/*`, `Quartz/Resource/Export/Lang/*.json`, `agents/i18n.md`.
- Settings persistence: `Quartz/IO/SettingsFile.cs`, `Quartz/IO/CoreSettings.cs`, feature `*Settings.cs` files.
- Main UI: `Quartz/UI/UICore.cs`, `Quartz/UI/Factory/Page/*.cs` (core pages), `Quartz/UI/Generator/*.cs`.
- Feature modules: `Quartz/Features/<Feature>/`.
- Stat panels: `Quartz/Features/Panels/PanelsOverlay.cs`, `PanelsSettings.cs`, `PagePanels*.cs`.
- TUF browser: `Quartz/Features/Tuf/*`, UI in `TufBrowserUI.cs`, `TufPacksUI.cs`, `TufSettingsUI.cs` (same folder).
- Calibration: `Quartz/Features/Calibration/*`, `Quartz/Core/OverlayCalibration.cs`, `CalibrationPopupUI.cs` and `PageCalibration.cs` in the feature folder.
- Addon system: `Quartz/Addons/*` (service wired in `Quartz/Core/QuartzRuntime.cs`).
- Menu sidebar/pages: `Quartz/UI/UICore.cs` (`OriginalMenuState`), `Quartz/UI/Factory/MenuFactory.cs`, `Quartz/UI/Factory/PageFactory.cs`, `Quartz/UI/Factory/Page/*.cs`.
- Key viewer CSS parser: `Quartz/Features/KeyViewer/KeyViewerCss.cs`, tests in `Quartz.Tests/Program.cs`.
- Tests: `Quartz.Tests/Program.cs`, `test.sh`.
- Quartz's `Words` translator (PrismBridge) answers exact English strings globally, so words like "None" or "Menu" are translated for any mod drawing them. Acceptable while the languages agree.

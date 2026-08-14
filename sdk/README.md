# Quartz Addon SDK

A Quartz addon is a **precompiled .NET assembly** with a branded extension:
`.qaddon` (a plain `.dll` works too). You build it against `QuartzAddon.dll` —
the mod's public **reference assembly** (its full public API surface, no
implementation) — so your editor and the C# compiler check the addon before it
ever reaches the game.

Drop the built `.qaddon` into `UserData/Quartz/Addons`. Anything the mod can do,
an addon can do — the game (`Assembly-CSharp`), Unity, Harmony and all of Quartz
are on the reference set.

## What's here

| File | Purpose |
|------|---------|
| `QuartzAddon.props` | Import from your addon `.csproj`; wires up all references + emits the `.qaddon`. |
| `Directory.Build.props.example` | Copy next to your addon if the game path isn't auto-detected. |

`QuartzAddon.dll` is **not** in this folder in a fresh clone — it is a build
output, regenerated with a new identity on every build, so it is not committed.
Download it beside `QuartzAddon.props` before your first build:

```bash
curl -L -o sdk/QuartzAddon.dll \
  https://github.com/PrismMods/Quartz/releases/download/latest-alpha/QuartzAddon.dll
```

That URL always serves the newest alpha. Grab it from a specific build's release
page instead if you want to pin your addon to that version. Building the mod
locally also writes the file. `QuartzAddon.props` stops with a clear error if it
is missing.

## Minimal addon project

```
MyAddon/
  MyAddon.csproj
  MyAddon.cs
```

`MyAddon.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <Import Project="../sdk/QuartzAddon.props" />
  <PropertyGroup>
    <AssemblyName>MyAddon</AssemblyName>
  </PropertyGroup>
</Project>
```

`MyAddon.cs`:

```csharp
using Quartz.Addons;

public class MyAddon : QuartzAddon {
    public override string Name => "My Addon";
    public override void OnLoad() => Context.Msg("loaded");
}
```

Build it:

```bash
dotnet build -c Release
```

Output: `bin/Release/MyAddon.qaddon`. Copy it into
`…/A Dance of Fire and Ice/UserData/Quartz/Addons/`, then press **Reload Addons**
on the Addons tab (or restart the game).

Deploy straight to the game in one step:

```bash
dotnet build -c Release -p:DeployToGame=true
```

## Game path

`QuartzAddon.props` auto-detects the common Steam locations. If your install is
elsewhere, copy `Directory.Build.props.example` to `Directory.Build.props` next
to your `.csproj` and set `GamePath` / `GameData`, or pass them on the command
line:

```bash
dotnet build -c Release -p:GamePath="/path/to/A Dance of Fire and Ice" -p:GameData="A Dance of Fire and Ice_Data"
```

## Iterating

Edit, `dotnet build`, drop the new `.qaddon` in, press **Reload Addons**. The
loader reads the file from a byte copy, so a rebuilt `.qaddon` is picked up on
reload without restarting the game, and can be deleted (or removed from the
Addons page) even while loaded. `QuartzAddon.props` defines `QUARTZ_ADDON`, so
you can guard addon-only code with `#if QUARTZ_ADDON`.

## API surface

Everything an addon overrides or calls lives in `Quartz.Addons`:

- `QuartzAddon` — base class; override `OnLoad` / `OnEnable` / `OnDisable` /
  `OnTick` / `OnUnload` and the `Id` / `Name` / `Version` / `Author` metadata.
  Optional metadata: `Repo` (`"owner/repo"` — Quartz checks GitHub releases and
  shows an update line on the Addons page), `Requires` (hard dependencies by
  addon id; a missing or disabled one shows an actionable error instead of a
  half-loaded addon) and `LoadAfter` (soft ordering). Optional interop:
  override `OnCall(object[])` / `GetApi()` to let other addons talk to yours.
- `AddonContext` (as `Context`) — `Msg`/`Wrn`/`Err`, `GetSettings<T>` /
  `SaveSettings`, `RegisterSettingsTab` (see below), `RegisterStat`,
  `RegisterTag`, `RegisterTab`, `RegisterAction` (buttons under your addon on
  the Addons page), `RegisterTranslations` (raw JSON or an embedded resource,
  same format as the mod's Lang files), `Harmony` / `PatchAll`, and `DataPath`
  (a per-addon folder under `UserData/Quartz/Addons/<id>`, created on first
  use, removed with the addon). Note the settings file `Addon.<id>.json` lives
  in the profile root, so it is per-profile; `DataPath` is global.
  Statics: `AddonContext.Loader` / `IsMelonLoader` / `IsUnityModManager`
  (which mod loader is hosting Quartz), and `IsAddonLoaded(id)` /
  `CallAddon(id, args...)` / `GetAddonApi(id)` for addon-to-addon calls with
  no compile-time reference.
- `AddonEvents` — `LevelStart`, `LevelEnd`, `Hit`, `SceneLoaded`,
  `ModEnabledChanged`.

## Auto-generated settings UI

Call `Context.GetSettings<T>()` then `Context.RegisterSettingsTab("My Tab")`
and Quartz builds a settings tab from your settings class — no UI code. Every
public field/property becomes a control by type:

| Member type | Control |
|---|---|
| `bool` | toggle |
| `enum`, `string` + `[Choices("A", "B")]` | dropdown |
| `int` / `float` / `double` + `[Slider(min, max, step)]` | slider |
| `int` / `float` / `double` | text input (parsed) |
| `string` | text input |
| `UnityEngine.Color` | color picker |
| `Dictionary<string, string>`, `List<KeyValuePair<string, string>>` | key/value row editor |

Attributes: `[Section("Title")]` inserts a header, `[Name("Label")]` overrides
the label (default: the member name split on case), `[Desc("...")]` adds a
tooltip, `[ReloadRequired]` reloads all addons after that option is saved, and
`[Ignore]` skips a member. Middle-click resets a control to the value from
`new T()`. Labels localize automatically under `ADDON_<ID>_<MEMBER>` keys —
supply other languages via `RegisterTranslations`.

Need something the table can't do? Build that tab by hand with
`Context.RegisterTab` and `Quartz.UI.Generator.GenerateUI` instead — toggles,
sliders, dropdowns, inputs, color pickers, key/value rows (`DictRows`),
headers (`Header`) and more, the same builders the mod's own pages use.

## Fail-fast loading

Quartz pre-JITs every method of an addon at load. If the addon was built
against an older Quartz and touches an API that changed, it fails on the
Addons page with the exact member name — instead of exploding mid-run.

See the worked example at `examples/AddonExample` for all of this in use.

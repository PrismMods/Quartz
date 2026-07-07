using Newtonsoft.Json.Linq;
using Quartz.Core;
using Quartz.IO.Interface;
using UnityEngine;

namespace Quartz.IO;

public sealed class CoreSettings : ISettingsFile {
    public bool Active = true;
    public string Language = "en-US";
    public bool IsFirstRun = true;
    public bool ShowOnStartup = false;
    public bool Tooltip = true;
    public bool MiddleClickToDefault = true;
    public float UIScale = 0.85f;
    public string FontName = "";

    // Apply FontName (in place, no repositioning/resizing) to specific pieces of
    // A Dance of Fire and Ice's own native HUD text, not just the mod's UI. Each
    // is independently toggleable and targets exactly one known native
    // component — no scene-wide scanning. All default OFF (opt-in).
    public bool FontSongTitle = false;    // scrHUDText.isTitle
    public bool FontCountdown = false;    // scrCountdown
    public bool FontJudgement = false;    // scrHitTextMesh (the per-hit "Perfect!"/"Early!" popup)

    // Size multiplier on top of each element's auto-fit-to-box size (1 = the
    // game's own size, fit-shrunk only if the mod font would otherwise overflow).
    public float FontSongTitleSize = 1f;
    public float FontCountdownSize = 1f;
    public float FontJudgementSize = 1f;

    // Font for the mod's own settings window. Empty means "follow the overlay
    // font (FontName)"; otherwise a font display name from the same picker, so
    // the settings window can use a different face than the gameplay overlays.
    public string SettingsFontName = "";

    public float ScrollSpeed = 80f;

    // Width (canvas units) of the settings-window white outlines — the panel
    // border ring, the submenu column outline, the top rule and the bottom
    // band edge. Default 6.25 = half the 12.5 corner radius, matching the
    // original border-ring stroke.
    public float OutlineWidth = 6.25f;

    // Settings-window opacity (0..1). Default fully opaque (shown as 100%).
    public float PanelOpacity = 1.0f;

    // User-resized settings-window size, in panel-local (reference) units. 0 =
    // unset → use the screen-derived default. Restored (clamped to screen/min)
    // on next launch so a resize persists across sessions.
    public float PanelWidth = 0f;
    public float PanelHeight = 0f;

    // Height of the settings window's bottom context band (Context/Live
    // Preview panes), in panel-local units. 0 = unset → default height at
    // first build. The band is always visible; this is purely its height.
    public float ContextBandHeight = 0f;

    // Overlay position calibration: the screen resolution the overlay offsets
    // were authored at. Every overlay X/Y offset is scaled by current-screen /
    // this, so a layout lands in the same relative spot on a different monitor.
    // 0 = unset → captured from the current display on first use; the user can
    // re-capture from the Profiles tab ("Recalibrate display").
    public float CalibWidth = 0f;
    public float CalibHeight = 0f;

    public Dictionary<string, bool> CollapsibleStates = [];

    // Menu toggle keybind, stored as ints (Keybind.KeyModifier and KeyCode).
    // Default Alt + K (shown as Option + K on macOS).
    public int ToggleModifier = (int)Keybind.KeyModifier.Alt;
    public int ToggleKey = (int)KeyCode.K;

    // Release channel to accept updates from. Lower (less stable) channels
    // include all higher ones: Alpha gets every build, Stable only final
    // releases. Defaults to Alpha for the current alpha test phase.
    public int UpdateChannel = (int)ReleaseChannel.Alpha;

    // Version tag the user chose to skip (e.g. "v2.0.0-alpha-2"). That build
    // won't be offered again; a newer one still will.
    public string SkippedVersion = "";

    public ReleaseChannel GetUpdateChannel() => (ReleaseChannel)UpdateChannel;

    // Whether a build published on `remote` should be offered to this user —
    // the chosen channel accepts itself and everything more stable.
    public bool AcceptsChannel(ReleaseChannel remote) => remote >= GetUpdateChannel();

    // UI accent color (drives the whole theme via UIColors.ApplyAccent).
    // Default ff9999 (soft red).
    public float AccentR = 1.0f;
    public float AccentG = 0.5995077f;
    public float AccentB = 0.5995077f;

    public Color GetAccentColor() => new(Mathf.Clamp01(AccentR), Mathf.Clamp01(AccentG), Mathf.Clamp01(AccentB), 1f);

    public void SetAccentColor(Color color) {
        AccentR = Mathf.Clamp01(color.r);
        AccentG = Mathf.Clamp01(color.g);
        AccentB = Mathf.Clamp01(color.b);
    }

    public bool GetCollapsibleExpanded(string key)
        => CollapsibleStates.TryGetValue(key, out bool expanded) && expanded;

    public void SetCollapsibleExpanded(string key, bool expanded)
        => CollapsibleStates[key] = expanded;

    public JToken Serialize() {
        JObject collapsibleStates = [];
        foreach(var kvp in CollapsibleStates) collapsibleStates[kvp.Key] = kvp.Value;

        return new JObject {
            [nameof(Active)] = Active,
            [nameof(Language)] = Language,
            [nameof(IsFirstRun)] = IsFirstRun,
            [nameof(ShowOnStartup)] = ShowOnStartup,
            [nameof(Tooltip)] = Tooltip,
            [nameof(MiddleClickToDefault)] = MiddleClickToDefault,
            [nameof(UIScale)] = UIScale,
            [nameof(FontName)] = FontName,
            [nameof(FontSongTitle)] = FontSongTitle,
            [nameof(FontCountdown)] = FontCountdown,
            [nameof(FontJudgement)] = FontJudgement,
            [nameof(FontSongTitleSize)] = FontSongTitleSize,
            [nameof(FontCountdownSize)] = FontCountdownSize,
            [nameof(FontJudgementSize)] = FontJudgementSize,
            [nameof(SettingsFontName)] = SettingsFontName,
            [nameof(ScrollSpeed)] = ScrollSpeed,
            [nameof(OutlineWidth)] = OutlineWidth,
            [nameof(PanelOpacity)] = PanelOpacity,
            [nameof(PanelWidth)] = PanelWidth,
            [nameof(PanelHeight)] = PanelHeight,
            [nameof(ContextBandHeight)] = ContextBandHeight,
            [nameof(CalibWidth)] = CalibWidth,
            [nameof(CalibHeight)] = CalibHeight,
            [nameof(ToggleModifier)] = ToggleModifier,
            [nameof(ToggleKey)] = ToggleKey,
            [nameof(UpdateChannel)] = UpdateChannel,
            [nameof(SkippedVersion)] = SkippedVersion,
            [nameof(CollapsibleStates)] = collapsibleStates,
            [nameof(AccentR)] = AccentR,
            [nameof(AccentG)] = AccentG,
            [nameof(AccentB)] = AccentB
        };
    }

    public void Deserialize(JToken token) {
        Active = IOUtils.Read(token, nameof(Active), Active);
        Language = IOUtils.Read(token, nameof(Language), Language);
        IsFirstRun = IOUtils.Read(token, nameof(IsFirstRun), IsFirstRun);
        ShowOnStartup = IOUtils.Read(token, nameof(ShowOnStartup), ShowOnStartup);
        Tooltip = IOUtils.Read(token, nameof(Tooltip), Tooltip);
        MiddleClickToDefault = IOUtils.Read(token, nameof(MiddleClickToDefault), MiddleClickToDefault);
        UIScale = IOUtils.Read(token, nameof(UIScale), UIScale);
        FontName = IOUtils.Read(token, nameof(FontName), FontName);
        FontSongTitle = IOUtils.Read(token, nameof(FontSongTitle), FontSongTitle);
        FontCountdown = IOUtils.Read(token, nameof(FontCountdown), FontCountdown);
        FontJudgement = IOUtils.Read(token, nameof(FontJudgement), FontJudgement);
        FontSongTitleSize = IOUtils.Read(token, nameof(FontSongTitleSize), FontSongTitleSize);
        FontCountdownSize = IOUtils.Read(token, nameof(FontCountdownSize), FontCountdownSize);
        FontJudgementSize = IOUtils.Read(token, nameof(FontJudgementSize), FontJudgementSize);
        SettingsFontName = IOUtils.Read(token, nameof(SettingsFontName), SettingsFontName);
        ScrollSpeed = IOUtils.Read(token, nameof(ScrollSpeed), ScrollSpeed);
        OutlineWidth = IOUtils.Read(token, nameof(OutlineWidth), OutlineWidth);
        PanelOpacity = IOUtils.Read(token, nameof(PanelOpacity), PanelOpacity);
        PanelWidth = IOUtils.Read(token, nameof(PanelWidth), PanelWidth);
        PanelHeight = IOUtils.Read(token, nameof(PanelHeight), PanelHeight);
        ContextBandHeight = IOUtils.Read(token, nameof(ContextBandHeight), ContextBandHeight);
        CalibWidth = IOUtils.Read(token, nameof(CalibWidth), CalibWidth);
        CalibHeight = IOUtils.Read(token, nameof(CalibHeight), CalibHeight);
        ToggleModifier = IOUtils.Read(token, nameof(ToggleModifier), ToggleModifier);
        ToggleKey = IOUtils.Read(token, nameof(ToggleKey), ToggleKey);
        UpdateChannel = IOUtils.Read(token, nameof(UpdateChannel), UpdateChannel);
        SkippedVersion = IOUtils.Read(token, nameof(SkippedVersion), SkippedVersion);
        CollapsibleStates.Clear();
        if(token[nameof(CollapsibleStates)] is JObject collapsibleStates) {
            foreach(var prop in collapsibleStates.Properties()) {
                try {
                    CollapsibleStates[prop.Name] = prop.Value.Value<bool>();
                } catch { }
            }
        }
        AccentR = IOUtils.Read(token, nameof(AccentR), AccentR);
        AccentG = IOUtils.Read(token, nameof(AccentG), AccentG);
        AccentB = IOUtils.Read(token, nameof(AccentB), AccentB);
    }
}

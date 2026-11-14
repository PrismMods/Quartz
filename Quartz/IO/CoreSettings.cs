using Newtonsoft.Json.Linq;
using Quartz.Core;
using Quartz.IO.Interface;
using UnityEngine;
namespace Quartz.IO;
public sealed class CoreSettings : ISettingsFile {
    public bool Active = true;
    public string Language = "en-US";
    public bool LanguageAutoDetected = false;
    public bool IsFirstRun = true;
    public bool ShowOnStartup = false;
    public bool Tooltip = true;
    public bool MiddleClickToDefault = true;
    public bool BlockInputsWhileMenuOpen = true;
    public float UIScale = 0.85f;
    public string FontName = "";
    public bool FontJudgement = false;
    public float FontJudgementSize = 1f;
    public string SettingsFontName = "";
    public Dictionary<string, (float Scale, float LineSpacing)> FontAdjust = [];
    // Overlay key to font name; a missing key follows FontName.
    public Dictionary<string, string> OverlayFonts = [];
    // Facet window key to its saved state string (position, size); opaque to Quartz.
    public Dictionary<string, string> FacetWindows = [];
    public float ScrollSpeed = 80f;
    public float FpsRefreshInterval = 0f;
    public float PanelOpacity = 1.0f;
    public float PanelWidth = 0f;
    public float PanelHeight = 0f;
    public bool ColorPickerHorizontal = false;
    public float PanelWidthFrac = 0f;
    public float PanelHeightFrac = 0f;
    public float ContextBandHeight = 0f;
    public float CalibWidth = 0f;
    public float CalibHeight = 0f;
    public float KvInspectorWidth = 0f;
    public bool KvSnapToGrid = true;
    public bool KvShowGrid = true;
    public bool KvShowGuides = true;
    public float ExitReorganizeOffsetX = -40f;
    public float ExitReorganizeOffsetY = 40f;
    public float ReorganizePanelX = 18f;
    public float ReorganizePanelY = 18f;
    public Dictionary<string, bool> CollapsibleStates = [];
    public Dictionary<string, (int Modifier, int Key)> ToggleKeybinds = [];
    public int ToggleModifier = (int)Keybind.KeyModifier.Alt;
    public int ToggleKey = (int)KeyCode.K;
    public int KvSwitcherKey = 0;
    public int KvSwitcherModifier = (int)Keybind.KeyModifier.None;
    public bool ExperimentalNewMenu;
    public int UpdateChannel = (int)ReleaseChannel.Alpha;
    public string SkippedVersion = "";
    public bool AprilFoolsForced = false;
    // Clamp: stored 2 (old RC) and 3 (old Stable) would otherwise sit above every channel.
    public ReleaseChannel GetUpdateChannel() =>
        UpdateChannel >= (int)ReleaseChannel.Stable ? ReleaseChannel.Stable : (ReleaseChannel)UpdateChannel;
    public bool AcceptsChannel(ReleaseChannel remote) => remote >= GetUpdateChannel();
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
        JObject toggleKeybinds = [];
        foreach(var kvp in ToggleKeybinds)
            toggleKeybinds[kvp.Key] = new JArray(kvp.Value.Modifier, kvp.Value.Key);
        JObject fontAdjust = [];
        foreach(var kvp in FontAdjust)
            fontAdjust[kvp.Key] = new JArray(kvp.Value.Scale, kvp.Value.LineSpacing);
        JObject overlayFonts = [];
        foreach(var kvp in OverlayFonts) overlayFonts[kvp.Key] = kvp.Value;
        JObject facetWindows = [];
        foreach(var kvp in FacetWindows) facetWindows[kvp.Key] = kvp.Value;
        return new JObject {
            [nameof(Active)] = Active,
            [nameof(Language)] = Language,
            [nameof(LanguageAutoDetected)] = LanguageAutoDetected,
            [nameof(IsFirstRun)] = IsFirstRun,
            [nameof(ShowOnStartup)] = ShowOnStartup,
            [nameof(Tooltip)] = Tooltip,
            [nameof(MiddleClickToDefault)] = MiddleClickToDefault,
            [nameof(BlockInputsWhileMenuOpen)] = BlockInputsWhileMenuOpen,
            [nameof(UIScale)] = UIScale,
            [nameof(FontName)] = FontName,
            [nameof(FontJudgement)] = FontJudgement,
            [nameof(FontJudgementSize)] = FontJudgementSize,
            [nameof(SettingsFontName)] = SettingsFontName,
            [nameof(FontAdjust)] = fontAdjust,
            [nameof(OverlayFonts)] = overlayFonts,
            [nameof(FacetWindows)] = facetWindows,
            [nameof(ScrollSpeed)] = ScrollSpeed,
            [nameof(FpsRefreshInterval)] = FpsRefreshInterval,
            [nameof(PanelOpacity)] = PanelOpacity,
            [nameof(PanelWidth)] = PanelWidth,
            [nameof(PanelHeight)] = PanelHeight,
            [nameof(ColorPickerHorizontal)] = ColorPickerHorizontal,
            [nameof(PanelWidthFrac)] = PanelWidthFrac,
            [nameof(PanelHeightFrac)] = PanelHeightFrac,
            [nameof(ContextBandHeight)] = ContextBandHeight,
            [nameof(CalibWidth)] = CalibWidth,
            [nameof(CalibHeight)] = CalibHeight,
            [nameof(KvInspectorWidth)] = KvInspectorWidth,
            [nameof(KvSnapToGrid)] = KvSnapToGrid,
            [nameof(KvShowGrid)] = KvShowGrid,
            [nameof(KvShowGuides)] = KvShowGuides,
            [nameof(ExitReorganizeOffsetX)] = ExitReorganizeOffsetX,
            [nameof(ExitReorganizeOffsetY)] = ExitReorganizeOffsetY,
            [nameof(ReorganizePanelX)] = ReorganizePanelX,
            [nameof(ReorganizePanelY)] = ReorganizePanelY,
            [nameof(ToggleModifier)] = ToggleModifier,
            [nameof(ToggleKey)] = ToggleKey,
            [nameof(KvSwitcherKey)] = KvSwitcherKey,
            [nameof(KvSwitcherModifier)] = KvSwitcherModifier,
            [nameof(ExperimentalNewMenu)] = ExperimentalNewMenu,
            [nameof(UpdateChannel)] = UpdateChannel,
            [nameof(SkippedVersion)] = SkippedVersion,
            [nameof(AprilFoolsForced)] = AprilFoolsForced,
            [nameof(CollapsibleStates)] = collapsibleStates,
            [nameof(ToggleKeybinds)] = toggleKeybinds,
            [nameof(AccentR)] = AccentR,
            [nameof(AccentG)] = AccentG,
            [nameof(AccentB)] = AccentB
        };
    }
    /// <summary>
    /// Clears a layout switcher chord its own row would refuse now, with one log line: a hand-edited file, or a menu hotkey
    /// moved onto it. Returns whether it cleared one.
    /// </summary>
    internal bool ClearRefusedSwitcherKey() {
        if(KvSwitcherKey == 0) return false;
        if(KvSwitcherModifier >= 0 && KvSwitcherModifier <= (int)Keybind.KeyModifier.Cmd && Enum.IsDefined(typeof(KeyCode), KvSwitcherKey)
            && !Quartz.Prism.PrismRules.SwitcherHotkeyRefused(KvSwitcherModifier, KvSwitcherKey, ToggleModifier, ToggleKey)) return false;
        MainCore.Runtime?.Logger?.Msg($"Layout switcher hotkey {KvSwitcherModifier}+{KvSwitcherKey} cleared: the menu hotkey or another chord fires on it");
        KvSwitcherKey = 0;
        KvSwitcherModifier = 0;
        return true;
    }
    public void Deserialize(JToken token) {
        Active = IOUtils.Read(token, nameof(Active), Active);
        Language = IOUtils.Read(token, nameof(Language), Language);
        // Configs from before auto-detect already have a language; don't override it.
        LanguageAutoDetected = IOUtils.Read(token, nameof(LanguageAutoDetected), true);
        IsFirstRun = IOUtils.Read(token, nameof(IsFirstRun), IsFirstRun);
        ShowOnStartup = IOUtils.Read(token, nameof(ShowOnStartup), ShowOnStartup);
        Tooltip = IOUtils.Read(token, nameof(Tooltip), Tooltip);
        MiddleClickToDefault = IOUtils.Read(token, nameof(MiddleClickToDefault), MiddleClickToDefault);
        BlockInputsWhileMenuOpen = IOUtils.Read(token, nameof(BlockInputsWhileMenuOpen), BlockInputsWhileMenuOpen);
        UIScale = IOUtils.Read(token, nameof(UIScale), UIScale);
        FontName = IOUtils.Read(token, nameof(FontName), FontName);
        FontJudgement = IOUtils.Read(token, nameof(FontJudgement), FontJudgement);
        FontJudgementSize = IOUtils.Read(token, nameof(FontJudgementSize), FontJudgementSize);
        SettingsFontName = IOUtils.Read(token, nameof(SettingsFontName), SettingsFontName);
        ScrollSpeed = IOUtils.Read(token, nameof(ScrollSpeed), ScrollSpeed);
        FpsRefreshInterval = IOUtils.Read(token, nameof(FpsRefreshInterval), FpsRefreshInterval);
        PanelOpacity = IOUtils.Read(token, nameof(PanelOpacity), PanelOpacity);
        PanelWidth = IOUtils.Read(token, nameof(PanelWidth), PanelWidth);
        PanelHeight = IOUtils.Read(token, nameof(PanelHeight), PanelHeight);
        ColorPickerHorizontal = IOUtils.Read(token, nameof(ColorPickerHorizontal), ColorPickerHorizontal);
        PanelWidthFrac = IOUtils.Read(token, nameof(PanelWidthFrac), PanelWidthFrac);
        PanelHeightFrac = IOUtils.Read(token, nameof(PanelHeightFrac), PanelHeightFrac);
        ContextBandHeight = IOUtils.Read(token, nameof(ContextBandHeight), ContextBandHeight);
        CalibWidth = IOUtils.Read(token, nameof(CalibWidth), CalibWidth);
        CalibHeight = IOUtils.Read(token, nameof(CalibHeight), CalibHeight);
        KvInspectorWidth = IOUtils.Read(token, nameof(KvInspectorWidth), KvInspectorWidth);
        KvSnapToGrid = IOUtils.Read(token, nameof(KvSnapToGrid), KvSnapToGrid);
        KvShowGrid = IOUtils.Read(token, nameof(KvShowGrid), KvShowGrid);
        KvShowGuides = IOUtils.Read(token, nameof(KvShowGuides), KvShowGuides);
        ExitReorganizeOffsetX = IOUtils.Read(token, nameof(ExitReorganizeOffsetX), ExitReorganizeOffsetX);
        ExitReorganizeOffsetY = IOUtils.Read(token, nameof(ExitReorganizeOffsetY), ExitReorganizeOffsetY);
        ReorganizePanelX = IOUtils.Read(token, nameof(ReorganizePanelX), ReorganizePanelX);
        ReorganizePanelY = IOUtils.Read(token, nameof(ReorganizePanelY), ReorganizePanelY);
        ToggleModifier = IOUtils.Read(token, nameof(ToggleModifier), ToggleModifier);
        ToggleKey = IOUtils.Read(token, nameof(ToggleKey), ToggleKey);
        KvSwitcherKey = IOUtils.Read(token, nameof(KvSwitcherKey), KvSwitcherKey);
        KvSwitcherModifier = IOUtils.Read(token, nameof(KvSwitcherModifier), KvSwitcherModifier);
        ClearRefusedSwitcherKey();
        ExperimentalNewMenu = IOUtils.Read(token, nameof(ExperimentalNewMenu), false); // a profile without the key is off, never the previous profile's value
        UpdateChannel = IOUtils.Read(token, nameof(UpdateChannel), UpdateChannel);
        SkippedVersion = IOUtils.Read(token, nameof(SkippedVersion), SkippedVersion);
        AprilFoolsForced = IOUtils.Read(token, nameof(AprilFoolsForced), AprilFoolsForced);
        CollapsibleStates.Clear();
        if(token[nameof(CollapsibleStates)] is JObject collapsibleStates) {
            foreach(var prop in collapsibleStates.Properties()) {
                try {
                    CollapsibleStates[prop.Name] = prop.Value.Value<bool>();
                } catch(Exception e) { Diag.Warn(e, "Settings/CollapsibleStates:" + prop.Name); }
            }
        }
        ToggleKeybinds.Clear();
        if(token[nameof(ToggleKeybinds)] is JObject toggleKeybinds) {
            foreach(var prop in toggleKeybinds.Properties()) {
                try {
                    if(prop.Value is not JArray pair || pair.Count != 2) continue;
                    ToggleKeybinds[prop.Name] = (pair[0].Value<int>(), pair[1].Value<int>());
                } catch(Exception e) { Diag.Warn(e, "Settings/ToggleKeybinds:" + prop.Name); }
            }
        }
        FontAdjust.Clear();
        if(token[nameof(FontAdjust)] is JObject fontAdjust) {
            foreach(var prop in fontAdjust.Properties()) {
                try {
                    if(prop.Value is not JArray pair || pair.Count != 2) continue;
                    FontAdjust[prop.Name] = (pair[0].Value<float>(), pair[1].Value<float>());
                } catch(Exception e) { Diag.Warn(e, "Settings/FontAdjust:" + prop.Name); }
            }
        }
        OverlayFonts.Clear();
        if(token[nameof(OverlayFonts)] is JObject overlayFonts) {
            foreach(var prop in overlayFonts.Properties())
                if(prop.Value.Type == JTokenType.String && !string.IsNullOrEmpty((string)prop.Value)) OverlayFonts[prop.Name] = (string)prop.Value;
        }
        FacetWindows.Clear();
        if(token[nameof(FacetWindows)] is JObject facetWindows) {
            foreach(var prop in facetWindows.Properties())
                if(prop.Value.Type == JTokenType.String) FacetWindows[prop.Name] = (string)prop.Value;
        }
        AccentR = IOUtils.Read(token, nameof(AccentR), AccentR);
        AccentG = IOUtils.Read(token, nameof(AccentG), AccentG);
        AccentB = IOUtils.Read(token, nameof(AccentB), AccentB);
    }
}

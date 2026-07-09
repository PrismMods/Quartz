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
    public bool FontSongTitle = false;    
    public bool FontCountdown = false;    
    public bool FontJudgement = false;    
    public float FontSongTitleSize = 1f;
    public float FontCountdownSize = 1f;
    public float FontJudgementSize = 1f;
    public string SettingsFontName = "";
    public float ScrollSpeed = 80f;
    public float OutlineWidth = 6.25f;
    public float PanelOpacity = 1.0f;
    public float PanelWidth = 0f;
    public float PanelHeight = 0f;
    public float ContextBandHeight = 0f;
    public float CalibWidth = 0f;
    public float CalibHeight = 0f;
    public Dictionary<string, bool> CollapsibleStates = [];
    public int ToggleModifier = (int)Keybind.KeyModifier.Alt;
    public int ToggleKey = (int)KeyCode.K;
    public int UpdateChannel = (int)ReleaseChannel.Alpha;
    public string SkippedVersion = "";
    public ReleaseChannel GetUpdateChannel() => (ReleaseChannel)UpdateChannel;
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

using System.Globalization;
using System.Text;
using Quartz.Core;
using Quartz.Game.Stats;
using Quartz.IO;
using Quartz.Resource;
using Quartz.UI;
using Quartz.UI.Utility;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using TMPro;
using Quartz.Overlay;
namespace Quartz.Features.Panels;
public static partial class PanelsOverlay {
    public static SettingsFile<PanelsSettings> ConfMgr { get; private set; }
    public static PanelsSettings Conf => ConfMgr?.Data;
    [Obsolete("The overlay master switch moved to Quartz.Overlay.OverlaySwitch.Enabled.")]
    public static bool IsEnabled => Quartz.Overlay.OverlaySwitch.Enabled;
    public sealed class StatDef {
        public string Id;
        public string Label;
        public string Category;
        public Func<PanelConfig, string> Value;
    }
    public static readonly StatDef[] Catalog = [
        new() { Id = "progress", Category = "Accuracy", Label = "Progress", Value = p =>
            GameStats.RunHasStartProgress
                ? Pct(GameStats.RunStartProgress, p) + " - " + Pct(GameStats.Progress, p)
                : Pct(GameStats.Progress, p) },
        new() { Id = "accuracy", Category = "Accuracy", Label = "Accuracy", Value = p => Pct(GameStats.Accuracy, p) },
        new() { Id = "xaccuracy", Category = "Accuracy", Label = "X-Accuracy", Value = p => Pct(GameStats.XAccuracy, p) },
        new() { Id = "maxaccuracy", Category = "Accuracy", Label = "Max X-Acc", Value = p => Pct(GameStats.MaxXAccuracy, p) },
        new() { Id = "musictime", Category = "Time", Label = "Music Time", Value = _ => GameStats.MusicTimeText },
        new() { Id = "maptime", Category = "Time", Label = "Map Time", Value = _ => GameStats.MapTimeText },
        new() { Id = "checkpoints", Category = "Map Stats", Label = "Checkpoints", Value = _ =>
            GameStats.CheckpointCount.ToString(CultureInfo.InvariantCulture) },
        new() { Id = "tbpm", Category = "BPM", Label = "TBPM", Value = _ => Bpm(true) },
        new() { Id = "cbpm", Category = "BPM", Label = "CBPM", Value = _ => Bpm(false) },
        new() { Id = "kps", Category = "BPM", Label = "KPS", Value = _ => {
            GameStats.GetBpm(out float tbpm, out float cbpm);
            return (cbpm / 60f).ToString("0.##", CultureInfo.InvariantCulture);
        } },
        new() { Id = "autokps", Category = "BPM", Label = "Auto KPS", Value = _ =>
            GameStats.AutoKps.ToString(CultureInfo.InvariantCulture) },
        new() { Id = "hold", Category = "Other", Label = "Holds", Value = _ => {
            string hold = GameStats.HoldBehaviorLabel;
            return string.IsNullOrEmpty(hold) ? null : hold;
        } },
        new() { Id = "timingscale", Category = "Other", Label = "Timing Scale", Value = _ =>
            (GameStats.MarginScale * 100f).ToString("0.##", CultureInfo.InvariantCulture) + "%" },
        new() { Id = "pitch", Category = "Other", Label = "Pitch", Value = _ =>
            (GameStats.Pitch * 100f).ToString("0.##", CultureInfo.InvariantCulture) + "%" },
        new() { Id = "attempt", Category = "Map Stats", Label = "Attempt", Value = _ =>
            GameStats.SessionAttempts.ToString(CultureInfo.InvariantCulture) },
        new() { Id = "totalattempts", Category = "Map Stats", Label = "Total Attempts", Value = _ =>
            GameStats.TotalAttempts.ToString(CultureInfo.InvariantCulture) },
        new() { Id = "best", Category = "Map Stats", Label = "Best", Value = p => {
            float start = GameStats.BestStart;
            return start > 0.0001f
                ? Pct(start, p) + " - " + Pct(GameStats.Best, p)
                : Pct(GameStats.Best, p);
        } },
        new() { Id = "fps", Category = "Other", Label = "FPS", Value = _ =>
            GameStats.Fps.ToString(CultureInfo.InvariantCulture) },
        new() { Id = "text", Category = "Other", Label = "Text", Value = _ => null },
        new() { Id = "image", Category = "Other", Label = "Image", Value = _ => null },
    ];
    private static readonly Dictionary<string, StatDef> CatalogById = Catalog.ToDictionary(
        stat => stat.Id,
        StringComparer.Ordinal
    );
    private static readonly Dictionary<string, string> CatalogLocaleKeys = Catalog.ToDictionary(
        stat => stat.Id,
        stat => LocaleKey("PANEL_STAT_", stat.Id),
        StringComparer.Ordinal
    );
    private static List<StatDef> mergedStats;
    private static int mergedVersion = -1;
    internal static StatDef FindStat(string id) {
        if(string.IsNullOrEmpty(id)) return null;
        if(CatalogById.TryGetValue(id, out StatDef stat)) return stat;
        if(!Quartz.Overlay.StatRegistry.IsRegistered(id)) return null;
        foreach(StatDef candidate in AllStats)
            if(candidate.Id == id) return candidate;
        return null;
    }
    public static IReadOnlyList<StatDef> AllStats {
        get {
            IReadOnlyList<Quartz.Overlay.StatSource> registered = Quartz.Overlay.StatRegistry.Sources;
            if(registered.Count == 0) return Catalog;
            if(mergedStats != null && mergedVersion == Quartz.Overlay.StatRegistry.Version) return mergedStats;
            List<StatDef> all = new(Catalog.Length + registered.Count);
            all.AddRange(Catalog);
            foreach(Quartz.Overlay.StatSource source in registered) {
                all.Add(new StatDef {
                    Id = source.Id,
                    Label = source.Label,
                    Category = source.Category,
                    Value = _ => source.Value(),
                });
            }
            mergedStats = all;
            mergedVersion = Quartz.Overlay.StatRegistry.Version;
            return mergedStats;
        }
    }
    [Obsolete("Register through Quartz.Overlay.StatRegistry instead.")]
    public static void RegisterStat(StatDef stat) {
        if(stat == null || string.IsNullOrWhiteSpace(stat.Id) || stat.Value == null)
            throw new ArgumentException("stat needs an Id and a Value delegate");
        Quartz.Overlay.StatRegistry.Register(new Quartz.Overlay.StatSource {
            Id = stat.Id,
            Label = stat.Label,
            Category = stat.Category,
            Value = () => stat.Value(null),
        });
    }
    [Obsolete("Unregister through Quartz.Overlay.StatRegistry instead.")]
    public static void UnregisterStat(string statId) => Quartz.Overlay.StatRegistry.Unregister(statId);
    public static string LocalizedStatLabel(StatDef stat) {
        if(stat == null) return "";
        if(CatalogLocaleKeys.TryGetValue(stat.Id, out string key)) return MainCore.Tr.Get(key, stat.Label);
        return Quartz.Overlay.StatRegistry.IsRegistered(stat.Id)
            ? MainCore.Tr.Get(LocaleKey("PANEL_STAT_", stat.Id), stat.Label)
            : stat.Label;
    }
    internal static string EffectiveSeparator(string raw) {
        if(string.IsNullOrEmpty(raw)) return " ";
        return raw.Length == 1 ? " " + raw + " " : raw;
    }
    public static string LocalizedCategory(string category)
        => MainCore.Tr.Get(LocaleKey("PANEL_CATEGORY_", category), category);
    private static string LocaleKey(string prefix, string id) {
        if(string.IsNullOrWhiteSpace(id)) return prefix;
        StringBuilder key = new(prefix);
        bool lastUnderscore = false;
        foreach(char raw in id.Trim().ToUpperInvariant()) {
            char c = char.IsLetterOrDigit(raw) ? raw : '_';
            if(c == '_') {
                if(lastUnderscore) continue;
                lastUnderscore = true;
            } else {
                lastUnderscore = false;
            }
            key.Append(c);
        }
        while(key.Length > prefix.Length && key[^1] == '_') key.Length--;
        return key.ToString();
    }
    private static string Bpm(bool tile) {
        GameStats.GetBpm(out float tbpm, out float cbpm);
        return (tile ? tbpm : cbpm).ToString("0.##", CultureInfo.InvariantCulture);
    }
    private static readonly string[] PctFormats = {
        "0", "0.0", "0.00", "0.000", "0.0000", "0.00000", "0.000000"
    };
    private static string Pct(float ratio, PanelConfig p) {
        if(float.IsNaN(ratio) || float.IsInfinity(ratio)) ratio = 0f;
        int d = Mathf.Clamp(p.Decimals, 0, 6);
        return (ratio * 100f).ToString(PctFormats[d], CultureInfo.InvariantCulture) + "%";
    }
    private const float PadX = 14f;
    private const float PadY = 10f;
    private static GameObject canvasObj;
    private static GraphicRaycaster raycaster;
    private static readonly List<LivePanel> panels = [];
    private static Updater updater;
    private static bool ShouldShow() =>
        (OverlaySwitch.Enabled && GameStats.InGame) || UICore.IsReorganizing;
    private static bool gateWant;
    private static bool gateRunning = true;
    internal static void Gate() {
        if(updater == null) return;
        bool want = ShouldShow() || UICore.IsReorganizing;
        bool run = want || gateWant;
        gateWant = want;
        if(gateRunning == run) return;
        gateRunning = run;
        updater.enabled = run;
    }
    public static void EnsureConf() => ConfMgr ??= SettingsFile<PanelsSettings>.Loaded("OverlayPanels.json");
    internal static void Rescale(float fx, float fy) {
        EnsureConf();
        foreach(PanelConfig pc in Conf.Panels) {
            pc.PosX *= fx;
            pc.PosY *= fy;
        }
        Apply();
        Save();
    }
    public static void Save() => ConfMgr?.RequestSave();
    public static void Initialize(GameObject root) {
        if(canvasObj != null) return;
        EnsureConf();
        canvasObj = UnityUtils.CreateOverlayCanvas("QuartzPanelsCanvas", root.transform, 32760, out raycaster);
        BuildPanels();
        updater = canvasObj.AddComponent<Updater>();
        gateWant = true;
        gateRunning = true;
    }
    public static void Dispose() {
        if(canvasObj == null) return;
        SyncPositionsToConfig();
        ConfMgr?.Save();
        foreach(LivePanel p in panels) ReleasePanelImages(p);
        DisposeImageCache();
        Object.Destroy(canvasObj);
        canvasObj = null;
        raycaster = null;
        panels.Clear();
        updater = null;
    }
    public static void Rebuild(PanelConfig skipPositionSync = null) {
        if(canvasObj == null) return;
        SyncPositionsToConfig(skipPositionSync);
        foreach(LivePanel p in panels) {
            ReleasePanelImages(p);
            if(p.Rect != null) Object.Destroy(p.Rect.gameObject);
        }
        panels.Clear();
        BuildPanels();
        Apply();
    }
    public static void SetAnchor(PanelConfig config, PanelAnchor anchor) {
        config.Anchor = (int)anchor;
        Vector2 def = PanelConfig.DefaultOffset(anchor);
        config.PosX = def.x;
        config.PosY = def.y;
        Save();
        Rebuild(config);
    }
    private static void BuildPanels() {
        List<PanelConfig> configs = Conf.Panels;
        for(int i = 0; i < configs.Count; i++) panels.Add(CreatePanel(configs[i]));
        for(int i = configs.Count - 1; i >= 0; i--) panels[i].Rect.SetAsLastSibling();
    }
    public static void Apply() {
        foreach(LivePanel p in panels) ApplyPanel(p);
    }
    private static void ApplyPanel(LivePanel p) {
        if(p?.Config == null) return;
        if(p.Text != null) {
            p.Text.font = FontManager.Current;
            p.Text.fontSize = p.Config.FontSize;
            p.Text.color = p.Config.GetTextColor();
            p.Text.lineSpacing = p.Config.LineSpacing;
            TMPTextShadow.Apply(
                p.Text,
                p.Config.TextShadowEnabled,
                p.Config.TextShadowX,
                p.Config.TextShadowY,
                p.Config.TextShadowSoftness,
                p.Config.GetTextShadowColor()
            );
            p.Dirty = true;
        }
        if(p.Background != null) {
            p.Background.enabled = p.Config.BackgroundEnabled;
            p.Background.color = p.Config.GetBackgroundColor();
        }
    }
    public static void ResetPosition(PanelConfig config) {
        Vector2 def = PanelConfig.DefaultOffset((PanelAnchor)config.Anchor);
        config.PosX = def.x;
        config.PosY = def.y;
        foreach(LivePanel p in panels) {
            if(p.Config == config && p.Rect != null)
                p.Rect.anchoredPosition = OverlayCalibration.Scale(new Vector2(config.PosX, config.PosY));
        }
        Save();
    }
    private static void SyncPositionsToConfig(PanelConfig skip = null) {
        foreach(LivePanel p in panels) {
            if(p.Rect != null && p.Config != null && p.Config != skip) {
                Vector2 stored = OverlayCalibration.Unscale(p.Rect.anchoredPosition);
                p.Config.PosX = stored.x;
                p.Config.PosY = stored.y;
            }
        }
    }
    private static LivePanel CreatePanel(PanelConfig config) {
        GameObject panelObj = new("Panel_" + config.Name);
        panelObj.transform.SetParent(canvasObj.transform, false);
        Vector2 anchor = PanelConfig.AnchorVector((PanelAnchor)config.Anchor);
        RectTransform rect = panelObj.AddComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.anchoredPosition = OverlayCalibration.Scale(new Vector2(config.PosX, config.PosY));
        Image bg = panelObj.AddComponent<Image>();
        bg.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P1024);
        bg.type = Image.Type.Sliced;
        bg.color = config.GetBackgroundColor();
        bg.raycastTarget = false;
        GameObject drag = ReorganizeHandle.CreateDragSurface(rect, () => config.Name, Save);
        GameObject textObj = new("Text");
        textObj.transform.SetParent(rect, false);
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(PadX, PadY);
        textRect.offsetMax = new Vector2(-PadX, -PadY);
        TextMeshProUGUI text = textObj.AddComponent<TextMeshProUGUI>();
        text.font = FontManager.Current;
        text.alignment = anchor.x switch {
            0f => TextAlignmentOptions.TopLeft,
            1f => TextAlignmentOptions.TopRight,
            _ => TextAlignmentOptions.Top,
        };
        text.color = Color.white;
        text.raycastTarget = false;
        text.text = "";
        Quartz.Compat.Game.TextCompat.NoWrap(text);
        LivePanel panel = new() {
            Config = config,
            Rect = rect,
            Background = bg,
            DragObj = drag,
            Text = text,
        };
        ApplyPanel(panel);
        return panel;
    }
}

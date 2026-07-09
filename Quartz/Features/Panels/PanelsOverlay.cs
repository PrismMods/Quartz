using System.Globalization;
using System.Text;
using Quartz.Core;
using Quartz.Features.Interop;
using Quartz.Features.Status;
using Quartz.IO;
using Quartz.Resource;
using Quartz.UI;
using Quartz.UI.Utility;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

using TMPro;

namespace Quartz.Features.Panels;

public static partial class PanelsOverlay {
    public static SettingsFile<PanelsSettings> ConfMgr { get; private set; }
    public static PanelsSettings Conf => ConfMgr?.Data;

    // Master "Enable Overlays" switch, null-safe for the other overlay
    // features (ProgressBar/Combo/Judgement) that gate on it.
    public static bool IsEnabled => ConfMgr?.Data is { Enabled: true };

    // ===== stat catalog =====

    public sealed class StatDef {
        public string Id;
        public string Label;
        // Settings-UI grouping (same categories the old fixed HUD page used).
        public string Category;
        // Returns the line's value text, or null to skip the line entirely.
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
            // Checkpoint bests render as a "start - best" range, like Progress.
            float start = GameStats.BestStart;
            return start > 0.0001f
                ? Pct(start, p) + " - " + Pct(GameStats.Best, p)
                : Pct(GameStats.Best, p);
        } },
        new() { Id = "fps", Category = "Other", Label = "FPS", Value = _ =>
            GameStats.Fps.ToString(CultureInfo.InvariantCulture) },
        // Free-form custom text. The value comes from the per-entry StatEntry.Text
        // (handled directly in UpdatePanel), not this delegate — multiple "text"
        // entries can coexist on one panel, each with its own string.
        new() { Id = "text", Category = "Other", Label = "Text", Value = _ => null },
        // XPerfect perfect breakdown. Value returns null (line hidden) unless the
        // XPerfect mod is active, so the panel only shows them when meaningful.
        new() { Id = "xperfect", Category = "Accuracy", Label = "X Perfect", Value = _ =>
            XPerfectBridge.Active ? GameStats.XPerfectX.ToString(CultureInfo.InvariantCulture) : null },
        new() { Id = "plusperfect", Category = "Accuracy", Label = "+ Perfect", Value = _ =>
            XPerfectBridge.Active ? GameStats.XPerfectPlus.ToString(CultureInfo.InvariantCulture) : null },
        new() { Id = "minusperfect", Category = "Accuracy", Label = "- Perfect", Value = _ =>
            XPerfectBridge.Active ? GameStats.XPerfectMinus.ToString(CultureInfo.InvariantCulture) : null },
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

    // Addon-registered stats. They join the built-in Catalog everywhere —
    // stat picker, lookup, localization (their locale key falls back to the
    // registered Label). A panel entry whose addon stat is gone (addon
    // unloaded/disabled) simply renders nothing: FindStat misses and
    // UpdatePanel skips the line, exactly like any unknown id.
    private static readonly List<StatDef> addonStats = [];

    // Built-ins first, addon stats after, for the settings-UI picker.
    public static IReadOnlyList<StatDef> AllStats {
        get {
            if(addonStats.Count == 0) return Catalog;
            List<StatDef> all = new(Catalog.Length + addonStats.Count);
            all.AddRange(Catalog);
            all.AddRange(addonStats);
            return all;
        }
    }

    public static void RegisterStat(StatDef stat) {
        if(stat == null || string.IsNullOrWhiteSpace(stat.Id) || stat.Value == null)
            throw new ArgumentException("stat needs an Id and a Value delegate");
        if(CatalogById.ContainsKey(stat.Id))
            throw new InvalidOperationException($"stat id '{stat.Id}' is already registered");

        addonStats.Add(stat);
        CatalogById[stat.Id] = stat;
        CatalogLocaleKeys[stat.Id] = LocaleKey("PANEL_STAT_", stat.Id);
    }

    public static void UnregisterStat(string statId) {
        if(string.IsNullOrEmpty(statId)) return;
        int removed = addonStats.RemoveAll(s => s.Id == statId);
        if(removed == 0) return; // never removes a built-in
        CatalogById.Remove(statId);
        CatalogLocaleKeys.Remove(statId);
    }

    public static string LocalizedStatLabel(StatDef stat) {
        if(stat == null) return "";

        return CatalogLocaleKeys.TryGetValue(stat.Id, out string key)
            ? MainCore.Tr.Get(key, stat.Label)
            : stat.Label;
    }

    // The text drawn between a stat's label and its value. Stored raw, padded
    // here so a tidy single-character separator doesn't need manual spaces:
    //   ""          -> a single space
    //   "|"  (1 ch) -> " | "  (a space added each side)
    //   "::" (2+ ch) -> used verbatim (the user supplied their own spacing)
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

    // Precomputed "0", "0.0" … "0.000000" so Pct (called per stat per panel per
    // frame) doesn't rebuild `"0." + new string('0', d)` on every call.
    private static readonly string[] PctFormats = {
        "0", "0.0", "0.00", "0.000", "0.0000", "0.00000", "0.000000"
    };

    private static string Pct(float ratio, PanelConfig p) {
        if(float.IsNaN(ratio) || float.IsInfinity(ratio)) ratio = 0f;

        int d = Mathf.Clamp(p.Decimals, 0, 6);
        return (ratio * 100f).ToString(PctFormats[d], CultureInfo.InvariantCulture) + "%";
    }

    // ===== lifecycle =====

    private const float PadX = 14f;
    private const float PadY = 10f;

    private static GameObject canvasObj;
    private static GraphicRaycaster raycaster;
    private static readonly List<LivePanel> panels = [];
    private static Updater updater;

    public static void EnsureConf() => ConfMgr ??= SettingsFile<PanelsSettings>.Loaded("OverlayPanels.json");

    public static void Save() => ConfMgr?.RequestSave();

    public static void Initialize(GameObject root) {
        if(canvasObj != null) return;

        EnsureConf();

        canvasObj = UnityUtils.CreateOverlayCanvas("QuartzPanelsCanvas", root.transform, 32760, out raycaster);

        BuildPanels();

        updater = canvasObj.AddComponent<Updater>();
    }

    public static void Dispose() {
        if(canvasObj == null) return;

        SyncPositionsToConfig();
        ConfMgr?.Save();

        Object.Destroy(canvasObj);
        canvasObj = null;
        raycaster = null;
        panels.Clear();
        updater = null;
    }

    // Tears the live panel objects down and rebuilds them from config —
    // called from the settings UI after add/delete/rename. skipPositionSync:
    // a panel whose config position was just rewritten (anchor change) and
    // must not be clobbered by its live rect's old-anchor position.
    public static void Rebuild(PanelConfig skipPositionSync = null) {
        if(canvasObj == null) return;

        SyncPositionsToConfig(skipPositionSync);

        foreach(LivePanel p in panels)
            if(p.Rect != null) Object.Destroy(p.Rect.gameObject);
        panels.Clear();

        BuildPanels();
        Apply();
    }

    // Changes a panel's anchor preset. Offsets are relative to the anchor, so
    // the old offset is meaningless at the new corner — snap to the new
    // corner's default inset. The rebuild skips syncing this panel: its live
    // rect still holds the old-anchor position, which would overwrite the
    // fresh default and strand the panel off-screen.
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

        // Layer order: config index 0 is the front-most panel. Unity UI draws
        // later siblings on top, so push them back-to-front — index 0 ends up
        // last in the hierarchy and renders over the rest where they overlap.
        // Matches the settings list, where the top section is the top layer.
        for(int i = configs.Count - 1; i >= 0; i--) panels[i].Rect.SetAsLastSibling();
    }

    // Re-applies appearance settings to the live panels (UI change).
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
            // FontSize/lineSpacing change the preferred size, which UpdatePanel
            // only recomputes when the body changes — force one re-measure.
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
        // Text follows the anchor's horizontal side, like the old left/right
        // panels did (right-anchored panels read right-aligned).
        text.alignment = anchor.x switch {
            0f => TextAlignmentOptions.TopLeft,
            1f => TextAlignmentOptions.TopRight,
            _ => TextAlignmentOptions.Top,
        };
        text.color = Color.white;
        text.raycastTarget = false;
        text.text = "";

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

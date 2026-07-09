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

// LivePanel + Updater. Extracted from PanelsOverlay.cs.
public static partial class PanelsOverlay {

    private sealed class LivePanel {
        public PanelConfig Config;
        public RectTransform Rect;
        public Image Background;
        public GameObject DragObj;
        public TextMeshProUGUI Text;

        // Per-frame change-guard state (see UpdatePanel). LastBody = null forces
        // the first render; Dirty is raised by Apply()/reactivation to force one
        // re-measure when appearance changed but the body string did not.
        public string LastBody;
        public bool Dirty = true;

        // Cached EffectiveSeparator result. SeparatorSource is the raw LabelSeparator
        // it was built from, so UpdatePanel recomputes only when the setting changes
        // (otherwise the 1-char default " x " string was re-allocated each refresh).
        public string SeparatorSource;
        public string Separator;
    }

    private sealed class Updater : MonoBehaviour {
        private const float TextRefreshInterval = 0.05f;
        private readonly StringBuilder sb = new();
        private float nextTextRefresh;
        private bool lastShow;
        private bool lastReorganizing;

        private void Update() {
            bool isReorganizing = UICore.IsReorganizing;
            bool show = (IsEnabled && GameStats.InGame) || isReorganizing;
            if(raycaster != null && raycaster.enabled != isReorganizing) raycaster.enabled = isReorganizing;
            float now = Time.unscaledTime;
            bool stateChanged = show != lastShow || isReorganizing != lastReorganizing;
            bool refreshText = stateChanged || now >= nextTextRefresh;
            if(refreshText) nextTextRefresh = now + TextRefreshInterval;
            lastShow = show;
            lastReorganizing = isReorganizing;

            foreach(LivePanel p in panels)
                UpdatePanel(p, show, isReorganizing, refreshText || p.Dirty);
        }

        private void UpdatePanel(LivePanel p, bool show, bool isReorganizing, bool refreshText) {
            if(p?.Text == null || p.Rect == null) return;

            if(p.DragObj != null && p.DragObj.activeSelf != isReorganizing) p.DragObj.SetActive(isReorganizing);

            if(!show) {
                if(p.Rect.gameObject.activeSelf) p.Rect.gameObject.SetActive(false);
                return;
            }

            if(!refreshText) {
                if(isReorganizing) SyncPosition(p);
                return;
            }

            sb.Clear();

            if(show) {
                PanelConfig c = p.Config;
                // The separator depends only on c.LabelSeparator (a settings value,
                // never mutated per frame). Cache it on the panel keyed on the raw
                // source so EffectiveSeparator (which allocates " x " for the 1-char
                // default) runs only when the setting actually changes.
                if(p.Separator == null || p.SeparatorSource != c.LabelSeparator) {
                    p.SeparatorSource = c.LabelSeparator;
                    p.Separator = EffectiveSeparator(c.LabelSeparator);
                }
                string separator = p.Separator;
                if(!string.IsNullOrEmpty(c.Prefix)) sb.AppendLine(c.Prefix);

                for(int i = 0; i < c.Stats.Count; i++) {
                    StatEntry entry = c.Stats[i];
                    if(!entry.Enabled) continue;

                    StatDef stat = FindStat(entry.Id);
                    if(stat == null) continue;

                    string value;
                    if(entry.Id == "text") {
                        // The "text" stat renders the entry's own custom string,
                        // with {TagName} placeholders substituted by addon tags
                        // (and built-in stat ids like {fps}). An empty result —
                        // literally empty or all-empty tags — is skipped so it
                        // leaves no blank line.
                        if(string.IsNullOrEmpty(entry.Text)) continue;
                        value = Quartz.Addons.AddonTags.Interpolate(entry.Text, name => ResolveStatToken(name, c));
                        if(string.IsNullOrEmpty(value)) continue;
                    } else {
                        try { value = stat.Value(c); }
                        catch { continue; }
                        if(value == null) continue;
                    }

                    // English by default; localized only when this panel opts
                    // in (the settings UI always shows localized labels though).
                    // Skipped entirely when the entry hides its label (number only).
                    if(entry.ShowLabel) {
                        string label = c.LocalizeStatLabels
                            ? LocalizedStatLabel(stat)
                            : stat.Label;
                        sb.Append(label).Append(separator);
                    }

                    // Per-stat value coloring (v1 ColorRange): tint the value
                    // by the stat's own ratio through the entry's gradient.
                    StatColor color = entry.Color;
                    if(color is { Enabled: true }) {
                        Color tint = color.Evaluate(ColorRatio(entry.Id, color));
                        sb.Append("<color=#");
                        AppendHex(sb, tint);
                        sb.Append('>').Append(value).AppendLine("</color>");
                    } else {
                        sb.AppendLine(value);
                    }
                }
            }

            int bodyLength = TrimmedBodyLength(sb);
            string body = BuilderEquals(sb, bodyLength, p.LastBody)
                ? p.LastBody
                : bodyLength == 0 ? "" : sb.ToString(0, bodyLength);
            // Reorganize mode forces an empty panel to render its name so the
            // user has a hit target to grab.
            if(isReorganizing && body.Length == 0) body = p.Config.Name;

            bool active = body.Length > 0 || isReorganizing;
            if(p.Rect.gameObject.activeSelf != active) {
                p.Rect.gameObject.SetActive(active);
                // Re-show: shadow layers may have been disabled while hidden, so
                // force a full text + shadow re-sync on the next applied frame.
                if(active) p.Dirty = true;
            }

            if(!active) return;

            TMP_FontAsset font = FontManager.Current;
            bool fontChanged = p.Text.font != font;
            if(fontChanged) p.Text.font = font;

            // Text values are sampled at 20 Hz; only re-tessellate, re-measure,
            // and re-sync the shadow when the sampled body actually changed.
            if(fontChanged || p.Dirty || body != p.LastBody) {
                p.Text.text = body;
                Vector2 pref = p.Text.GetPreferredValues(body);
                p.Rect.sizeDelta = new Vector2(pref.x + PadX * 2f, pref.y + PadY * 2f);
                TMPTextShadow.Apply(
                    p.Text,
                    p.Config.TextShadowEnabled,
                    p.Config.TextShadowX,
                    p.Config.TextShadowY,
                    p.Config.TextShadowSoftness,
                    p.Config.GetTextShadowColor()
                );
                p.LastBody = body;
                p.Dirty = false;
            }

            // Position only changes in Reorganize mode (drag); writing it back
            // every frame otherwise is a no-op round-trip against Apply()'s value.
            if(isReorganizing) SyncPosition(p);
        }

        private static StatDef FindStat(string id) =>
            id != null && CatalogById.TryGetValue(id, out StatDef stat) ? stat : null;

        // Resolver for {name} tokens in custom text: maps a built-in stat id
        // to its current value so {fps}, {accuracy}, ... work alongside addon
        // tags. Returns null when the name isn't a stat (the "text" stat is
        // excluded to avoid it referencing itself), leaving addon-tag
        // resolution / literal passthrough to the caller.
        private static string ResolveStatToken(string name, PanelConfig config) {
            if(name == "text" || !CatalogById.TryGetValue(name, out StatDef stat)) return null;
            try {
                return stat.Value(config) ?? "";
            } catch {
                return "";
            }
        }

        private static void SyncPosition(LivePanel p) {
            Vector2 stored = OverlayCalibration.Unscale(p.Rect.anchoredPosition);
            p.Config.PosX = stored.x;
            p.Config.PosY = stored.y;
        }

        // Appends `tint` as 8 uppercase hex chars (RRGGBBAA) straight into the
        // builder — same output as ColorUtility.ToHtmlStringRGBA, but without the
        // intermediate string it allocates per colored stat per frame.
        private static void AppendHex(StringBuilder sb, Color tint) {
            Color32 c = tint;
            AppendHexByte(sb, c.r);
            AppendHexByte(sb, c.g);
            AppendHexByte(sb, c.b);
            AppendHexByte(sb, c.a);
        }

        private static void AppendHexByte(StringBuilder sb, byte b) {
            const string hex = "0123456789ABCDEF";
            sb.Append(hex[b >> 4]).Append(hex[b & 0xF]);
        }

        // sb.ToString().TrimEnd() with one allocation instead of two: scan past
        // trailing whitespace, then copy once. Byte-identical to the old result.
        private static int TrimmedBodyLength(StringBuilder sb) {
            int len = sb.Length;
            while(len > 0 && char.IsWhiteSpace(sb[len - 1])) len--;
            return len;
        }

        private static bool BuilderEquals(StringBuilder sb, int length, string value) {
            if(value == null || value.Length != length) return false;
            for(int i = 0; i < length; i++)
                if(sb[i] != value[i]) return false;
            return true;
        }

        // The 0..1 value that drives a stat's color gradient — mirrors which
        // ratio v1 fed each ColorRange. Stats without a moving ratio sit at
        // the top of the gradient (static color).
        private static float ColorRatio(string id, StatColor color) {
            try {
                switch(id) {
                    case "progress": return GameStats.Progress;
                    case "accuracy": return GameStats.Accuracy;
                    case "xaccuracy": return GameStats.XAccuracy;
                    case "maxaccuracy": return GameStats.MaxXAccuracy;
                    case "musictime": return GameStats.MusicTimeRatio;
                    case "maptime": return GameStats.MapTimeRatio;
                    case "best": return GameStats.Best;

                    case "tbpm": {
                        GameStats.GetBpm(out float tbpm, out _);
                        return color.MaxBpm <= 0f ? 0f : tbpm / color.MaxBpm;
                    }

                    // v1 colored KPS and Auto KPS with the current-BPM color.
                    case "cbpm":
                    case "kps":
                    case "autokps": {
                        GameStats.GetBpm(out _, out float cbpm);
                        return color.MaxBpm <= 0f ? 0f : cbpm / color.MaxBpm;
                    }

                    default: return 1f;
                }
            } catch {
                return 1f;
            }
        }
    }
}

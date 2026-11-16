#if !IL2CPP && !QUARTZ_KEYVIEWER
using Newtonsoft.Json.Linq;
using Quartz.Core;
using Quartz.Features.KeyViewer;
using Quartz.Features.KeyViewer.Layout;
using Quartz.Resource;
using UnityEngine;
using UnityEngine.UIElements;
namespace Quartz.UI.FacetUi.KeyViewerEditor;
/// <summary>
/// Draws one key viewer element into a canvas box as the classic editor's canvas does: its idle fill, border, corner radius,
/// idle image, label and a "0" counter placed as the overlay places it. A stat shows its type word, a graph its word and a
/// knob a ring. Each Draw replaces what the box held.
/// </summary>
internal static class KvElementView {
    private const string DefaultBg = "rgba(46, 46, 47, 0.9)", DefaultBorder = "rgba(113, 113, 113, 0.9)", DefaultFont = "rgba(121, 121, 121, 0.9)";

    internal static Color Fill(KvElement e) => KeyViewerOverlay.HexToColor(Str(e, "backgroundColor", DefaultBg), 0.9f);
    internal static Color Border(KvElement e) => KeyViewerOverlay.HexToColor(Str(e, "borderColor", DefaultBorder), 0.9f);

    internal static void Draw(VisualElement box, KvElement e) {
        box.Clear();
        float radius = Mathf.Clamp(Num(e, "borderRadius", 10f), 0f, 100f), width = Mathf.Clamp(Num(e, "borderWidth", 3f), 0f, 20f);
        IStyle s = box.style;
        s.backgroundColor = Fill(e);
        s.borderTopColor = s.borderBottomColor = s.borderLeftColor = s.borderRightColor = Border(e);
        s.borderTopWidth = s.borderBottomWidth = s.borderLeftWidth = s.borderRightWidth = width;
        s.borderTopLeftRadius = s.borderTopRightRadius = s.borderBottomLeftRadius = s.borderBottomRightRadius = radius;
        DrawImage(box, e, radius);
        if(e.Kind == KvElementKind.Knob) Ring(box, e);

        JObject c = e.Raw["counter"] as JObject;
        bool stat = e.Kind == KvElementKind.Stat;
        bool counter = (e.Kind == KvElementKind.Key || stat) && (c?["enabled"] is not JValue { Type: JTokenType.Boolean } on || on.ToObject<bool>());
        string align = c?["align"]?.ToString() is { Length: > 0 } a ? a.ToLowerInvariant() : "top";
        bool between = string.Equals(c?["alignMode"]?.ToString(), "between", StringComparison.OrdinalIgnoreCase);
        bool outside = string.Equals(c?["placement"]?.ToString(), "outside", StringComparison.OrdinalIgnoreCase);
        float counterSize = c?["fontSize"] is JValue { Type: JTokenType.Integer or JTokenType.Float } cs ? cs.ToObject<float>() : 16f;
        float gap = c?["gap"] is JValue { Type: JTokenType.Integer or JTokenType.Float } g ? g.ToObject<float>() : 6f;
        bool inline = stat && counter && e.LabelEnabled && !outside && !between && align != "top" && align != "bottom";

        // Label and counter share a centred row or column, the counter on the side its align names.
        bool row = align is "left" or "right", counterFirst = align is "top" or "left";
        var content = new VisualElement { name = "kv-content", pickingMode = PickingMode.Ignore };
        content.style.position = Position.Absolute;
        content.style.left = content.style.right = content.style.top = content.style.bottom = 2f;
        content.style.flexDirection = row ? FlexDirection.Row : FlexDirection.Column;
        content.style.justifyContent = between ? Justify.SpaceBetween : Justify.Center;
        content.style.alignItems = Align.Center;
        box.Add(content);
        Color font = KeyViewerOverlay.HexToColor(Str(e, "fontColor", DefaultFont), 1f);
        Label label = e.LabelEnabled
            ? Text("kv-label", inline ? LabelOf(e) + "  0" : LabelOf(e), Num(e, "fontSize", e.Kind == KvElementKind.Key ? 18f : 16f), font)
            : null;
        Label count = counter && !inline ? Text("kv-counter", "0", counterSize, CounterColor(e, font)) : null;
        if(count != null && outside) {
            PlaceOutside(count, e, align, Mathf.Max(0f, gap), counterSize);
            box.Add(count);
            count = null;
        }
        Label first = counterFirst ? count : label, second = counterFirst ? label : count;
        if(first != null) content.Add(first);
        if(second != null) {
            if(first != null && !between) {
                if(row) second.style.marginLeft = Mathf.Max(0f, gap);
                else second.style.marginTop = Mathf.Max(0f, gap);
            }
            content.Add(second);
        }
    }

    private static Label Text(string name, string text, float size, Color color) {
        var l = new Label(text) { name = name, pickingMode = PickingMode.Ignore, enableRichText = false };
        l.style.fontSize = size;
        l.style.color = color;
        l.style.unityTextAlign = TextAnchor.MiddleCenter;
        // The key viewer's own font, as the overlay draws it; a font with no source file keeps the menu's face.
        UnityEngine.TextCore.Text.FontAsset face = ShellSettings.Face(FontManager.For(KeyViewerOverlay.FontKey));
        if(face != null) l.style.unityFontDefinition = FontDefinition.FromSDFFont(face);
        l.style.whiteSpace = WhiteSpace.NoWrap;
        l.style.marginLeft = l.style.marginRight = l.style.marginTop = l.style.marginBottom = 0f;
        l.style.paddingLeft = l.style.paddingRight = l.style.paddingTop = l.style.paddingBottom = 0f;
        return l;
    }

    // Outside the box, as the overlay sets it: above by default, else below, left or right, gap px away.
    private static void PlaceOutside(Label count, KvElement e, string align, float gap, float size) {
        float w = Mathf.Max(e.W, size * 4f), h = Mathf.Max(12f, size + 8f);
        (float x, float y) = align switch {
            "bottom" => (e.W * 0.5f - w * 0.5f, e.H + gap),
            "left" => (-gap - w, e.H * 0.5f - h * 0.5f),
            "right" => (e.W + gap, e.H * 0.5f - h * 0.5f),
            _ => (e.W * 0.5f - w * 0.5f, -gap - h),
        };
        IStyle s = count.style;
        s.position = Position.Absolute;
        s.left = x;
        s.top = y;
        s.width = w;
        s.height = h;
    }

    // The idle image (else the active one), clipped to the box's corners and fitted as the overlay fits it.
    private static void DrawImage(VisualElement box, KvElement e, float radius) {
        string idle = Str(e, "inactiveImage", "").Trim(), active = Str(e, "activeImage", "").Trim();
        string source = idle.Length > 0 ? idle : active;
        if(source.Length == 0 || KeyViewerOverlay.ResolveImage(source, KvStore.Current) is not { } texture) return;
        string own = idle.Length > 0 ? Str(e, "idleImageFit", "") : Str(e, "activeImageFit", "");
        string fit = own.Length > 0 ? own : Str(e, "imageFit", "cover");
        var image = new VisualElement { name = "kv-image", pickingMode = PickingMode.Ignore };
        IStyle s = image.style;
        s.position = Position.Absolute;
        s.left = s.right = s.top = s.bottom = 0f;
        s.backgroundImage = texture;
        s.backgroundSize = fit.ToLowerInvariant() switch {
            "fill" => new BackgroundSize(Length.Percent(100f), Length.Percent(100f)),
            "contain" or "none" => new BackgroundSize(BackgroundSizeType.Contain),
            _ => new BackgroundSize(BackgroundSizeType.Cover),
        };
        s.borderTopLeftRadius = s.borderTopRightRadius = s.borderBottomLeftRadius = s.borderBottomRightRadius = radius;
        box.Add(image);
    }

    private static void Ring(VisualElement box, KvElement e) {
        float d = Mathf.Max(4f, Mathf.Min(e.W, e.H) * 0.6f);
        var ring = new VisualElement { name = "kv-ring", pickingMode = PickingMode.Ignore };
        IStyle s = ring.style;
        s.position = Position.Absolute;
        s.left = (e.W - d) * 0.5f;
        s.top = (e.H - d) * 0.5f;
        s.width = s.height = d;
        s.borderTopLeftRadius = s.borderTopRightRadius = s.borderBottomLeftRadius = s.borderBottomRightRadius = d * 0.5f;
        s.borderTopWidth = s.borderBottomWidth = s.borderLeftWidth = s.borderRightWidth = 3f;
        s.borderTopColor = s.borderBottomColor = s.borderLeftColor = s.borderRightColor = KeyViewerOverlay.HexToColor(Str(e, "fontColor", DefaultFont), 1f);
        box.Add(ring);
    }

    private static string LabelOf(KvElement e) {
        string text = e.DisplayText;
        if(!string.IsNullOrEmpty(text)) return text;
        return e.Kind switch {
            KvElementKind.Key => KeyViewerOverlay.KeyCodeShortLabel(e.KeyCodeValue),
            KvElementKind.Graph => QuartzHost.Translate("KVI_SEC_GRAPH"),
            KvElementKind.Knob => QuartzHost.Translate("KVI_KNOB"),
            _ => QuartzHost.Translate(e.StatType switch {
                "kpsAvg" => "KVI_STAT_KPSAVG",
                "kpsMax" => "KVI_STAT_KPSMAX",
                "total" => "KVI_STAT_TOTAL",
                _ => "KVI_STAT_KPS",
            }),
        };
    }

    private static Color CounterColor(KvElement e, Color font) {
        string idle = e.Raw["counter"]?["fill"]?["idle"]?.ToString();
        return string.IsNullOrEmpty(idle) ? font : KeyViewerOverlay.HexToColor(idle, 1f);
    }

    private static float Num(KvElement e, string key, float fallback) {
        JToken t = e.Raw[key];
        if(t == null || t.Type == JTokenType.Null) return fallback;
        try {
            return t.ToObject<float>();
        } catch(Exception ex) {
            Diag.Ignore(ex);
            return fallback;
        }
    }

    private static string Str(KvElement e, string key, string fallback) {
        JToken t = e.Raw[key];
        return t == null || t.Type == JTokenType.Null ? fallback : t.ToString();
    }
}
#endif

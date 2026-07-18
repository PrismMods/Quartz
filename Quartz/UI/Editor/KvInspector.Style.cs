using Newtonsoft.Json.Linq;
using Quartz.Features.KeyViewer;
using Quartz.Features.KeyViewer.Layout;
using Quartz.UI.Generator;
using Quartz.UI.Objects;
using UnityEngine;
namespace Quartz.UI.Editor;
internal sealed partial class KvInspector {
    // The defaults and fallback alphas ParseDmNoteSpec applies to a field it does not find.
    // They are not uniform — a missing backgroundColor resolves at 0.9 alpha, a missing
    // fontColor at 1 — so each picker has to carry its own or middle-click-to-default would
    // hand back a colour the renderer never used.
    private const string DefBg = "rgba(46, 46, 47, 0.9)";
    private const string DefBgActive = "rgba(121, 121, 121, 0.9)";
    private const string DefBorder = "rgba(113, 113, 113, 0.9)";
    private const string DefBorderActive = "rgba(255, 255, 255, 0.9)";
    private const string DefFont = "rgba(121, 121, 121, 0.9)";
    private const string DefFontActive = "#FFFFFF";
    // ParseGraphSpec is a separate reader with its own defaults, and it reads none of the
    // pressed/text fields — a graph has no pressed state and draws no label.
    private const string DefGraphBg = "rgba(17, 17, 20, 0.9)";
    private const string DefGraphBorder = "rgba(255, 255, 255, 0.1)";
    private void BuildStyleTab(RectTransform root, List<UIObject> tracked, KvElement[] batch) {
        if(AllGraphs(batch)) {
            BuildGraphStyle(root, tracked, batch);
            return;
        }
        // backgroundColor, borderColor, borderRadius and borderWidth are the four fields both
        // readers share, so those rows write to the whole batch. Everything else below is
        // ParseDmNoteSpec's alone, and a graph caught in a mixed selection is skipped rather than
        // handed keys ParseGraphSpec never looks at.
        KvElement[] boxes = NonGraphs(batch);
        Header(root, "KVI_SEC_COLORS", "Colors");
        Box(root, tracked, batch, "Background", "kvi_bg", "backgroundColor", DefBg, 0.9f);
        Box(root, tracked, boxes, "Background (Pressed)", "kvi_bg_active", "activeBackgroundColor", DefBgActive, 0.9f);
        Box(root, tracked, batch, "Border", "kvi_border", "borderColor", DefBorder, 0.9f);
        Box(root, tracked, boxes, "Border (Pressed)", "kvi_border_active", "activeBorderColor", DefBorderActive, 0.9f);
        Box(root, tracked, boxes, "Text", "kvi_font_color", "fontColor", DefFont, 1f);
        Box(root, tracked, boxes, "Text (Pressed)", "kvi_font_color_active", "activeFontColor", DefFontActive, 1f);
        Flag(
            root, tracked, "Transparent When Idle", "kvi_idle_transparent", false,
            boxes, el => KvProps.Bool(el.Raw, "idleTransparent", false),
            (el, v) => el.Raw["idleTransparent"] = v
        ).Rect.AddToolTip(
            "DESC_KVI_IDLE_TRANSPARENT",
            "Hide the background while the key is up, keeping its border and label. The background colour is remembered."
        );
        Flag(
            root, tracked, "Transparent When Pressed", "kvi_active_transparent", false,
            boxes, el => KvProps.Bool(el.Raw, "activeTransparent", false),
            (el, v) => el.Raw["activeTransparent"] = v
        ).Rect.AddToolTip(
            "DESC_KVI_ACTIVE_TRANSPARENT",
            "Hide the background while the key is held, keeping its border and label."
        );
        Header(root, "KVI_SEC_SHAPE", "Shape");
        // Every range here is ParseDmNoteSpec's own clamp. A value outside it is silently
        // pulled back at render time, so offering more would only mislead.
        Num(root, tracked, "Corner Radius", "kvi_radius", 10f, 0f, 100f, "0 px", 1f,
            batch, el => KvProps.Float(el.Raw, "borderRadius", 10f),
            (el, v) => el.Raw["borderRadius"] = v);
        Num(root, tracked, "Border Width", "kvi_border_width", 3f, 0f, 20f, "0 px", 1f,
            batch, el => KvProps.Float(el.Raw, "borderWidth", 3f),
            (el, v) => el.Raw["borderWidth"] = v);
        // The reader's fontSize default is per-kind (18 for a key, 16 for anything else), so a
        // batch has to read and reset against the first element's or the row would lie about both.
        float defFont = boxes[0].Kind == KvElementKind.Key ? 18f : 16f;
        Num(root, tracked, "Font Size", "kvi_font_size", defFont, 1f, 200f, "0 px", 1f,
            boxes, el => KvProps.Int(el.Raw, "fontSize", (int)defFont),
            (el, v) => KvProps.SetInt(el.Raw, "fontSize", v));
        // JipperKeyViewer's press animation: the box scales to this while held. 100% is off
        // (the extension field is dropped at the default, like every quartz* key). Rendered
        // through the CSS state transform, so explicit custom CSS scale overrides it.
        Num(root, tracked, "Press Scale", "kvi_press_scale", 100f, 25f, 200f, "0' %'", 1f,
            boxes, el => KvProps.Float(el.Raw, "quartzPressScale", 1f) * 100f,
            (el, v) => {
                if(Mathf.Abs(v - 100f) < 0.5f) el.Raw.Remove("quartzPressScale");
                else el.Raw["quartzPressScale"] = v / 100f;
            });
        Header(root, "KVI_SEC_FONT_STYLE", "Font Style");
        FontStyleRows(root, tracked, boxes, "kvi_font", el => el.Raw, el => el.Raw);
    }
    /// <summary>
    /// DM Note's fontWeight/fontItalic/fontUnderline/fontStrikethrough as four toggles, shared
    /// between the label (fields on the element) and the counter (fields on its counter object).
    /// Bold is fontWeight 700-or-absent — DM Note itself only ever writes the two.
    /// </summary>
    private void FontStyleRows(RectTransform root, List<UIObject> tracked, KvElement[] batch,
        string idPrefix, Func<KvElement, JObject> read, Func<KvElement, JObject> write) {
        Flag(root, tracked, "Bold", idPrefix + "_bold", false,
            batch, el => KvProps.Int(read(el), "fontWeight", 400) >= 600,
            (el, v) => {
                if(v) write(el)["fontWeight"] = 700;
                else write(el).Remove("fontWeight");
            });
        FontStyleFlag(root, tracked, batch, "Italic", idPrefix + "_italic", "fontItalic", read, write);
        FontStyleFlag(root, tracked, batch, "Underline", idPrefix + "_underline", "fontUnderline", read, write);
        FontStyleFlag(root, tracked, batch, "Strikethrough", idPrefix + "_strikethrough", "fontStrikethrough", read, write);
    }
    private void FontStyleFlag(RectTransform root, List<UIObject> tracked, KvElement[] batch,
        string label, string id, string field, Func<KvElement, JObject> read, Func<KvElement, JObject> write)
        => Flag(root, tracked, label, id, false,
            batch, el => KvProps.Bool(read(el), field, false),
            (el, v) => {
                if(v) write(el)[field] = true;
                else write(el).Remove(field);
            });
    /// <summary>
    /// Only the four fields ParseGraphSpec actually reads. The key reader's other rows —
    /// pressed colours, text colours, font size, the transparency flags — would render as live
    /// controls that write keys nothing ever looks at.
    /// </summary>
    private void BuildGraphStyle(RectTransform root, List<UIObject> tracked, KvElement[] batch) {
        Header(root, "KVI_SEC_COLORS", "Colors");
        Box(root, tracked, batch, "Background", "kvi_bg", "backgroundColor", DefGraphBg, 0.9f);
        Box(root, tracked, batch, "Border", "kvi_border", "borderColor", DefGraphBorder, 0.1f);
        Header(root, "KVI_SEC_SHAPE", "Shape");
        Num(root, tracked, "Corner Radius", "kvi_radius", 8f, 0f, 100f, "0 px", 1f,
            batch, el => KvProps.Float(el.Raw, "borderRadius", 8f),
            (el, v) => el.Raw["borderRadius"] = v);
        Num(root, tracked, "Border Width", "kvi_border_width", 3f, 0f, 20f, "0 px", 1f,
            batch, el => KvProps.Float(el.Raw, "borderWidth", 3f),
            (el, v) => el.Raw["borderWidth"] = v);
    }
    private void Box(
        RectTransform root, List<UIObject> tracked, KvElement[] batch,
        string label, string id, string field, string def, float defAlpha
    ) => Colour(
        root, tracked, label, id,
        KeyViewerOverlay.HexToColor(def, defAlpha),
        batch,
        el => KvProps.Color(el.Raw, field, def, defAlpha),
        (el, c) => KvProps.SetColor(el.Raw, field, c), true
    );
}

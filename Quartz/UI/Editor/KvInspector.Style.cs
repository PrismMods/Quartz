using Newtonsoft.Json.Linq;
using Quartz.Features.KeyViewer;
using Quartz.Features.KeyViewer.Layout;
using Quartz.UI.Generator;
using Quartz.UI.Objects;
using UnityEngine;
namespace Quartz.UI.Editor;
internal sealed partial class KvInspector {
    private const string DefBg = "rgba(46, 46, 47, 0.9)";
    private const string DefBgActive = "rgba(121, 121, 121, 0.9)";
    private const string DefBorder = "rgba(113, 113, 113, 0.9)";
    private const string DefBorderActive = "rgba(255, 255, 255, 0.9)";
    private const string DefFont = "rgba(121, 121, 121, 0.9)";
    private const string DefFontActive = "#FFFFFF";
    private const string DefGraphBg = "rgba(17, 17, 20, 0.9)";
    private const string DefGraphBorder = "rgba(255, 255, 255, 0.1)";
    private void BuildStyleTab(RectTransform root, List<UIObject> tracked, KvElement[] batch) {
        if(AllGraphs(batch)) {
            BuildGraphStyle(root, tracked, batch);
            return;
        }
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
        Num(root, tracked, "Corner Radius", "kvi_radius", 10f, 0f, 100f, "0 px", 1f,
            batch, el => KvProps.Float(el.Raw, "borderRadius", 10f),
            (el, v) => el.Raw["borderRadius"] = v);
        Num(root, tracked, "Border Width", "kvi_border_width", 3f, 0f, 20f, "0 px", 1f,
            batch, el => KvProps.Float(el.Raw, "borderWidth", 3f),
            (el, v) => el.Raw["borderWidth"] = v);
        float defFont = boxes[0].Kind == KvElementKind.Key ? 18f : 16f;
        Num(root, tracked, "Font Size", "kvi_font_size", defFont, 1f, 200f, "0 px", 1f,
            boxes, el => KvProps.Int(el.Raw, "fontSize", (int)defFont),
            (el, v) => KvProps.SetInt(el.Raw, "fontSize", v));
        Num(root, tracked, "Press Scale", "kvi_press_scale", 100f, 25f, 200f, "0' %'", 1f,
            boxes, el => KvProps.Float(el.Raw, "quartzPressScale", 1f) * 100f,
            (el, v) => {
                if(Mathf.Abs(v - 100f) < 0.5f) el.Raw.Remove("quartzPressScale");
                else el.Raw["quartzPressScale"] = v / 100f;
            });
        Header(root, "KVI_SEC_FONT_STYLE", "Font Style");
        FontStyleRows(root, tracked, boxes, "kvi_font", el => el.Raw, el => el.Raw);
    }
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

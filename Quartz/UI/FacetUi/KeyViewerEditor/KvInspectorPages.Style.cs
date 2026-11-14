#if !IL2CPP && !QUARTZ_KEYVIEWER
using Newtonsoft.Json.Linq;
using Quartz.Features.KeyViewer;
using Quartz.Features.KeyViewer.Editor;
using Quartz.Features.KeyViewer.Layout;
using UnityEngine;
using UnityEngine.UIElements;
namespace Quartz.UI.FacetUi.KeyViewerEditor;
// The Style tab: colours, shape, font style, image and custom CSS, as the classic Style tab; graphs get their own short form.
internal static partial class KvInspectorPages {
    private const string DefBg = "rgba(46, 46, 47, 0.9)";
    private const string DefBgActive = "rgba(121, 121, 121, 0.9)";
    private const string DefBorder = "rgba(113, 113, 113, 0.9)";
    private const string DefBorderActive = "rgba(255, 255, 255, 0.9)";
    private const string DefFont = "rgba(121, 121, 121, 0.9)";
    private const string DefFontActive = "#FFFFFF";
    private const string DefGraphBg = "rgba(17, 17, 20, 0.9)";
    private const string DefGraphBorder = "rgba(255, 255, 255, 0.1)";
    private const int ImagePathLimit = 1024, ClassNameLimit = 96;
    private static readonly string[] ImageFits = ["cover", "contain", "fill", "none"];

    private static void BuildStyle(VisualElement p, KvElement[] batch) {
        if(AllOf(batch, KvElementKind.Graph)) {
            GraphStyle(p);
            return;
        }
        KvElement[] boxes = [.. batch.Where(e => e.Kind != KvElementKind.Graph)];
        VisualElement colors = Section(p, "KVI_SEC_COLORS");
        Box(colors, "KVI_BG", Sel, "backgroundColor", DefBg, 0.9f);
        Box(colors, "KVI_BG_ACTIVE", NonGraphs, "activeBackgroundColor", DefBgActive, 0.9f);
        Box(colors, "KVI_BORDER", Sel, "borderColor", DefBorder, 0.9f);
        Box(colors, "KVI_BORDER_ACTIVE", NonGraphs, "activeBorderColor", DefBorderActive, 0.9f);
        Box(colors, "KVI_FONT_COLOR", NonGraphs, "fontColor", DefFont, 1f);
        Box(colors, "KVI_FONT_COLOR_ACTIVE", NonGraphs, "activeFontColor", DefFontActive, 1f);
        Flag(colors, "KVI_IDLE_TRANSPARENT", false, NonGraphs, e => KvProps.Bool(e.Raw, "idleTransparent", false),
            (e, v) => e.Raw["idleTransparent"] = v, "DESC_KVI_IDLE_TRANSPARENT");
        Flag(colors, "KVI_ACTIVE_TRANSPARENT", false, NonGraphs, e => KvProps.Bool(e.Raw, "activeTransparent", false),
            (e, v) => e.Raw["activeTransparent"] = v, "DESC_KVI_ACTIVE_TRANSPARENT");

        VisualElement shape = Section(p, "KVI_SEC_SHAPE");
        Num(shape, "KVI_RADIUS", 10f, 0f, 100f, "0 px", 1f, Sel, e => KvProps.Float(e.Raw, "borderRadius", 10f), (e, v) => e.Raw["borderRadius"] = v);
        Num(shape, "KVI_BORDER_WIDTH", 3f, 0f, 20f, "0 px", 1f, Sel, e => KvProps.Float(e.Raw, "borderWidth", 3f), (e, v) => e.Raw["borderWidth"] = v);
        int defFont = boxes[0].Kind == KvElementKind.Key ? 18 : 16;
        Num(shape, "KVI_FONT_SIZE", defFont, 1f, 200f, "0 px", 1f, NonGraphs, e => KvProps.Int(e.Raw, "fontSize", defFont),
            (e, v) => KvProps.SetInt(e.Raw, "fontSize", v));
        Num(shape, "KVI_PRESS_SCALE", 100f, 25f, 200f, "0' %'", 1f, NonGraphs, e => KvProps.Float(e.Raw, "quartzPressScale", 1f) * 100f,
            (e, v) => {
                if(Mathf.Abs(v - 100f) < 0.5f) e.Raw.Remove("quartzPressScale");
                else e.Raw["quartzPressScale"] = v / 100f;
            });

        FontStyleRows(Section(p, "KVI_SEC_FONT_STYLE"), NonGraphs, "KVI_FONT", e => e.Raw, e => e.Raw);
        ImageRows(p, true);
        CssRows(p);
    }

    private static void GraphStyle(VisualElement p) {
        VisualElement colors = Section(p, "KVI_SEC_COLORS");
        Box(colors, "KVI_BG", Sel, "backgroundColor", DefGraphBg, 0.9f);
        Box(colors, "KVI_BORDER", Sel, "borderColor", DefGraphBorder, 0.1f);
        VisualElement shape = Section(p, "KVI_SEC_SHAPE");
        Num(shape, "KVI_RADIUS", 8f, 0f, 100f, "0 px", 1f, Sel, e => KvProps.Float(e.Raw, "borderRadius", 8f), (e, v) => e.Raw["borderRadius"] = v);
        Num(shape, "KVI_BORDER_WIDTH", 3f, 0f, 20f, "0 px", 1f, Sel, e => KvProps.Float(e.Raw, "borderWidth", 3f), (e, v) => e.Raw["borderWidth"] = v);
        ImageRows(p, false);
        CssRows(p);
    }

    private static void Box(VisualElement p, string id, Func<IReadOnlyList<KvElement>> items, string field, string def, float defAlpha) =>
        Colour(p, id, KeyViewerOverlay.HexToColor(def, defAlpha), true, items,
            e => KvProps.Color(e.Raw, field, def, defAlpha), (e, c) => KvProps.SetColor(e.Raw, field, c));

    private static void ImageRows(VisualElement p, bool includeActive) {
        VisualElement s = Section(p, "KVI_SEC_IMAGE");
        ImageField(s, "KVI_IMAGE_IDLE", "inactiveImage", "DESC_KVI_IMAGE_IDLE");
        if(includeActive) ImageField(s, "KVI_IMAGE_ACTIVE", "activeImage", "DESC_KVI_IMAGE_ACTIVE");
        Pick(s, "kv-image-fit", ImageFits, ImageFitKey, "cover", Sel, e => KvProps.Str(e.Raw, "imageFit", "cover"), (e, v) => {
            if(v == "cover") e.Raw.Remove("imageFit");
            else e.Raw["imageFit"] = v;
        });
    }

    private static void ImageField(VisualElement p, string id, string field, string tip) =>
        Text(p, id, Sel, e => KvProps.Str(e.Raw, field, ""), (e, v) => {
            string s = v.Trim();
            if(s.Length == 0) e.Raw.Remove(field);
            else e.Raw[field] = s;
        }, ImagePathLimit, tip);

    private static string ImageFitKey(string s) => s switch {
        "contain" => "KVI_IMGFIT_CONTAIN",
        "fill" => "KVI_IMGFIT_FILL",
        "none" => "KVI_IMGFIT_NONE",
        _ => "KVI_IMGFIT_COVER",
    };

    private static void CssRows(VisualElement p) {
        VisualElement s = Section(p, "KVI_SEC_CSS");
        Flag(s, "KVI_INLINE_PRIORITY", false, Sel, e => e.UseInlineStyles, (e, v) => e.UseInlineStyles = v, "DESC_KVI_INLINE_PRIORITY");
        Text(s, "KVI_CLASS", Sel, e => e.ClassName, (e, v) => e.ClassName = v, ClassNameLimit, "DESC_KVI_CLASS");
    }

    // Bold, italic, underline and strikethrough on the element (Style) or on its counter (Counter).
    private static void FontStyleRows(VisualElement p, Func<IReadOnlyList<KvElement>> items, string prefix,
        Func<KvElement, JObject> read, Func<KvElement, JObject> write) {
        Flag(p, prefix + "_BOLD", false, items, e => KvProps.Int(read(e), "fontWeight", 400) >= 600, (e, v) => {
            if(v) write(e)["fontWeight"] = 700;
            else write(e).Remove("fontWeight");
        });
        FontStyleFlag(p, items, prefix + "_ITALIC", "fontItalic", read, write);
        FontStyleFlag(p, items, prefix + "_UNDERLINE", "fontUnderline", read, write);
        FontStyleFlag(p, items, prefix + "_STRIKETHROUGH", "fontStrikethrough", read, write);
    }

    private static void FontStyleFlag(VisualElement p, Func<IReadOnlyList<KvElement>> items, string id, string field,
        Func<KvElement, JObject> read, Func<KvElement, JObject> write) =>
        Flag(p, id, false, items, e => KvProps.Bool(read(e), field, false), (e, v) => {
            if(v) write(e)[field] = true;
            else write(e).Remove(field);
        });
}
#endif

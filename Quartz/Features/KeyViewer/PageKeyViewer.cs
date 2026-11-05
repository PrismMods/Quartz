using Quartz.Overlay;
using Quartz.UI.Generator;
using Quartz.UI.Utility;
using UnityEngine;
namespace Quartz.Features.KeyViewer;
internal static partial class PageKeyViewer {
    public static void AppendTo(Transform content, UIScrollController pageScroll = null) {
        KeyViewerOverlay.EnsureConf();
        KeyViewerSettings conf = KeyViewerOverlay.Conf;
        // No "Key Viewer" heading: the page tab already says it, and every row here comes out of the editor's height.
        RectTransform body = (RectTransform)content;
        OverlayFontPicker.Dropdown(body, KeyViewerOverlay.FontKey, "keyviewer_font");
        RectTransform editorBody = GenerateUI.MakeBody(body, "EditorMode");
        Action onEditorShown = AppendEditor(editorBody, conf, pageScroll);
        onEditorShown();
    }
}

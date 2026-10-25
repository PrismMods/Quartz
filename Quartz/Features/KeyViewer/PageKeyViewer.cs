using Quartz.Overlay;
using Quartz.UI.Generator;
using Quartz.UI.Utility;
using UnityEngine;
namespace Quartz.Features.KeyViewer;
internal static partial class PageKeyViewer {
    public static void AppendTo(Transform content, UIScrollController pageScroll = null) {
        KeyViewerOverlay.EnsureConf();
        KeyViewerSettings conf = KeyViewerOverlay.Conf;
        GenerateUI.CollapsibleSection sec = GenerateUI.FlatSection(content, "Key Viewer");
        OverlayFontPicker.Dropdown(sec.Body, KeyViewerOverlay.FontKey, "keyviewer_font");
        RectTransform editorBody = GenerateUI.MakeBody(sec.Body, "EditorMode");
        Action onEditorShown = AppendEditor(editorBody, conf, pageScroll);
        onEditorShown();
    }
}

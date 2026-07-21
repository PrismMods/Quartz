using Quartz.Features.KeyViewer;
using Quartz.UI.Generator;
using Quartz.UI.Utility;
using UnityEngine;
namespace Quartz.UI.Factory.Page;
internal static partial class PageKeyViewer {
    public static void AppendTo(Transform content, UIScrollController pageScroll = null) {
        KeyViewerOverlay.EnsureConf();
        KeyViewerSettings conf = KeyViewerOverlay.Conf;
        GenerateUI.CollapsibleSection sec = GenerateUI.FlatSection(
            content, "Key Viewer",
            v => { conf.Enabled = v; KeyViewerOverlay.Save(); },
            conf.Enabled,
            "Enable Key Viewer", "keyviewer_enable"
        );
        RectTransform editorBody = GenerateUI.MakeBody(sec.Body, "EditorMode");
        Action onEditorShown = AppendEditor(editorBody, conf, pageScroll);
        onEditorShown();
    }
}

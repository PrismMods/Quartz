using Quartz.Features.KeyViewer;
using Quartz.UI.Generator;
using Quartz.UI.Utility;
using UnityEngine;
namespace Quartz.UI.Factory.Page;
internal static partial class PageKeyViewer {
    /// <summary>
    /// The Key Viewer page: the enable toggle, and the layout editor.
    ///
    /// Nothing else is here. The editor is sized to the page viewport, so a row left beside or
    /// under it would only be reachable by scrolling the editor itself off screen — every other
    /// setting lives on the Settings tab of its inspector instead.
    /// </summary>
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

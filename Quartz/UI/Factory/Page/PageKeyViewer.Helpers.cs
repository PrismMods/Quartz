using Quartz.Core;
using Quartz.Features.KeyViewer;
using Quartz.Resource;
using Quartz.UI.Generator;
using Quartz.UI.Objects;
using Quartz.UI.Objects.Impl;
using Quartz.UI.Panes;
using UnityEngine;
using UnityEngine.UI;
using static UnityEngine.EventSystems.PointerEventData;
using TMPro;
namespace Quartz.UI.Factory.Page;
internal static partial class PageKeyViewer {
    /// <summary>
    /// The scroll controller is handed on rather than dropped: Editor mode sizes itself to the
    /// viewport it is scrolling inside, which is the one thing a page body cannot measure for
    /// itself.
    /// </summary>
    public static void Create(RectTransform parent) {
        RectTransform content = Quartz.UI.Factory.PageFactory.CreateScrollablePage(
            parent, out Quartz.UI.Utility.UIScrollController scroll
        );
        AppendTo(content, scroll);
    }
    private static string FootStyleName(int s) => s <= 0
        ? MainCore.Tr.Get("KEYVIEWER_FOOT_NONE", "None")
        : string.Format(MainCore.Tr.Get("KEYVIEWER_FOOT_COUNT", "{0} Keys"), s * 2);
    private static string StyleName(int style) => style switch {
        0 => MainCore.Tr.Get("KEYVIEWER_STYLE_10", "10 Keys"),
        1 => MainCore.Tr.Get("KEYVIEWER_STYLE_12", "12 Keys"),
        3 => MainCore.Tr.Get("KEYVIEWER_STYLE_20", "20 Keys"),
        4 => MainCore.Tr.Get("KEYVIEWER_STYLE_8", "8 Keys"),
        5 => MainCore.Tr.Get("KEYVIEWER_STYLE_14", "14 Keys"),
        Features.KeyViewer.Layout.KvPresets.Style24 => MainCore.Tr.Get("KEYVIEWER_STYLE_24", "24 Keys"),
        _ => MainCore.Tr.Get("KEYVIEWER_STYLE_16", "16 Keys"),
    };
    private static UISlider AddSlider(
        Transform body, string label, string id,
        float defVal, float min, float max, float val,
        string format, float step,
        Action<float> setter, Action save
    ) => GenerateUI.SnapSlider(body, label, id, defVal, min, max, val, format, step, setter, null, save);
    private static void AddColor(
        Transform body, string label, string id,
        Color defColor, Color current, Action<Color> setter,
        Action apply, Action save, Action refreshPreview
    ) => GenerateUI.ColorPicker(
            GenerateUI.Row(body),
            defColor,
            current,
            c => { setter(c); apply(); refreshPreview(); },
            c => { setter(c); apply(); refreshPreview(); save(); },
            label,
            id
        );
    private sealed class LiveKeyPreviewHandle : UIObject {
        private readonly Action<KeyViewerOverlay.KeyPressChangedEventArgs> handler;
        public LiveKeyPreviewHandle(RectTransform rect, Action<KeyViewerOverlay.KeyPressChangedEventArgs> handler)
            : base("livekeypreview", rect) {
            this.handler = handler;
            KeyViewerOverlay.OnKeyPressChanged += handler;
        }
        public override void Dispose() {
            base.Dispose();
            KeyViewerOverlay.OnKeyPressChanged -= handler;
        }
    }
}

using Quartz.Core;
using Quartz.Features.KeyViewer;
using Quartz.Features.KeyViewer.Layout;
using Quartz.Resource;
using Quartz.UI.Editor;
using Quartz.UI.Generator;
using Quartz.UI.Objects.Impl;
using Quartz.UI.Utility;
using TMPro;
using UnityEngine;
namespace Quartz.UI.Factory.Page;
internal static partial class PageKeyViewer {
    private static Action AppendEditor(RectTransform body, KeyViewerSettings conf, UIScrollController pageScroll) {
        RectTransform editor = EditorRegion(body, pageScroll);
        EditorSplit split = EditorSplitRow(editor);
        KvCanvas canvas = KvCanvas.Create(split.CanvasHost);
        KvInspector inspector = KvInspector.Attach(canvas);
        canvas.Bind(KvStore.Current, KvStore.Current.SelectedTab);
        GenerateUI.AddLocalizedMutedText(
            GenerateUI.Row(editor, ThinRowHeight), "KVI_TOOLBAR_HINT",
            "Scroll to pan, or drag with the middle or right mouse button. Press + and - to zoom, 0 for actual size. Arrow keys nudge, Delete removes.",
            15f, 0.45f
        );
        TextMeshProUGUI status = GenerateUI.AddMutedText(GenerateUI.Row(editor, ThinRowHeight), 17f, 0.45f);
        RectTransform toolbar = KvToolbar.Bar(editor);
        Action refreshTabs = AppendTabStrip(toolbar, canvas, status, RefreshStatus);
        inspector.BuildToolbar(toolbar);
        EditorPanel panel = InspectorPanel(split.PaneHost);
        Action refreshSettings = AppendEditorSettings(panel.Settings, conf, refreshTabs);
        void RefreshStatus() {
            KvDocument doc = canvas.Document;
            status.text = string.Format(
                MainCore.Tr.Get("KEYVIEWER_EDITOR_STATUS", "{0} elements on tab \"{1}\""),
                doc == null ? 0 : doc.AllElements(canvas.Tab).Count,
                canvas.Tab
            );
        }
        void Rebind() {
            KvDocument doc = KvStore.Current;
            canvas.Bind(doc, doc.SelectedTab);
            refreshTabs();
            KeyViewerOverlay.RequestLayoutRebuild();
        }
        void RebindIfStale() {
            if(!ReferenceEquals(canvas.Document, KvStore.Current)) Rebind();
        }
        canvas.Changed += () => {
            KvStore.RequestSave();
            KeyViewerOverlay.RequestLayoutRebuild();
            RefreshStatus();
        };
        canvas.DocumentReplaced += replaced => {
            KvStore.Replace(replaced);
            KeyViewerOverlay.RequestLayoutRebuild();
            RefreshStatus();
        };
        AppendFileStrip(toolbar, status, Rebind, RefreshStatus);
        inspector.BindSettings(panel.Settings, refreshSettings);
        inspector.BindHost(panel.Tabs, panel.Props, panel.Scroll);
        RectTransform panelViewport = panel.Scroll.viewport;
        RectTransform divider = split.Divider;
        body.gameObject.AddComponent<KvCanvasTeardown>().OnDestroyed = () => {
            inspector.Dispose();
            canvas.Dispose();
            UIScrollController.RemoveInputCapture(panelViewport);
            UIScrollController.RemoveInputCapture(divider);
        };
        RefreshStatus();
        return () => {
            RebindIfStale();
            RefreshStatus();
            refreshSettings();
        };
    }
    private static void AppendFileStrip(
        RectTransform toolbar,
        TextMeshProUGUI status, Action rebind, Action refreshStatus
    ) {
        KvToolbar.Spacer(toolbar);
        RectTransform pill = KvToolbar.Pill(toolbar);
        RectTransform host = KvToolbar.RegionOf(toolbar);
        UIButton file = KvToolbar.Icon(
            pill, UISprite.Folder128, "keyviewer_editor_file", null,
            "DESC_KEYVIEWER_EDITOR_FILE",
            "Import a DM Note preset from the presets folder, or write this layout out to it."
        );
        void Export(bool includeCounts) {
            if(KvStore.Export(out string error, includeCounts)) {
                status.text = MainCore.Tr.Get("KEYVIEWER_EDITOR_EXPORTED", "Exported.");
            } else if(!string.IsNullOrEmpty(error)) {
                status.text = error;
            }
        }
        void Import() {
            if(KvStore.Import(out string error)) {
                rebind();
                refreshStatus();
            } else if(!string.IsNullOrEmpty(error)) {
                status.text = error;
            }
        }
        file.OnClick = () => KvPopup.Show(
            host, file.Rect,
            [
                ("KEYVIEWER_EDITOR_IMPORT", "Import"),
                ("KEYVIEWER_EDITOR_EXPORT_COUNTS", "Export"),
                ("KEYVIEWER_EDITOR_EXPORT_ZEROED", "Export (Zeroed)"),
                ("KEYVIEWER_EDITOR_OPEN_FOLDER", "Open Presets Folder"),
            ],
            index => {
                switch(index) {
                    case 0:
                        Import();
                        break;
                    case 1:
                        Export(true);
                        break;
                    case 2:
                        Export(false);
                        break;
                    default:
                        KvStore.RevealLibrary();
                        break;
                }
            }
        );
    }
}

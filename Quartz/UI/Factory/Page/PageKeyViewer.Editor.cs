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
    /// <summary>
    /// Builds the free-form layout editor into <paramref name="body"/>. Returns the callback to
    /// run each time the body is shown, which re-reads state the rest of the page can change
    /// behind the editor's back.
    ///
    /// The body holds the editor and nothing else. Every control the editor needs — the file
    /// actions, the renderer's own settings — is inside its chrome, because the region is sized to
    /// the page viewport and anything left below it would be unreachable without scrolling the
    /// editor itself off screen.
    /// </summary>
    private static Action AppendEditor(RectTransform body, KeyViewerSettings conf, UIScrollController pageScroll) {
        RectTransform editor = EditorRegion(body, pageScroll);
        EditorSplit split = EditorSplitRow(editor);
        // The canvas can move an element; the inspector is what makes it look like anything.
        // It owns no state the canvas has.
        KvCanvas canvas = KvCanvas.Create(split.CanvasHost);
        KvInspector inspector = KvInspector.Attach(canvas);
        canvas.Bind(KvStore.Current, KvStore.Current.SelectedTab);
        GenerateUI.AddLocalizedMutedText(
            GenerateUI.Row(editor, ThinRowHeight), "KVI_TOOLBAR_HINT",
            "Scroll to pan, or drag with the middle or right mouse button. Press + and - to zoom, 0 for actual size. Arrow keys nudge, Delete removes.",
            15f, 0.45f
        );
        TextMeshProUGUI status = GenerateUI.AddMutedText(GenerateUI.Row(editor, ThinRowHeight), 17f, 0.45f);
        // Built last and so laid out last: DM Note's bar is the floor of its window, under the
        // canvas rather than over it, which is what lets the canvas have everything above.
        RectTransform toolbar = KvToolbar.Bar(editor);
        // First on the bar, which is where DM Note's ToolBar puts TabTool — its justify-between
        // holds the tabs against the left edge and the actions against the right.
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
        // Import installs a new document instance, so the canvas has to be pointed at the
        // replacement or it keeps editing the one that was just discarded.
        void Rebind() {
            KvDocument doc = KvStore.Current;
            canvas.Bind(doc, doc.SelectedTab);
            // The replacement brings its own tabs, so the strip is describing a document that no
            // longer exists.
            refreshTabs();
            KeyViewerOverlay.RequestLayoutRebuild();
        }
        // Changed saves KvStore.Current, not the canvas's own document, so the two drifting
        // apart would quietly write the wrong layout. Bind is not unconditional: it drops
        // undo history and zoom, which switching tabs away and back must not do.
        void RebindIfStale() {
            if(!ReferenceEquals(canvas.Document, KvStore.Current)) Rebind();
        }
        canvas.Changed += () => {
            KvStore.RequestSave();
            KeyViewerOverlay.RequestLayoutRebuild();
            RefreshStatus();
        };
        // Undo/redo hand back a reparsed document the canvas has already adopted. Re-binding
        // here would clear the history that drove it, so only the store is updated.
        canvas.DocumentReplaced += replaced => {
            KvStore.Replace(replaced);
            KeyViewerOverlay.RequestLayoutRebuild();
            RefreshStatus();
        };
        AppendFileStrip(toolbar, status, Rebind, RefreshStatus);
        inspector.BindSettings(panel.Settings, refreshSettings);
        inspector.BindHost(panel.Tabs, panel.Props, panel.Scroll);
        // Held rather than read back off the controller at teardown: OnDestroy runs in no
        // guaranteed order, so the component this came from may already be gone by then.
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
    /// <summary>
    /// The file actions, as the pill on the far right of the bar: they act on the document the
    /// canvas is showing, so they belong with the rest of the actions that do. One icon rather than
    /// four buttons, which is how DM Note keeps a bar this dense to a single row.
    /// </summary>
    private static void AppendFileStrip(
        RectTransform toolbar,
        TextMeshProUGUI status, Action rebind, Action refreshStatus
    ) {
        // Everything before this sits against the left edge, everything after against the right —
        // DM Note's justify-between.
        KvToolbar.Spacer(toolbar);
        RectTransform pill = KvToolbar.Pill(toolbar);
        RectTransform host = KvToolbar.RegionOf(toolbar);
        // Built first because the import list and the action popup both hang off its rect.
        UIButton file = KvToolbar.Icon(
            pill, UISprite.Folder128, "keyviewer_editor_file", null,
            "DESC_KEYVIEWER_EDITOR_FILE",
            "Import a DM Note preset from the presets folder, or write this layout out to it."
        );
        void Export(bool includeCounts) {
            // Native SaveFile dialog, like TUF's folder picker. Its default location is the presets
            // folder, so an accepted save lands somewhere the import list also sees.
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
                // Two entries rather than a prompt: there is no confirm helper in this UI, and
                // the choice has to be made on every export, so it is the entry that asks.
                ("KEYVIEWER_EDITOR_EXPORT_COUNTS", "Export"),
                ("KEYVIEWER_EDITOR_EXPORT_ZEROED", "Export (Zeroed)"),
                // The dialogs open on their own Space in exclusive fullscreen (macOS behaviour for
                // every native picker); Reveal opens Finder without that, as a reliable fallback.
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

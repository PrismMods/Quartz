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
        void Export(KvExportFormat format) {
            if(KvStore.Export(format, out string error)) {
                status.text = format == KvExportFormat.Quartz
                    ? MainCore.Tr.Get(
                        "KEYVIEWER_EDITOR_EXPORTED_QKV",
                        "Exported as .qkv — the full layout and Key Viewer settings, for another Quartz install."
                    )
                    : DmNoteExportStatus();
            } else if(!string.IsNullOrEmpty(error)) {
                status.text = error;
            }
        }
        static string DmNoteExportStatus() {
            List<string> gaps = KvStore.DmNoteGaps();
            if(gaps.Count == 0) return MainCore.Tr.Get("KEYVIEWER_EDITOR_EXPORTED_JSON", "Exported for DM Note.");
            List<string> named = [];
            foreach(string gap in gaps) named.Add(GapName(gap));
            return string.Format(
                MainCore.Tr.Get(
                    "KEYVIEWER_EDITOR_EXPORTED_JSON_GAPS",
                    "Exported for DM Note, without: {0}. Export .qkv instead to keep them."
                ), string.Join(", ", named));
        }
        static string GapName(string gap) => gap switch {
            KvExportShaping.GapStats => MainCore.Tr.Get("KEYVIEWER_DMGAP_STATS", "Avg/Max stats (exported as KPS)"),
            KvExportShaping.GapGhostKeys => MainCore.Tr.Get("KEYVIEWER_DMGAP_GHOST", "ghost keys"),
            KvExportShaping.GapPressedLabels => MainCore.Tr.Get("KEYVIEWER_DMGAP_PRESSED_LABEL", "pressed labels"),
            KvExportShaping.GapHiddenLabels => MainCore.Tr.Get("KEYVIEWER_DMGAP_HIDDEN_LABEL", "hidden labels"),
            KvExportShaping.GapCounterWhilePressed => MainCore.Tr.Get("KEYVIEWER_DMGAP_COUNTER_PRESSED", "counters hidden while pressed"),
            KvExportShaping.GapCountInTotal => MainCore.Tr.Get("KEYVIEWER_DMGAP_COUNT_IN_TOTAL", "keys kept out of the total"),
            KvExportShaping.GapPerKeyKps => MainCore.Tr.Get("KEYVIEWER_DMGAP_PER_KEY_KPS", "per-key KPS counters"),
            KvExportShaping.GapFootRows => MainCore.Tr.Get("KEYVIEWER_DMGAP_FOOT", "foot-row markers"),
            KvExportShaping.GapPressScale => MainCore.Tr.Get("KEYVIEWER_DMGAP_PRESS_SCALE", "press scaling"),
            KvExportShaping.GapNoteShadows => MainCore.Tr.Get("KEYVIEWER_DMGAP_NOTE_SHADOW", "note shadows"),
            _ => MainCore.Tr.Get("KEYVIEWER_DMGAP_OTHER", "other Quartz-only settings"),
        };
        void Import() {
            if(KvStore.Import(out string error, out int settingsApplied)) {
                rebind();
                refreshStatus();
                if(settingsApplied > 0) {
                    status.text = string.Format(
                        MainCore.Tr.Get(
                            "KEYVIEWER_EDITOR_IMPORTED_QKV",
                            "Imported. {0} Key Viewer setting(s) came from the .qkv and replaced yours."
                        ), settingsApplied);
                }
            } else if(!string.IsNullOrEmpty(error)) {
                status.text = error;
            }
        }
        file.OnClick = () => KvPopup.Show(
            host, file.Rect,
            [
                ("KEYVIEWER_EDITOR_IMPORT", "Import"),
                ("KEYVIEWER_EDITOR_EXPORT_QKV", "Export .qkv (Quartz)"),
                ("KEYVIEWER_EDITOR_EXPORT_JSON", "Export .json (DM Note)"),
                ("KEYVIEWER_EDITOR_OPEN_FOLDER", "Open Presets Folder"),
            ],
            index => {
                switch(index) {
                    case 0:
                        Import();
                        break;
                    case 1:
                        Export(KvExportFormat.Quartz);
                        break;
                    case 2:
                        Export(KvExportFormat.DmNote);
                        break;
                    default:
                        KvStore.RevealLibrary();
                        break;
                }
            }
        );
    }
}

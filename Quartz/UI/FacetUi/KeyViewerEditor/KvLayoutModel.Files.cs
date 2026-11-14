#if !IL2CPP && !QUARTZ_KEYVIEWER
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Quartz.Compat.Game;
using Quartz.Core;
using Quartz.Features.KeyViewer;
using Quartz.Features.KeyViewer.Layout;
using KeyLayoutEditor = PrismLib.UI.Toolkit.KeyLayoutEditor<Quartz.Features.KeyViewer.Layout.KvElement>;
using LayoutGroup = PrismLib.UI.Toolkit.LayoutGroup;
using MenuItem = global::Facet.MenuItem;
using Role = global::Facet.Role;
namespace Quartz.UI.FacetUi.KeyViewerEditor;
// The tab actions and the File menu: presets, foot counts, import and export.
internal sealed partial class KvLayoutModel {
    private static string Tr(string key) => QuartzHost.Translate(key);

    // Said on the editor when there is one (the switcher can act with the page never built).
    private static void Say(string text, Role role = Role.TextMuted) {
        if(!string.IsNullOrEmpty(text)) KvEditorPage.Editor?.Say(text, role);
    }

    public override string LimitText() => KvStore.Current.CustomTabCount < KvDocument.MaxCustomTabs ? null
        : string.Format(Tr("KEYVIEWER_EDITOR_TAB_MAX"), KvStore.Current.CustomTabCount);

    // Every caller guards for the message too; this is the one place that cannot be missed.
    private bool AtLimit() {
        string limit = LimitText();
        if(limit == null) return false;
        Say(limit, Role.Danger);
        return true;
    }

    public override IList<MenuItem> NewItems(LayoutGroup group, Action<string> created) {
        List<MenuItem> items = [];
        if(group == LayoutGroup.Feet) {
            for(int s = 1; s <= KeyViewerSettings.MaxFootStyle; s++) {
                int n = s * 2;
                items.Add(new MenuItem {
                    Id = "foot-" + n, Text = string.Format(Tr("KEYVIEWER_FOOT_COUNT"), n),
                    Run = () => Create(LayoutGroup.Feet, Tr("KEYVIEWER_EDITOR_FOOT_TAB"), created, (doc, tab) => {
                        doc.SetFootTab(tab, true);
                        KvMigration.GenerateStockFootTab(doc, tab, doc.SelectedTab, n);
                    }),
                });
            }
            return items;
        }
        items.Add(new MenuItem {
            Id = "empty", Text = Tr("MENU_KVE_NEW_TAB"),
            Run = () => Create(LayoutGroup.Hands, Tr("MENU_KVE_NEW_TAB_NAME"), created, null),
        });
        foreach(int style in KvPresets.Styles) {
            string key = StyleKey(style);
            items.Add(new MenuItem {
                Id = key, Text = Tr(key),
                Run = () => Create(LayoutGroup.Hands, Tr(key), created, (doc, tab) => KvMigration.GenerateStockTab(doc, tab, style)),
            });
        }
        return items;
    }

    // The classic editor's preset names.
    internal static string StyleKey(int style) => style switch {
        0 => "KEYVIEWER_STYLE_10",
        1 => "KEYVIEWER_STYLE_12",
        3 => "KEYVIEWER_STYLE_20",
        4 => "KEYVIEWER_STYLE_8",
        5 => "KEYVIEWER_STYLE_14",
        KvPresets.Style24 => "KEYVIEWER_STYLE_24",
        KvPresets.Style108 => "KEYVIEWER_STYLE_108",
        _ => "KEYVIEWER_STYLE_16",
    };

    private void Create(LayoutGroup group, string name, Action<string> created, Action<KvDocument, string> fill) {
        if(AtLimit()) return;
        KvDocument doc = KvStore.Current;
        string tab = doc.NewTabId();
        doc.EnsureTab(tab, doc.UniqueTabName(name));
        fill?.Invoke(doc, tab);
        Show(group, tab);
        created?.Invoke(tab);
    }

    public override string Duplicate(string layout) {
        KvDocument doc = KvStore.Current;
        if(!doc.HasTab(layout) || AtLimit()) return null;
        string copy = doc.DuplicateTab(layout);
        if(copy != null) Show(doc.IsFootTab(copy) ? LayoutGroup.Feet : LayoutGroup.Hands, copy);
        return copy;
    }

    public override void Delete(string layout) => KvStore.Current.RemoveTab(layout);
    public override void Rename(string layout, string name) => KvStore.Current.RenameTab(layout, name);
    public override void Move(string layout, int indexInGroup) => KvStore.Current.MoveTabTo(layout, indexInGroup);

    // The switcher's export of any card: that layout is shown only while it is written, and no step is recorded.
    public override void Export(string layout) {
        KvDocument doc = KvStore.Current;
        if(!doc.HasTab(layout)) return;
        (string hand, string foot, string was) = (doc.SelectedTab, doc.SelectedFootTab, editing);
        if(doc.IsFootTab(layout)) doc.SelectedFootTab = layout;
        else doc.SelectedTab = layout;
        try {
            PickAndExport(KvExportFormat.DmNote);
        } finally {
            (doc.SelectedTab, doc.SelectedFootTab, editing) = (hand, foot, was);
        }
    }

    public override IList<MenuItem> FileItems(KeyLayoutEditor editor) => [
        new() { Id = "MENU_KVE_IMPORT_JSON", Text = Tr("MENU_KVE_IMPORT_JSON"), Run = () => PickAndImport("json") },
        new() { Id = "MENU_KVE_IMPORT_QKV", Text = Tr("MENU_KVE_IMPORT_QKV"), Run = () => PickAndImport("qkv") },
        new() {
            Id = "KEYVIEWER_DM_CSS_IMPORT", Text = Tr("KEYVIEWER_DM_CSS_IMPORT"),
            Run = () => {
                string error = null;
                ImportStep("KEYVIEWER_DM_CSS_IMPORT", () => KeyViewerOverlay.ImportDmNoteCss(out error));
                if(!string.IsNullOrEmpty(error)) Say(error, Role.Danger);
            },
        },
        MenuItem.Separator,
        new() { Id = "KEYVIEWER_EDITOR_EXPORT_JSON", Text = Tr("KEYVIEWER_EDITOR_EXPORT_JSON"), Run = () => PickAndExport(KvExportFormat.DmNote) },
        new() { Id = "KEYVIEWER_EDITOR_EXPORT_QKV", Text = Tr("KEYVIEWER_EDITOR_EXPORT_QKV"), Run = () => PickAndExport(KvExportFormat.Quartz) },
        MenuItem.Separator,
        new() { Id = "KEYVIEWER_EDITOR_OPEN_FOLDER", Text = Tr("KEYVIEWER_EDITOR_OPEN_FOLDER"), Run = KvStore.RevealLibrary },
    ];

    private void PickAndImport(string extension) {
        if(AtLimit()) return;
        string picked;
        try {
            Directory.CreateDirectory(KvStore.LibraryDir);
            picked = FileDialog.PickFile(KvStore.LibraryDir, extension == "qkv" ? "Quartz KeyViewer Preset" : "JSON Preset", [extension],
                Tr(extension == "qkv" ? "MENU_KVE_IMPORT_QKV" : "MENU_KVE_IMPORT_JSON"));
        } catch(Exception e) {
            Say("Picker failed: " + e.Message, Role.Danger);
            return;
        }
        if(!string.IsNullOrEmpty(picked)) ImportPath(picked, out _);
    }

    private void PickAndExport(KvExportFormat format) {
        bool quartz = format == KvExportFormat.Quartz;
        string extension = quartz ? "qkv" : "json", picked;
        try {
            Directory.CreateDirectory(KvStore.LibraryDir);
            picked = FileDialog.SaveFile(KvStore.LibraryDir, "quartz-keyviewer-preset." + extension, quartz ? "Quartz KeyViewer Preset" : "JSON Preset",
                [extension], Tr(quartz ? "KEYVIEWER_EDITOR_EXPORT_QKV" : "KEYVIEWER_EDITOR_EXPORT_JSON"));
        } catch(Exception e) {
            Say("Picker failed: " + e.Message, Role.Danger);
            return;
        }
        if(string.IsNullOrEmpty(picked)) return;
        if(string.IsNullOrEmpty(Path.GetExtension(picked))) picked += "." + extension;
        ExportPath(picked, format, out _);
    }

    /// <summary>Imports a .json or .qkv file as one undo step, and says how it went (message).</summary>
    internal bool ImportPath(string path, out string message) {
        message = LimitText();
        if(AtLimit()) return false;
        string error = null;
        int settings = 0, tabs = KvStore.Current.Tabs.Count();
        bool ok = false;
        string label = string.Equals(Path.GetExtension(path), ".qkv", StringComparison.OrdinalIgnoreCase) ? "MENU_KVE_IMPORT_QKV" : "MENU_KVE_IMPORT_JSON";
        ImportStep(label, () => {
            ok = KvStore.ImportFromPath(path, out error, out settings);
            if(ok) Show(LayoutGroup.Hands, KvStore.Current.SelectedTab);
            return ok;
        });
        bool added = KvStore.Current.Tabs.Count() > tabs;
        message = !ok ? error
            : settings > 0 ? string.Format(Tr("KEYVIEWER_EDITOR_IMPORTED_QKV"), settings)
            : !added ? Tr("KEYVIEWER_EDITOR_IMPORT_NO_KEYS")
            : string.Format(Tr("MENU_KVE_IMPORTED"), KvStore.Current.TabName(KvStore.Current.SelectedTab));
        Say(message, ok && added || settings > 0 ? Role.TextMuted : Role.Danger);
        return ok;
    }

    /// <summary>Writes the shown layout to path, and says how it went (message).</summary>
    internal bool ExportPath(string path, KvExportFormat format, out string message) {
        if(!KvStore.ExportToPath(path, format, out string error)) {
            message = error;
            Say(message, Role.Danger);
            return false;
        }
        List<string> gaps = format == KvExportFormat.DmNote ? KvStore.DmNoteGaps() : [];
        message = format == KvExportFormat.Quartz ? Tr("KEYVIEWER_EDITOR_EXPORTED_QKV")
            : gaps.Count == 0 ? Tr("KEYVIEWER_EDITOR_EXPORTED_JSON")
            : string.Format(Tr("KEYVIEWER_EDITOR_EXPORTED_JSON_GAPS"), string.Join(", ", gaps.Select(GapName)));
        Say(message);
        return true;
    }

    // The classic editor's names for what a DM Note export leaves out.
    private static string GapName(string gap) => Tr(gap switch {
        KvExportShaping.GapStats => "KEYVIEWER_DMGAP_STATS",
        KvExportShaping.GapGhostKeys => "KEYVIEWER_DMGAP_GHOST",
        KvExportShaping.GapPressedLabels => "KEYVIEWER_DMGAP_PRESSED_LABEL",
        KvExportShaping.GapHiddenLabels => "KEYVIEWER_DMGAP_HIDDEN_LABEL",
        KvExportShaping.GapCounterWhilePressed => "KEYVIEWER_DMGAP_COUNTER_PRESSED",
        KvExportShaping.GapCountInTotal => "KEYVIEWER_DMGAP_COUNT_IN_TOTAL",
        KvExportShaping.GapPerKeyKps => "KEYVIEWER_DMGAP_PER_KEY_KPS",
        KvExportShaping.GapFootRows => "KEYVIEWER_DMGAP_FOOT",
        KvExportShaping.GapPressScale => "KEYVIEWER_DMGAP_PRESS_SCALE",
        KvExportShaping.GapNoteShadows => "KEYVIEWER_DMGAP_NOTE_SHADOW",
        _ => "KEYVIEWER_DMGAP_OTHER",
    });

    /// <summary>One undo step over the layout and the Key Viewer settings around import; a failed or cancelled import
    /// changes nothing and records nothing. Live press counts are never rolled back.</summary>
    internal void ImportStep(string labelKey, Func<bool> import) {
        global::Facet.History.Snapshot<string>(labelKey, null, CaptureWithSettings, RestoreWithSettings, () => import(),
            global::Facet.History.BoundTo(UndoDocument), Host.Translate);
        Committed();
        KvEditorPage.Editor?.Refresh();
    }

    private string CaptureWithSettings() {
        JToken settings = KeyViewerOverlay.Conf.Serialize();
        (settings as JObject)?.Remove(nameof(KeyViewerSettings.Counts));
        JObject snapshot = new() { ["layout"] = Capture(), ["settings"] = settings };
        // An import merges images into the table: its undo takes them out again, its redo puts them back.
        if(KvStore.Current.Root[ImagesKey] is { } images) snapshot[ImagesKey] = images;   // copied by the add
        return snapshot.ToString(Formatting.None);
    }

    private void RestoreWithSettings(string snapshot) {
        JObject o = JObject.Parse(snapshot);
        KeyViewerSettings conf = KeyViewerOverlay.Conf;
        if(o["settings"] is JObject settings) {
            KeyViewerOverlay.FlushCounts();
            settings[nameof(KeyViewerSettings.Counts)] = (conf.Serialize() as JObject)?[nameof(KeyViewerSettings.Counts)]?.DeepClone();
            conf.Deserialize(settings);
        }
        Restore((string)o["layout"], o.Property(ImagesKey));
        KeyViewerOverlay.Apply();
        KeyViewerOverlay.Save();
    }
}
#endif

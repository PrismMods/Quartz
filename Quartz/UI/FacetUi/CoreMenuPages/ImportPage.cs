#if !IL2CPP
using Facet;
using Quartz.Async;
using Quartz.Features;
using Quartz.Features.Interop;
using UnityEngine.UIElements;
namespace Quartz.UI.FacetUi.CoreMenuPages;
/// <summary>Import in the new menu, row for row as PageImport. The page holds the option and incompatible-mod lists, read on
/// attach and on Rescan, so a refresh pass does no UMM reflection and the search index's detached build reads nothing. Each
/// import goes through QuartzShell.Do.</summary>
internal static class ImportPage {
    // The KeyViewer mode and parts per option for the session: this page's own, as the classic page keeps its own.
    private static readonly Dictionary<string, SettingsImportReplaceMode> modes = [];
    private static readonly Dictionary<string, SettingsImportKeyViewerPart> parts = [];
    private static readonly SettingsImportReplaceMode[] Modes =
        [SettingsImportReplaceMode.ReplaceAll, SettingsImportReplaceMode.ReplaceCertain, SettingsImportReplaceMode.KeepOld];
    private static readonly (SettingsImportKeyViewerPart Flag, string Key)[] Parts = [
        (SettingsImportKeyViewerPart.KeysLayout, "IMPORT_PART_KEYS"),
        (SettingsImportKeyViewerPart.Labels, "IMPORT_PART_LABELS"),
        (SettingsImportKeyViewerPart.Colors, "IMPORT_PART_COLORS"),
        (SettingsImportKeyViewerPart.Rain, "IMPORT_PART_RAIN"),
        (SettingsImportKeyViewerPart.PositionSize, "IMPORT_PART_POSITION"),
    ];

    internal static void Build(VisualElement p) {
        Control<bool> head = QuartzShell.Header(p, "IMPORT_HEADER", null, () => QuartzHost.Translate("IMPORT_HINT"));
        ShellPage.Described(p, "menu-header", "IMPORT_HINT");
        SettingsImportOption[] options = [];
        InstalledModInfo[] incompatible = [];
        string read = null;   // the held lists' ids; null until the first read
        void Read() {
            List<SettingsImportOption> found = SettingsImporter.GetAvailableOptions();
            HashSet<string> ids = new(found.Select(o => o.Id), StringComparer.OrdinalIgnoreCase);
            List<InstalledModInfo> others = [.. SettingsImporter.GetAllInstalledMods().Where(m => !ids.Contains(m.Id))];
            found.Sort((a, b) => string.Compare(a.Label, b.Label, StringComparison.OrdinalIgnoreCase));
            // Other mods by name, then the Prism suite's own mods as a group of their own.
            others.Sort((a, b) => a.Suite != b.Suite ? a.Suite.CompareTo(b.Suite) : string.Compare(a.Label, b.Label, StringComparison.OrdinalIgnoreCase));
            options = [.. found];
            incompatible = [.. others];
            read = string.Join("|", options.Select(o => o.OptionId)) + "/" + string.Join("|", incompatible.Select(m => m.Id));
        }
        void Rescan() {
            Read();
            QuartzShell.Surface?.Refresh();
        }

        Button rescan = Controls.Action(p, "", "IMPORT_RESCAN", Rescan, o => {
            o.Id = "IMPORT_RESCAN";
            o.Tooltip = "DESC_IMPORT_RESCAN";
        });
        if(head != null) {   // on the header's right, where a feature's switch sits: no row of its own
            VisualElement row = rescan.parent.parent;
            head.Root.Add(row);
            row.style.flexShrink = 0f;
            row.style.marginBottom = 0f;
            row[0].style.display = DisplayStyle.None;   // the empty label, which would grow
            ShellRows.Unrow(row);
        }

        StatusLine status = Controls.Status(p);
        status.Root.name = "import-status";

        VisualElement list = Controls.Card(p, null);
        VisualElement rows = Controls.Live(list, () => read, box => {
            if(read == null) return;
            if(options.Length == 0 && incompatible.Length == 0) {
                Label none = ShellRows.Inset(Ui.Muted(box, "IMPORT_NONE"));
                none.name = "IMPORT_NONE";
                none.style.whiteSpace = WhiteSpace.Normal;
                return;
            }
            int kv = 0;   // the KeyViewer rows count only the options that carry them, as the classic page lists them
            for(int i = 0; i < options.Length; i++) Option(box, options[i], i + 1, ref kv, status);
            for(int i = 0; i < incompatible.Length; i++) {
                VisualElement right = Controls.Row(box, "");
                Label name = Named(right.parent.Q<Label>(), incompatible[i].Label);
                if(name != null) Paint.Fg(name, Role.TextMuted);
                Label tag = Ui.Muted(right, incompatible[i].Suite ? "IMPORT_PRISM_SUITE" : "IMPORT_NOT_COMPATIBLE");
                tag.name = (incompatible[i].Suite ? "IMPORT_PRISM_SUITE#" : "IMPORT_NOT_COMPATIBLE#") + (i + 1);
                if(incompatible[i].Suite) Paint.Fg(tag, Role.Accent);
            }
        });
        rows.name = "import-list";

        // Only while shown: the search index builds this page detached.
        Ui.WhileAttached(list, Rescan, () => { });
    }

    private static void Option(VisualElement box, SettingsImportOption option, int n, ref int kv, StatusLine status) {
        Button run = Controls.Action(box, "", "IMPORT_DO", () => Run(option, false, status), o => {
            o.Id = "IMPORT_DO#" + n;
            o.Tooltip = "DESC_IMPORT_DO";
        });
        Named(run.parent?.parent?.Q<Label>(), option.Label);
        Button profile = Ui.Btn(run.parent, "IMPORT_PROFILE", () => {
            Run(option, true, status);
            QuartzShell.Surface?.Refresh();
        });
        profile.name = "IMPORT_PROFILE#" + n + "-input";
        profile.style.marginLeft = Theme.For(profile).Sizes.Gap;
        Words.Tip(profile, "DESC_IMPORT_PROFILE");
        if(!SettingsImporter.HasKeyViewerPayload(option.Source)) return;

        string id = option.OptionId;
        int k = ++kv;
        Controls.Dropdown<SettingsImportReplaceMode>(box, "IMPORT_KV_MODE", Bind.To(() => Mode(id), v => modes[id] = v), Modes, ModeKey,
            o => o.Id = "IMPORT_KV_MODE#" + k);
        foreach((SettingsImportKeyViewerPart flag, string key) in Parts)
            Controls.Toggle(box, key, Bind.To(() => (Part(id) & flag) != 0, on => parts[id] = on ? Part(id) | flag : Part(id) & ~flag), o => {
                o.Id = key + "#" + k;
                o.VisibleWhen = () => Mode(id) == SettingsImportReplaceMode.ReplaceCertain;
            });
    }

    // A mod's name as the mod gives it, not a Lang key: shown as given, and a relabel leaves it alone.
    private static Label Named(Label label, string text) {
        if(label == null) return null;
        Words.Label(label, "");
        label.text = text;
        label.name = "import-name";
        return label;
    }

    private static SettingsImportReplaceMode Mode(string id) => modes.TryGetValue(id, out var m) ? m : SettingsImportReplaceMode.ReplaceAll;

    private static SettingsImportKeyViewerPart Part(string id) => parts.TryGetValue(id, out var p) ? p : SettingsImportKeyViewerPart.All;

    private static string ModeKey(SettingsImportReplaceMode mode) => mode switch {
        SettingsImportReplaceMode.ReplaceAll => "IMPORT_MODE_REPLACE_ALL",
        SettingsImportReplaceMode.ReplaceCertain => "IMPORT_MODE_REPLACE_CERTAIN",
        _ => "IMPORT_MODE_KEEP_OLD",
    };

    // The classic page's RunImport: switch on the features the settings need first, then import on the next pump.
    private static void Run(SettingsImportOption option, bool profile, StatusLine status) =>
        QuartzShell.Do((profile ? "import.profile:" : "import.run:") + option.OptionId, () => {
            if(FeatureImport.EnsureForKind(SettingsImporter.KindOf(option.Source))) {
                status.Show("IMPORT_ENABLING");
                MainThread.Enqueue(() => Import(option, profile, status));
            } else Import(option, profile, status);
        });

    private static void Import(SettingsImportOption option, bool profile, StatusLine status) {
        string id = option.OptionId;
        SettingsImportResult result = profile
            ? SettingsImporter.ImportToProfile(option, Mode(id), Part(id))
            : SettingsImporter.Import(option, Mode(id), Part(id));
        string text = !result.Success ? string.Format(QuartzHost.Text("IMPORT_FAIL", "Import failed: {0}"), result.Message)
            : profile && result.ImportedCount > 0
                ? string.Format(QuartzHost.Text("IMPORT_PROFILE_OK", "Imported {0} settings from {1} into profile {2}."),
                    result.ImportedCount, option.Label, result.ProfileName)
            : result.ImportedCount > 0 ? string.Format(QuartzHost.Text("IMPORT_OK", "Imported {0} settings from {1}."), result.ImportedCount, option.Label)
            : string.Format(QuartzHost.Text("IMPORT_OK_NONE", "Nothing to import from {0}."), option.Label);
        // Holds a mod's name and a result message: shown as given, never looked up as a key.
        status.Show(() => text);
    }
}
#endif

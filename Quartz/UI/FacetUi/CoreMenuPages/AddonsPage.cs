#if !IL2CPP
using Facet;
using Quartz.Addons;
using Quartz.Async;
using Quartz.Compat.Game;
using Quartz.Core;
using UnityEngine;
using UnityEngine.UIElements;
namespace Quartz.UI.FacetUi.CoreMenuPages;
/// <summary>Addons in the new menu, row for row as PageAddons. Every button that acts goes through QuartzShell.Do; the update
/// check runs only while the page is shown, never in the search index's detached build. Addon names and action labels are drawn
/// as the addon gives them.</summary>
internal static class AddonsPage {
    private sealed class Item {
        public AddonService.Handle H;
        public int N;
        public string Key;
    }

    private static int serial;

    internal static void Build(VisualElement p) {
        QuartzShell.Header(p, "ADDONS", null, () => QuartzHost.Translate("ADDONS_HINT"));
        ShellPage.Described(p, "menu-header", "ADDONS_HINT");
        // This build's update results, by unit: one that lands after the page left only fills this dictionary.
        var updates = new Dictionary<string, AddonUpdateCheck.Result>();

        VisualElement card = Controls.Card(p, null);
        Button[] buttons = ShellRows.Buttons(card, new ShellRows.Btn("ADDONS_ADD", () => QuartzShell.Do("addons.add", AddAddon), "DESC_ADDONS_ADD"),
            new ShellRows.Btn("ADDONS_RELOAD", () => QuartzShell.Do("addons.reload", () => MainThread.Enqueue(AddonService.Reload)), "DESC_ADDONS_RELOAD"),
            new ShellRows.Btn("ADDONS_OPEN_FOLDER", () => QuartzShell.Do("addons.folder", AddonService.OpenAddonsFolder), "DESC_ADDONS_OPEN_FOLDER"));
        Label empty = ShellRows.Lead(buttons[0], Ui.Muted(card, "ADDONS_EMPTY"));   // on the buttons' row, at its left
        empty.style.whiteSpace = WhiteSpace.Normal;
        Controls.Hook(empty, o => {
            o.Id = "ADDONS_EMPTY";
            o.VisibleWhen = () => AddonService.Addons.Count == 0;
        });

        VisualElement list = Controls.Card(p, null, o => o.VisibleWhen = () => AddonService.Addons.Count > 0);
        // A reload makes new handles under the same units: each handle gets its own key, so its rows are its own.
        Item[] items = [];
        IReadOnlyList<Item> Items() {
            if(list.panel == null) return [];
            IReadOnlyList<AddonService.Handle> now = AddonService.Addons;
            bool same = now.Count == items.Length;
            for(int i = 0; same && i < items.Length; i++) same = items[i].H == now[i];
            if(!same) items = [.. now.Select((h, i) => new Item { H = h, N = i + 1, Key = h.UnitId + "#" + ++serial })];
            return items;
        }
        VisualElement rows = Controls.Repeat<Item>(list, Items, item => item.Key, (box, item) => Addon(box, item, updates));
        rows.name = "addons-list";

        // Only while shown: the search index builds this page detached, and the check reaches the network.
        Ui.WhileAttached(list, () => {
            foreach(AddonService.Handle h in AddonService.Addons) {
                if(!h.Loaded) continue;
                string unit = h.UnitId, repo = h.Instance.Repo, version = h.Version;
                updates.Remove(unit);
                QuartzShell.Do("addons.updatecheck:" + unit, () => AddonUpdateCheck.Check(repo, version, r => {
                    updates[unit] = r;
                    QuartzShell.Surface?.Refresh();
                }));
            }
        }, () => { });
    }

    private static void Addon(VisualElement box, Item item, Dictionary<string, AddonUpdateCheck.Result> updates) {
        AddonService.Handle h = item.H;
        string unit = h.UnitId;
        Control<bool> toggle = Controls.Toggle(box, "", Bind.To(() => h.Enabled,
            v => QuartzShell.Do("addons.enable:" + unit + ":" + v, () => AddonService.SetAddonEnabled(h, v))), o => {
            o.Id = Quartz.Localization.LocaleKeys.FromId("addon_" + unit);
            o.Undoable = false;
        });
        if(toggle.Root.Q<Label>() is Label name) {
            // The addon's own name, not a Lang key: shown as given, and a relabel leaves it alone.
            Words.Label(name, "");
            name.text = h.Name;
        }

        Control<string> status = ShellRows.Info(box, "addons-status#" + item.N,
            () => (h.Error, h.Enabled, h.Loaded, updates.TryGetValue(unit, out AddonUpdateCheck.Result r) ? r : null), s => Status(h),
            tone: () => h.Error != null ? Role.Danger : Role.TextMuted);
        Controls.Hook(status.Root, o => o.TooltipText = () => h.Error == null ? null : h.Error.Length > 900 ? h.Error[..900] + "…" : h.Error);
        Button link = Ui.Btn(status.Input.parent, "", () => QuartzShell.Do("addons.open:" + unit, () => {
            if(updates.TryGetValue(unit, out AddonUpdateCheck.Result r)) Application.OpenURL(r.Url);
        }));
        link.name = "addons-update#" + item.N;
        Words.Label(link, "");
        AddonUpdateCheck.Result shown = null;
        Controls.Hook(link, o => o.VisibleWhen = () => {
            AddonUpdateCheck.Result r = h.Loaded && updates.TryGetValue(unit, out AddonUpdateCheck.Result got) ? got : null;
            if(r != null && r != shown) link.text = string.Format(QuartzHost.Text("ADDONS_UPDATE_AVAILABLE", "update {0} available — click to open"), r.Tag);
            shown = r;
            return r != null;
        });

        Label restart = Ui.Muted(box, "ADDONS_RESTART_REQUIRED");
        restart.style.whiteSpace = WhiteSpace.Normal;
        Paint.Fg(restart, Role.Accent);
        Controls.Hook(restart, o => {
            o.Id = "ADDONS_RESTART_REQUIRED#" + item.N;
            o.VisibleWhen = () => h.Error == null && AddonService.NeedsRestart(h);
        });

        IReadOnlyList<(string Label, Action Run)> actions = h.Context?.Actions ?? [];
        for(int i = 0; i < actions.Count; i += 3) {
            VisualElement row = null;
            foreach((string label, Action run) in actions.Skip(i).Take(3)) {
                string named = label;
                Action click = () => QuartzShell.Do("addons.action:" + unit + ":" + named, run);
                Button b;
                if(row == null) {
                    b = Controls.Action(box, "", "", click);
                    row = b.parent;
                } else {
                    b = Ui.Btn(row, "", click);
                    b.style.marginLeft = Theme.For(b).Sizes.Gap;
                }
                Words.Label(b, "");
                b.text = named;
            }
        }

        bool armed = false;
        Button remove = null;
        remove = Controls.Action(box, "", "ADDONS_REMOVE", () => {
            if(!armed) {
                armed = true;
                Words.Label(remove, "ADDONS_REMOVE_CONFIRM");
                Paint.Look(remove, Theme.For(remove).Looks.DangerButton);
                return;
            }
            QuartzShell.Do("addons.remove:" + unit, () => AddonService.RemoveAddon(h));
        }, o => {
            o.Id = "ADDONS_REMOVE#" + item.N;
            o.Tooltip = "DESC_ADDONS_REMOVE";
        });
    }

    // Classic's status line: the error, Disabled, or the version, author and file.
    private static string Status(AddonService.Handle h) {
        if(h.Error != null) return h.Error;
        if(!h.Enabled) return QuartzHost.Text("ADDONS_STATUS_DISABLED", "Disabled");
        if(!h.Loaded) return "";
        string by = string.IsNullOrEmpty(h.Author) ? "" : $" · {h.Author}";
        return $"v{h.Version}{by} · {Path.GetFileName(h.SourcePath)}";
    }

    // Classic's Add Addon: pick a .qaddon or .dll, then copy it in and reload.
    private static void AddAddon() {
        string path = FileDialog.PickFile(null, Info.DisplayName + " Addon", AddonService.ImportExtensions,
            QuartzHost.Text("ADDONS_ADD_TITLE", "Add Quartz Addon"));
        if(string.IsNullOrEmpty(path)) return;
        AddonService.ImportAddon(path);
    }
}
#endif

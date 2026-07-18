using Quartz.Core;
using Quartz.Features.KeyViewer;
using Quartz.Features.KeyViewer.Layout;
using Quartz.Resource;
using Quartz.UI.Editor;
using Quartz.UI.Objects.Impl;
using Quartz.UI.Utility;
using TMPro;
using UnityEngine;
namespace Quartz.UI.Factory.Page;
internal static partial class PageKeyViewer {
    /// <summary>
    /// DM Note's TabTool, carrying Quartz's presets: the tab strip against the left edge of the bar
    /// where DM Note puts its own, and beside it the two buttons its TabList popup holds.
    ///
    /// The popup itself is not ported. Its job is to list the custom tabs and offer + / − under
    /// them, and DM Note needs that because its strip only ever shows the four builtins — here the
    /// strip already lists every tab, so all that is left of TabList is the two buttons, and they
    /// are on the bar rather than one click inside a tray that would show the list a second time.
    ///
    /// Returns the callback that repaints the strip. Tabs change from here and from a document
    /// swap (import, migrate); nothing polls.
    /// </summary>
    private static Action AppendTabStrip(
        RectTransform bar, KvCanvas canvas,
        TextMeshProUGUI status, Action refreshStatus
    ) {
        KvTabStrip strip = KvTabStrip.Create(bar);
        RectTransform actions = KvToolbar.Pill(bar);
        RectTransform host = KvToolbar.RegionOf(bar);
        UIButton delete = null;
        void Refresh() {
            KvDocument doc = KvStore.Current;
            List<string> tabs = [.. doc.Tabs];
            strip.Rebuild(tabs, doc.SelectedTab, doc.TabName, Select);
            // The last tab cannot go: KvDocument refuses it, so the button says so rather than
            // looking live and doing nothing.
            delete?.SetBlocked(tabs.Count <= 1, true);
        }
        void Select(string tab) {
            KvDocument doc = KvStore.Current;
            if(!doc.HasTab(tab)) return;
            doc.SelectedTab = tab;
            // Bind rather than Rebuild: this is a new editing session on a different tab, and
            // carrying the undo history across would let a rewind restore elements onto a tab they
            // never belonged to.
            canvas.Bind(doc, tab);
            KvStore.RequestSave();
            KeyViewerOverlay.RequestLayoutRebuild();
            Refresh();
            refreshStatus();
        }
        void Create(int style) {
            KvDocument doc = KvStore.Current;
            if(doc.CustomTabCount >= KvDocument.MaxCustomTabs) {
                status.text = string.Format(
                    MainCore.Tr.Get("KEYVIEWER_EDITOR_TAB_MAX", "You already have {0} tabs, DM Note's limit."),
                    KvDocument.MaxCustomTabs
                );
                return;
            }
            string tab = doc.NewTabId();
            // Named before the layout is generated: GenerateStockTab ensures the tab too, but
            // without a name, and an unnamed tab shows up as its raw id.
            doc.EnsureTab(tab, doc.UniqueTabName(StyleName(style)));
            // Stock, never the live settings: "8 Keys" means the stock 8-key layout.
            KvMigration.GenerateStockTab(doc, tab, style);
            Select(tab);
        }
        void Delete() {
            KvDocument doc = KvStore.Current;
            if(!doc.RemoveTab(doc.SelectedTab)) return;
            canvas.Bind(doc, doc.SelectedTab);
            KvStore.RequestSave();
            KeyViewerOverlay.RequestLayoutRebuild();
            Refresh();
            refreshStatus();
        }
        void SetFoot(int footCount) {
            KvDocument doc = canvas.Document;
            if(doc == null) return;
            // Picking the count that is already there regenerates the row, which would throw away
            // any editing done to those keys since. The list cannot show which entry is current, so
            // re-picking it has to be the no-op it looks like.
            if(KvPresets.FootCount(doc, canvas.Tab) == footCount) return;
            // Through the canvas, so a foot row is one undo step like every other edit; Mutated is
            // what saves and rebuilds the overlay.
            canvas.PushHistory();
            KvMigration.SetStockFootRow(doc, canvas.Tab, footCount);
            canvas.Rebuild();
            canvas.Mutated();
        }
        UIButton add = KvToolbar.Icon(
            actions, UISprite.Plus128, "keyviewer_editor_tab_add", null,
            "DESC_KEYVIEWER_EDITOR_TAB_ADD",
            "Add a tab holding one of the Simple mode key layouts, ready to edit."
        );
        add.OnClick = () => KvPopup.Show(host, add.Rect, PresetItems(), index => Create(KvPresets.Styles[index]));
        delete = KvToolbar.Icon(
            actions, UISprite.Minus128, "keyviewer_editor_tab_delete", null,
            "DESC_KEYVIEWER_EDITOR_TAB_DELETE",
            "Remove the tab you are editing, and every element on it."
        );
        // Two clicks rather than one, for the same reason the export choice is two entries: this UI
        // has no confirm dialog, so the entry in the tray is what asks.
        delete.OnClick = () => KvPopup.Show(
            host, delete.Rect,
            [("KEYVIEWER_EDITOR_TAB_DELETE_CONFIRM", "Delete this tab")],
            _ => Delete()
        );
        Danger(delete);
        UIButton foot = KvToolbar.Icon(
            actions, UISprite.Move128, "keyviewer_editor_foot", null,
            "DESC_KEYVIEWER_EDITOR_FOOT",
            "Add a row of foot keys under this tab's layout, or change how many. They light on press but don't count toward the total."
        );
        foot.OnClick = () => KvPopup.Show(host, foot.Rect, FootItems(), index => SetFoot(index * 2));
        Refresh();
        return Refresh;
    }
    /// <summary>
    /// The presets, in the order the Simple mode style dropdown lists them and under its own names,
    /// so "8 Keys" means one thing across the page.
    /// </summary>
    private static List<(string Key, string Text)> PresetItems() {
        List<(string, string)> items = [];
        // Null key, resolved text: KEYVIEWER_STYLE_* would localize itself, but the foot list below
        // cannot (its string takes a count), and a tray rebuilt on every open is already current in
        // whatever language is live. Keeping both lists on one rule beats a second style→key map
        // that could drift from StyleName.
        foreach(int style in KvPresets.Styles) items.Add((null, StyleName(style)));
        return items;
    }
    /// <summary>Off, then every foot count the legacy FootStyle axis allows.</summary>
    private static List<(string Key, string Text)> FootItems() {
        List<(string, string)> items = [];
        for(int s = 0; s <= KeyViewerSettings.MaxFootStyle; s++) items.Add((null, FootStyleName(s)));
        return items;
    }
    /// <summary>
    /// TabList's minus button: `bg-[#3C1E1E] hover:bg-[#442222]`. Applied after construction because
    /// <see cref="KvToolbar.Icon"/> builds every button in the bar's one language, and this is the
    /// only place DM Note departs from it.
    /// </summary>
    private static void Danger(UIButton button) {
        button.RestColor = static () => KvPalette.DangerBg;
        button.HoverColor = static () => KvPalette.DangerHover;
        button.UpdateVisual(true);
    }
}

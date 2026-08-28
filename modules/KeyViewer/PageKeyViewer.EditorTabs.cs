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
    private static Action AppendTabStrip(
        RectTransform bar, KvCanvas canvas,
        TextMeshProUGUI status, Action refreshStatus, Action refreshSettings
    ) {
        KvTabStrip handStrip = KvTabStrip.Create(
            bar, "KEYVIEWER_EDITOR_HAND_TABS", "Hand"
        );
        RectTransform actions = KvToolbar.Pill(bar);
        KvTabStrip footStrip = KvTabStrip.Create(
            bar, "KEYVIEWER_EDITOR_FOOT_TABS", "Foot"
        );
        RectTransform host = KvToolbar.RegionOf(bar);
        UIButton delete = null;
        void Refresh() {
            KvDocument doc = KvStore.Current;
            List<string> hands = [];
            List<string> feet = [];
            foreach(string tab in doc.Tabs) (doc.IsFootTab(tab) ? feet : hands).Add(tab);
            string hand = doc.SelectedTab;
            string foot = doc.SelectedFootTab;
            handStrip.Rebuild(hands, tab => tab == hand, canvas.Tab, doc.TabName, Select);
            footStrip.Rebuild(feet, tab => tab == foot, canvas.Tab, doc.TabName, Select);
            delete?.SetBlocked(!doc.IsFootTab(canvas.Tab) && doc.HandTabCount <= 1, true);
        }
        void Select(string tab) {
            KvDocument doc = KvStore.Current;
            if(!doc.HasTab(tab)) return;
            if(doc.IsFootTab(tab)) doc.SelectedFootTab = tab;
            else doc.SelectedTab = tab;
            canvas.Bind(doc, tab);
            KvStore.RequestSave();
            KeyViewerOverlay.RequestLayoutRebuild();
            Refresh();
            refreshStatus();
            refreshSettings();
        }
        bool AtTabLimit() {
            if(KvStore.Current.CustomTabCount < KvDocument.MaxCustomTabs) return false;
            status.text = string.Format(
                MainCore.Tr.Get("KEYVIEWER_EDITOR_TAB_MAX", "You already have {0} tabs, DM Note's limit."),
                KvDocument.MaxCustomTabs
            );
            return true;
        }
        void Create(int style) {
            if(AtTabLimit()) return;
            KvDocument doc = KvStore.Current;
            string tab = doc.NewTabId();
            doc.EnsureTab(tab, doc.UniqueTabName(StyleName(style)));
            KvMigration.GenerateStockTab(doc, tab, style);
            Select(tab);
        }
        void Delete() {
            KvDocument doc = KvStore.Current;
            if(!doc.RemoveTab(canvas.Tab)) return;
            canvas.Bind(doc, doc.SelectedTab);
            KvStore.RequestSave();
            KeyViewerOverlay.RequestLayoutRebuild();
            Refresh();
            refreshStatus();
            refreshSettings();
        }
        void SetFoot(int footCount) {
            KvDocument doc = canvas.Document;
            if(doc == null) return;
            if(footCount <= 0) {
                if(doc.SelectedFootTab == null) return;
                doc.SelectedFootTab = null;
                canvas.Rebuild();
                canvas.Mutated();
                Refresh();
                return;
            }
            if(doc.IsFootTab(canvas.Tab)) {
                canvas.PushHistory();
                KvMigration.GenerateStockFootTab(doc, canvas.Tab, doc.SelectedTab, footCount);
                canvas.Rebuild();
                canvas.Mutated();
                Refresh();
                return;
            }
            if(AtTabLimit()) return;
            string tab = doc.NewTabId();
            doc.EnsureTab(tab, doc.UniqueTabName(MainCore.Tr.Get("KEYVIEWER_EDITOR_FOOT_TAB", "Foot")));
            doc.SetFootTab(tab, true);
            KvMigration.GenerateStockFootTab(doc, tab, doc.SelectedTab, footCount);
            Select(tab);
        }
        UIButton add = KvToolbar.Icon(
            actions, UISprite.Plus128, "keyviewer_editor_tab_add", null,
            "DESC_KEYVIEWER_EDITOR_TAB_ADD",
            "Add a hand-key tab holding one of the Simple mode key layouts, ready to edit."
        );
        add.OnClick = () => KvPopup.Show(host, add.Rect, PresetItems(), index => Create(KvPresets.Styles[index]));
        delete = KvToolbar.Icon(
            actions, UISprite.Minus128, "keyviewer_editor_tab_delete", null,
            "DESC_KEYVIEWER_EDITOR_TAB_DELETE",
            "Remove the tab you are editing, and every element on it."
        );
        delete.OnClick = () => KvPopup.Show(
            host, delete.Rect,
            [("KEYVIEWER_EDITOR_TAB_DELETE_CONFIRM", "Delete this tab")],
            _ => Delete()
        );
        Danger(delete);
        UIButton foot = KvToolbar.Icon(
            footStrip.Pill, UISprite.Move128, "keyviewer_editor_foot", null,
            "DESC_KEYVIEWER_EDITOR_FOOT",
            "Add a foot-key tab, drawn alongside the hand tab you have open. Pick a count to build or resize one, or None to leave the foot keys off."
        );
        foot.OnClick = () => KvPopup.Show(host, foot.Rect, FootItems(), index => SetFoot(index * 2));
        Refresh();
        return Refresh;
    }
    private static List<(string Key, string Text)> PresetItems() {
        List<(string, string)> items = [];
        foreach(int style in KvPresets.Styles) items.Add((null, StyleName(style)));
        return items;
    }
    private static List<(string Key, string Text)> FootItems() {
        List<(string, string)> items = [];
        for(int s = 0; s <= KeyViewerSettings.MaxFootStyle; s++) items.Add((null, FootStyleName(s)));
        return items;
    }
    private static void Danger(UIButton button) {
        button.RestColor = static () => KvPalette.DangerBg;
        button.HoverColor = static () => KvPalette.DangerHover;
        button.UpdateVisual(true);
    }
}

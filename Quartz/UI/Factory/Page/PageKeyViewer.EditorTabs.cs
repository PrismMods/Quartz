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
            delete?.SetBlocked(tabs.Count <= 1, true);
        }
        void Select(string tab) {
            KvDocument doc = KvStore.Current;
            if(!doc.HasTab(tab)) return;
            doc.SelectedTab = tab;
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
            doc.EnsureTab(tab, doc.UniqueTabName(StyleName(style)));
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
            if(KvPresets.FootCount(doc, canvas.Tab) == footCount) return;
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

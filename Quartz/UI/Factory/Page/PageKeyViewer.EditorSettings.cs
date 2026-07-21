using Quartz.Core;
using Quartz.Features.KeyViewer;
using Quartz.Features.KeyViewer.Layout;
using Quartz.Resource;
using Quartz.UI.Editor;
using Quartz.UI.Generator;
using Quartz.UI.Objects.Impl;
using UnityEngine;
namespace Quartz.UI.Factory.Page;
internal static partial class PageKeyViewer {
    private static Action AppendEditorSettings(RectTransform body, KeyViewerSettings conf, Action refreshTabs) {
        KeyViewerSettings def = new();
        KvWidgets.Header(body, "KEYVIEWER_EDITOR_TAB_NAME", "Tab Name");
        UIInput tabName = KvWidgets.Input(
            GenerateUI.Row(body), "", KvStore.Current.TabName(KvStore.Current.SelectedTab),
            static _ => { }, "Name", MainCore.Spr.Get(UISprite.Text128),
            "keyviewer_editor_tab_name_ph"
        );
        tabName.InputField.onEndEdit.AddListener(raw => {
            KvDocument doc = KvStore.Current;
            string result = doc.RenameTab(doc.SelectedTab, raw);
            tabName.InputField.SetTextWithoutNotify(result ?? doc.TabName(doc.SelectedTab));
            if(result == null) return;
            refreshTabs();
            KvStore.RequestSave();
        });
        UIToggle showOutside = DmToggle(
            body, true,
            def.ShowOutsideGame,
            conf.ShowOutsideGame,
            v => { conf.ShowOutsideGame = v; KeyViewerOverlay.Save(); },
            "Show Outside Gameplay",
            "keyviewer_showoutside"
        );
        showOutside.Rect.AddToolTip(
            "DESC_KEYVIEWER_SHOWOUTSIDE",
            "Keep the key viewer visible in menus and outside of gameplay, not just while a level is playing."
        );
        UIToggle syncLimiter = DmSyncLimiter(body, conf, def, compact: true);
        Action refreshCss = AppendDmCss(body, conf, compact: true);
        Action refreshTuning = AppendDmTuning(body, conf, compact: true, includeOffsets: false);
        Action refreshGhostRainDots = AppendGhostRainDots(body, conf, def, compact: true);
        DmButton(
            body, true,
            () => KeyViewerOverlay.ResetPosition(),
            "Reset Position",
            "keyviewer_resetpos"
        ).SetSecondary();
        DmButton(
            body, true,
            () => KeyViewerOverlay.ResetCounts(),
            "Reset Counts",
            "keyviewer_resetcounts"
        ).SetSecondary().Rect.AddToolTip(
            "DESC_KEYVIEWER_RESETCOUNTS",
            "Clears every per-key press counter and the total."
        );
        return () => {
            tabName.InputField.SetTextWithoutNotify(KvStore.Current.TabName(KvStore.Current.SelectedTab));
            showOutside.Set(conf.ShowOutsideGame, false);
            syncLimiter.Set(conf.SyncToKeyLimiter, false);
            refreshCss();
            refreshTuning();
            refreshGhostRainDots();
        };
    }
}

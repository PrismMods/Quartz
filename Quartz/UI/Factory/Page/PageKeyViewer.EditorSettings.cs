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
    /// <summary>
    /// The Settings tab of the editor's inspector: everything about the viewer as a whole rather
    /// than about one element. Editor mode draws through the DM Note renderer and shares its
    /// settings, so these are the same controls the DM Note body builds, not an editor-local copy.
    ///
    /// Built once, refreshed on show. The DM Note body builds a control per field over the same
    /// conf and neither body is ever rebuilt, so without the re-read a stale control here would
    /// write its own old value back the next time it was touched.
    /// </summary>
    private static Action AppendEditorSettings(RectTransform body, KeyViewerSettings conf, Action refreshTabs) {
        KeyViewerSettings def = new();
        // Rename the tab being edited. DM Note has no rename, but two tabs generated from the same
        // preset share a base name ("16 Keys"), so this is how they are told apart.
        KvWidgets.Header(body, "KEYVIEWER_EDITOR_TAB_NAME", "Tab Name");
        // Wrapped in a Row (which gives the field its height — the widget itself adds none) and
        // handed a real icon: a null sprite renders as a stray solid quad.
        UIInput tabName = KvWidgets.Input(
            GenerateUI.Row(body), "", KvStore.Current.TabName(KvStore.Current.SelectedTab),
            static _ => { }, "Name", MainCore.Spr.Get(UISprite.Text128),
            "keyviewer_editor_tab_name_ph"
        );
        // onEndEdit, not onChanged: rename once on commit, not on every keystroke — a per-key rename
        // would uniquify a half-typed name and thrash the strip.
        tabName.InputField.onEndEdit.AddListener(raw => {
            KvDocument doc = KvStore.Current;
            string result = doc.RenameTab(doc.SelectedTab, raw);
            // A blank or rejected name reverts the field to the name still in force.
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
        // Every count on the layout, not just the selection's: the inspector's own Reset Count acts
        // on selected elements and leaves the KPS average and maximum standing, which is not what
        // "start over" means. ResetCounts already writes counts back through KvElement.Source —
        // that branch was unreachable while Simple mode's body held this button alone.
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

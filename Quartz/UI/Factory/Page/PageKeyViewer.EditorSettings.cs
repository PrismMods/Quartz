using Quartz.Core;
using Quartz.Features.KeyViewer;
using Quartz.Features.KeyViewer.Layout;
using Quartz.Resource;
using Quartz.UI.Editor;
using Quartz.UI.Generator;
using Quartz.UI.Objects.Impl;
using TMPro;
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
        UIToggle tabub = DmToggle(
            body, true,
            def.TabubEnabled,
            conf.TabubEnabled,
            v => { conf.TabubEnabled = v; KeyViewerOverlay.Apply(); KeyViewerOverlay.Save(); },
            "This Tabub Is Mine",
            "keyviewer_tabub"
        );
        tabub.Rect.AddToolTip(
            "DESC_KEYVIEWER_TABUB",
            "Past a point in the level, note rain stops and a picture drops over the key viewer, so nobody watching can read your tapping pattern. Presses are still counted underneath, and everything returns to normal on the next attempt."
        );
        UISlider tabubPercent = DmSlider(
            body, true, "Hide Tabub From", "keyviewer_tabub_percent",
            def.TabubPercent, 0f, 100f, conf.TabubPercent, "0' %'", 1f,
            v => { conf.TabubPercent = v; KeyViewerOverlay.Apply(); }, () => KeyViewerOverlay.Save()
        );
        tabubPercent.Rect.AddToolTip(
            "DESC_KEYVIEWER_TABUB_PERCENT",
            "How far into the level the picture takes over. It fades in over the second leading up to this point, and fades back out over the last second of the level."
        );
        UISlider tabubScale = DmSlider(
            body, true, "Tabub Image Size", "keyviewer_tabub_scale",
            def.TabubScale, 0.1f, 4f, conf.TabubScale, "0.00'x'", 0.05f,
            v => { conf.TabubScale = v; KeyViewerOverlay.ApplyTabub(); }, () => KeyViewerOverlay.Save()
        );
        tabubScale.Rect.AddToolTip(
            "DESC_KEYVIEWER_TABUB_SCALE",
            "Scales the picture. Drag it into place in Reorganize mode."
        );
        TextMeshProUGUI tabubStatus = GenerateUI.AddMutedText(GenerateUI.Row(body, 30f), 17f, 0.45f);
        void RefreshTabubStatus() => tabubStatus.text = !string.IsNullOrWhiteSpace(conf.TabubImagePath)
            ? Path.GetFileName(conf.TabubImagePath)
            : KeyViewerOverlay.HasTabubImage
                ? MainCore.Tr.Get("KEYVIEWER_TABUB_IMAGE_DEFAULT", "Using the built-in picture")
                : MainCore.Tr.Get("KEYVIEWER_TABUB_IMAGE_MISSING", "No picture set");
        DmButton(
            body, true,
            () => { KeyViewerOverlay.ImportTabubImage(out _); RefreshTabubStatus(); },
            "Custom Tabub Image",
            "keyviewer_tabub_image"
        ).Rect.AddToolTip(
            "DESC_KEYVIEWER_TABUB_IMAGE",
            "Pick your own picture instead of the one that ships with the mod."
        );
        DmButton(
            body, true,
            () => { KeyViewerOverlay.ClearTabubImage(); RefreshTabubStatus(); },
            "Use Built-in Image",
            "keyviewer_tabub_image_clear"
        ).SetSecondary();
        DmButton(
            body, true,
            () => KeyViewerOverlay.ResetTabubPosition(),
            "Reset Tabub Position",
            "keyviewer_tabub_resetpos"
        ).SetSecondary();
        RefreshTabubStatus();
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
            tabub.Set(conf.TabubEnabled, false);
            tabubPercent.SetOnlyValue(conf.TabubPercent, true);
            tabubScale.SetOnlyValue(conf.TabubScale, true);
            RefreshTabubStatus();
            syncLimiter.Set(conf.SyncToKeyLimiter, false);
            refreshCss();
            refreshTuning();
            refreshGhostRainDots();
        };
    }
}

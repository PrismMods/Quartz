using Quartz.Core;
using Quartz.Features.KeyViewer;
using Quartz.UI.Editor;
using Quartz.UI.Generator;
using Quartz.UI.Objects.Impl;
using TMPro;
using UnityEngine;
namespace Quartz.UI.Factory.Page;
internal static partial class PageKeyViewer {
    /// <summary>
    /// Which widget set a shared control builds with. The DM Note body is a full-width page and
    /// gets the shared rows; the editor builds the same controls into its property pane, which is
    /// a third of that width and needs <see cref="KvWidgets"/>.
    ///
    /// One seam rather than two copies of the builders: the two bodies drive the same fields on
    /// the same settings object, and a copy would be one edit away from disagreeing about which
    /// of them writes what.
    /// </summary>
    private static UIToggle DmToggle(
        RectTransform body, bool compact, bool def, bool value, Action<bool> onChanged, string text, string id
    ) => compact
        ? KvWidgets.Toggle(GenerateUI.Row(body), def, value, onChanged, text, id)
        : GenerateUI.Toggle(GenerateUI.Row(body), def, value, onChanged, text, id);
    private static UIButton DmButton(
        RectTransform body, bool compact, Action onClick, string text, string id
    ) => compact
        ? KvWidgets.Button(GenerateUI.Row(body, 44f), onClick, text, id)
        : GenerateUI.Button(GenerateUI.Row(body), onClick, text, id);
    private static UISlider DmSlider(
        RectTransform body, bool compact, string label, string id,
        float defVal, float min, float max, float val, string format, float step,
        Action<float> setter, Action save
    ) {
        if(!compact) return AddSlider(body, label, id, defVal, min, max, val, format, step, setter, save);
        float Snap(float v) => Mathf.Clamp(Mathf.Round(v / step) * step, min, max);
        UISlider s = KvWidgets.Slider(
            GenerateUI.Row(body), defVal, min, max, val, Snap, null, null, label, id
        );
        s.Format = format;
        s.OnChanged = setter;
        s.OnComplete = v => { setter(v); save?.Invoke(); };
        return s;
    }
    /// <summary>
    /// The Key Limiter sync toggle, for the two modes that have a key set of their own to sync
    /// (see KeyViewerOverlay.SyncModeSupported). Shared rather than built twice because flipping it
    /// on has to push the keys immediately and re-evaluate the Key Limiter page's lock, and a
    /// second copy of that would be one edit away from doing only one of them.
    /// </summary>
    private static UIToggle DmSyncLimiter(
        RectTransform body, KeyViewerSettings conf, KeyViewerSettings def, bool compact
    ) {
        UIToggle sync = DmToggle(
            body, compact,
            def.SyncToKeyLimiter,
            conf.SyncToKeyLimiter,
            v => {
                conf.SyncToKeyLimiter = v;
                KeyViewerOverlay.Save();
                if(v) KeyViewerOverlay.SyncKeysToKeyLimiter();
                KeyViewerOverlay.RaiseSyncSettingChanged();
            },
            "Sync Keys to Key Limiter",
            "keyviewer_synclimiter"
        );
        sync.Rect.AddToolTip(
            "DESC_KEYVIEWER_SYNCLIMITER",
            "Overwrites the Key Limiter's allowed keys with the keys shown here, and keeps them matched when you rebind keys or switch styles."
        );
        return sync;
    }
    /// <summary>
    /// The dotted ghost-rain styling. Both spawn paths read these — Simple mode's rain and the DM
    /// Note renderer Editor mode draws through — so the fields are live in Editor mode and only the
    /// controls were missing. Shared for the same reason as <see cref="DmSyncLimiter"/>: two bodies
    /// build a control per field over the one conf, and a second copy would be one edit away from
    /// disagreeing about the ranges or the write path.
    ///
    /// Returns the callback to run each time the body is shown. Both callers have to invoke it —
    /// neither body is ever rebuilt, so a stale control would write its own old value back on the
    /// next touch.
    /// </summary>
    private static Action AppendGhostRainDots(
        RectTransform body, KeyViewerSettings conf, KeyViewerSettings def, bool compact
    ) {
        void Save() => KeyViewerOverlay.Save();
        // No Apply: both spawn paths read these when a streak is created, not from built state.
        UIToggle dotted = DmToggle(
            body, compact,
            def.GhostRainDotted,
            conf.GhostRainDotted,
            v => { conf.GhostRainDotted = v; Save(); },
            "Dotted Ghost Rain",
            "keyviewer_ghostraindotted"
        );
        dotted.Rect.AddToolTip(
            "DESC_KEYVIEWER_GHOSTRAINDOTTED",
            "Ghost rain draws as a repeating dash pattern instead of a solid streak (port of JipperResourcePack's ghost rain)."
        );
        UISlider dotLength = DmSlider(body, compact, "Ghost Rain Dot Length", "keyviewer_ghostraindotlength",
            def.GhostRainDotLength, 1f, 60f, conf.GhostRainDotLength, "0 px", 1f,
            v => conf.GhostRainDotLength = v, Save);
        UISlider gapLength = DmSlider(body, compact, "Ghost Rain Gap Length", "keyviewer_ghostraingaplength",
            def.GhostRainGapLength, 1f, 60f, conf.GhostRainGapLength, "0 px", 1f,
            v => conf.GhostRainGapLength = v, Save);
        return () => {
            dotted.Set(conf.GhostRainDotted, false);
            dotLength.SetOnlyValue(conf.GhostRainDotLength, true);
            gapLength.SetOnlyValue(conf.GhostRainGapLength, true);
        };
    }
    /// <summary>
    /// Builds the custom CSS controls, which layer over whatever the DM Note renderer is drawing —
    /// an imported preset in DM Note mode, the edited layout in Editor mode. Both bodies build
    /// their own; returns the callback to run each time this one is shown.
    /// </summary>
    private static Action AppendDmCss(RectTransform body, KeyViewerSettings conf, bool compact = false) {
        KeyViewerSettings def = new();
        TextMeshProUGUI cssStatus = GenerateUI.AddMutedText(GenerateUI.Row(body, 30f), 17f, 0.45f);
        void RefreshCssStatus() => cssStatus.text = string.IsNullOrWhiteSpace(conf.DmCssText)
            ? MainCore.Tr.Get("KEYVIEWER_DM_CSS_NONE", "No custom CSS")
            : string.Format(MainCore.Tr.Get("KEYVIEWER_DM_CSS_LOADED", "Custom CSS: {0} chars"), conf.DmCssText.Length);
        UIToggle cssEnabled = DmToggle(
            body, compact,
            def.DmCssEnabled,
            conf.DmCssEnabled,
            v => { conf.DmCssEnabled = v; KeyViewerOverlay.Rebuild(); KeyViewerOverlay.Save(); },
            "Custom CSS",
            "keyviewer_dm_css_enabled"
        );
        DmButton(
            body, compact,
            () => {
                if(KeyViewerOverlay.ImportDmNoteCss(out string error)) RefreshCssStatus();
                else if(!string.IsNullOrEmpty(error)) cssStatus.text = error;
            },
            "Import Custom CSS",
            "keyviewer_dm_css_import"
        ).Rect.AddToolTip(
            "DESC_KEYVIEWER_DM_CSS_IMPORT",
            "Select a DM Note custom CSS file. Layers over the preset; restyles keys and counters."
        );
        DmButton(
            body, compact,
            () => {
                conf.DmCssText = "";
                conf.DmCssPath = "";
                KeyViewerOverlay.Rebuild();
                KeyViewerOverlay.Save();
                RefreshCssStatus();
            },
            "Clear CSS",
            "keyviewer_dm_css_clear"
        ).SetSecondary();
        RefreshCssStatus();
        return () => {
            cssEnabled.Set(conf.DmCssEnabled, false);
            RefreshCssStatus();
        };
    }
    /// <summary>
    /// Builds the tuning controls for the DM Note renderer, which both DM Note mode and
    /// Editor mode draw the in-game viewer through. Returns the callback to run each time
    /// the body is shown.
    /// <paramref name="includeOffsets"/> false drops the Offset X/Y sliders. The editor drags the
    /// overlay directly, so they are a second way to set a field the canvas already owns.
    /// </summary>
    private static Action AppendDmTuning(
        RectTransform body, KeyViewerSettings conf, bool compact = false, bool includeOffsets = true
    ) {
        KeyViewerSettings def = new();
        void Save() => KeyViewerOverlay.Save();
        void Apply() => KeyViewerOverlay.Apply();
        UIToggle noteEffect = DmToggle(
            body, compact,
            def.DmNoteEffect,
            conf.DmNoteEffect,
            v => { conf.DmNoteEffect = v; KeyViewerOverlay.Rebuild(); Save(); },
            "Note Rain",
            "keyviewer_dm_note"
        );
        UIToggle reverse = DmToggle(
            body, compact,
            def.DmNoteReverse,
            conf.DmNoteReverse,
            v => { conf.DmNoteReverse = v; Apply(); Save(); },
            "Reverse Rain",
            "keyviewer_dm_reverse"
        );
        UIToggle showCounter = DmToggle(
            body, compact,
            def.DmShowCounter,
            conf.DmShowCounter,
            v => { conf.DmShowCounter = v; KeyViewerOverlay.Rebuild(); Save(); },
            "Show Counter",
            "keyviewer_dm_counter"
        );
        UISlider scale = DmSlider(body, compact, "Scale", "keyviewer_dm_scale",
            def.DmScale, 0.2f, 4f, conf.DmScale, "0.00 x", 0.01f,
            v => { conf.DmScale = v; Apply(); }, Save);
        UISlider offsetX = includeOffsets ? DmSlider(body, compact, "Offset X", "keyviewer_dm_offsetx",
            def.DmOffsetX, -2000f, 2000f, conf.DmOffsetX, "0 px", 1f,
            v => { conf.DmOffsetX = v; Apply(); }, Save) : null;
        UISlider offsetY = includeOffsets ? DmSlider(body, compact, "Offset Y", "keyviewer_dm_offsety",
            def.DmOffsetY, -2000f, 2000f, conf.DmOffsetY, "0 px", 1f,
            v => { conf.DmOffsetY = v; Apply(); }, Save) : null;
        UISlider speed = DmSlider(body, compact, "Note Speed", "keyviewer_dm_speed",
            def.DmNoteSpeed, 10f, 1000f, conf.DmNoteSpeed, "0 px/s", 1f,
            v => { conf.DmNoteSpeed = v; Apply(); }, Save);
        UISlider track = DmSlider(body, compact, "Track Height", "keyviewer_dm_track",
            def.DmTrackHeight, 0f, 1000f, conf.DmTrackHeight, "0 px", 1f,
            v => { conf.DmTrackHeight = v; KeyViewerOverlay.Rebuild(); }, Save);
        UISlider fade = DmSlider(body, compact, "Fade (px)", "keyviewer_dm_fade",
            def.DmFadePx, 0f, 500f, conf.DmFadePx, "0 px", 1f,
            v => { conf.DmFadePx = v; Apply(); }, Save);
        UIToggle delayed = DmToggle(
            body, compact,
            def.DmDelayedNoteEnabled,
            conf.DmDelayedNoteEnabled,
            v => { conf.DmDelayedNoteEnabled = v; Apply(); Save(); },
            "Delayed Notes",
            "keyviewer_dm_delay_enabled"
        );
        UISlider shortThreshold = DmSlider(body, compact, "Short Note Threshold", "keyviewer_dm_short_threshold",
            def.DmShortNoteThresholdMs, 0f, 2000f, conf.DmShortNoteThresholdMs, "0 ms", 1f,
            v => { conf.DmShortNoteThresholdMs = v; Apply(); }, Save);
        UISlider shortMin = DmSlider(body, compact, "Short Note Min Length", "keyviewer_dm_short_min",
            def.DmShortNoteMinLengthPx, 1f, 9999f, conf.DmShortNoteMinLengthPx, "0 px", 1f,
            v => { conf.DmShortNoteMinLengthPx = v; Apply(); }, Save);
        UISlider keyDelay = DmSlider(body, compact, "Key Display Delay", "keyviewer_dm_key_delay",
            def.DmKeyDisplayDelayMs, 0f, 9999f, conf.DmKeyDisplayDelayMs, "0 ms", 1f,
            v => { conf.DmKeyDisplayDelayMs = v; Apply(); }, Save);
        // Both mode bodies build a control per field over the one shared conf and are only
        // hidden, never rebuilt, so the hidden body's controls still show what conf held when
        // the page was built. Without this re-read a stale slider would write its own old
        // value back to conf on the next drag.
        return () => {
            noteEffect.Set(conf.DmNoteEffect, false);
            reverse.Set(conf.DmNoteReverse, false);
            showCounter.Set(conf.DmShowCounter, false);
            scale.SetOnlyValue(conf.DmScale, true);
            offsetX?.SetOnlyValue(conf.DmOffsetX, true);
            offsetY?.SetOnlyValue(conf.DmOffsetY, true);
            speed.SetOnlyValue(conf.DmNoteSpeed, true);
            track.SetOnlyValue(conf.DmTrackHeight, true);
            fade.SetOnlyValue(conf.DmFadePx, true);
            delayed.Set(conf.DmDelayedNoteEnabled, false);
            shortThreshold.SetOnlyValue(conf.DmShortNoteThresholdMs, true);
            shortMin.SetOnlyValue(conf.DmShortNoteMinLengthPx, true);
            keyDelay.SetOnlyValue(conf.DmKeyDisplayDelayMs, true);
        };
    }
}

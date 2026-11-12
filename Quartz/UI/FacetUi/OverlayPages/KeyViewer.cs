#if !IL2CPP
using Facet;
using Quartz.Core;
using Quartz.Features.KeyViewer;
using Quartz.Features.KeyViewer.Js;
using Quartz.Overlay;
using Quartz.UI.Nav;
using UnityEngine.UIElements;
namespace Quartz.UI.FacetUi.OverlayPages;
/// <summary>
/// Overlay ▸ Key Viewer in the new menu: the settings rows of the classic page's inspector, beside a live preview of the key
/// viewer. The canvas editor, its inspector, the tab and file strips and the per-tab name stay in the classic menu, which a
/// card opens. Its switches have no hotkey, as in the classic page.
/// </summary>
internal static class KeyViewer {
    // Read on every call: a profile switch may replace what KeyViewerOverlay.Conf returns.
    private static KeyViewerSettings C => KeyViewerOverlay.Conf;
    private static readonly KeyViewerSettings Def = new();

    internal static void Page(VisualElement p) {
        KeyViewerOverlay.EnsureConf();
        ShellPage.Feature(p, "keyviewer", "SECTION_KEY_VIEWER", "KEYVIEWER_DESC", KeyViewerOverlay.FontKey, Cards);
    }

    private static void Cards(VisualElement p) {
        Action apply = KeyViewerOverlay.Apply, rebuild = KeyViewerOverlay.Rebuild, save = KeyViewerOverlay.Save;
        VisualElement kv = Controls.Card(p, "SECTION_KEY_VIEWER");
        Toggle(kv, "KEYVIEWER_SHOWOUTSIDE", () => C.ShowOutsideGame, v => C.ShowOutsideGame = v, Def.ShowOutsideGame, null, "DESC_KEYVIEWER_SHOWOUTSIDE");
        // Sets the field, saves and keeps the Key Limiter's keys matched; shown while the Key Limiter is loaded, asked live.
        ShellRows.Toggle(kv, "KEYVIEWER_SYNCLIMITER", null, Bind.To(() => C.SyncToKeyLimiter, KeyViewerOverlay.SetSyncToKeyLimiter), null, null, o => {
            o.Default = Def.SyncToKeyLimiter;
            o.Tooltip = "DESC_KEYVIEWER_SYNCLIMITER";
            o.VisibleWhen = () => KvKeyLimiterBridge.Available;
        });
        ShellRows.Font(kv, KeyViewerOverlay.FontKey);

        int state = NavRegistry.StateFor("overlay.keyviewer");
        Controls.Action(Controls.Card(p, null), "MENU_KV_EDITOR", "MENU_CLASSIC_OPEN", () => QuartzShell.OpenClassic(state), o => o.Id = "MENU_KV_EDITOR");

        VisualElement rain = Controls.Card(p, "KEYVIEWER_DM_NOTE");
        Toggle(rain, "KEYVIEWER_DM_NOTE", () => C.DmNoteEffect, v => C.DmNoteEffect = v, Def.DmNoteEffect, rebuild);
        Toggle(rain, "KEYVIEWER_DM_REVERSE", () => C.DmNoteReverse, v => C.DmNoteReverse = v, Def.DmNoteReverse, apply);
        Toggle(rain, "KEYVIEWER_DM_COUNTER", () => C.DmShowCounter, v => C.DmShowCounter = v, Def.DmShowCounter, rebuild);
        Slider(rain, "KEYVIEWER_DM_SCALE", () => C.DmScale, v => C.DmScale = v, Def.DmScale, 0.2f, 4f, 0.01f, "0.00 x", apply);
        Slider(rain, "KEYVIEWER_DM_FOOT_SCALE", () => C.DmFootScale, v => C.DmFootScale = v, Def.DmFootScale, 0.2f, 4f, 0.01f, "0.00 x", apply);
        Slider(rain, "KEYVIEWER_DM_SPEED", () => C.DmNoteSpeed, v => C.DmNoteSpeed = v, Def.DmNoteSpeed, 10f, 1000f, 1f, "0 px/s", apply);
        // The track is laid out by a rebuild: once a drag is let go, not per tick. An undo writes no let-go, so it rebuilds as it writes.
        Slider(rain, "KEYVIEWER_DM_TRACK", () => C.DmTrackHeight, v => {
            C.DmTrackHeight = v;
            if(History.Applying) rebuild();
        }, Def.DmTrackHeight, 0f, 1000f, 1f, "0 px", null).Committed += _ => rebuild();
        Slider(rain, "KEYVIEWER_DM_FADE", () => C.DmFadePx, v => C.DmFadePx = v, Def.DmFadePx, 0f, 500f, 1f, "0 px", apply);
        Toggle(rain, "KEYVIEWER_DM_DELAY_ENABLED", () => C.DmDelayedNoteEnabled, v => C.DmDelayedNoteEnabled = v, Def.DmDelayedNoteEnabled, apply);
        VisualElement timing = Controls.Advanced(rain);
        Slider(timing, "KEYVIEWER_DM_SHORT_THRESHOLD", () => C.DmShortNoteThresholdMs, v => C.DmShortNoteThresholdMs = v, Def.DmShortNoteThresholdMs,
            0f, 2000f, 1f, "0 ms", apply);
        Slider(timing, "KEYVIEWER_DM_KEY_DELAY", () => C.DmKeyDisplayDelayMs, v => C.DmKeyDisplayDelayMs = v, Def.DmKeyDisplayDelayMs, 0f, 9999f, 1f,
            "0 ms", apply);
        Toggle(timing, "KEYVIEWER_DM_INDEPENDENT_INPUT", () => C.IndependentInput, v => C.IndependentInput = v, Def.IndependentInput, apply);
        Slider(timing, "KEYVIEWER_DM_MIN_LIT", () => C.DmMinLitMs, v => C.DmMinLitMs = v, Def.DmMinLitMs, 0f, 500f, 1f, "0 ms", apply);

        VisualElement ghost = Controls.Card(p, "KEYVIEWER_GHOSTRAINDOTTED");
        Toggle(ghost, "KEYVIEWER_GHOSTRAINDOTTED", () => C.GhostRainDotted, v => C.GhostRainDotted = v, Def.GhostRainDotted, null,
            "DESC_KEYVIEWER_GHOSTRAINDOTTED");
        Func<bool> dotted = () => C.GhostRainDotted;
        Slider(ghost, "KEYVIEWER_GHOSTRAINDOTLENGTH", () => C.GhostRainDotLength, v => C.GhostRainDotLength = v, Def.GhostRainDotLength, 1f, 60f, 1f,
            "0 px", null, dotted);
        Slider(ghost, "KEYVIEWER_GHOSTRAINGAPLENGTH", () => C.GhostRainGapLength, v => C.GhostRainGapLength = v, Def.GhostRainGapLength, 1f, 60f, 1f,
            "0 px", null, dotted);

        VisualElement tabub = Controls.Card(p, "KEYVIEWER_TABUB");
        Toggle(tabub, "KEYVIEWER_TABUB", () => C.TabubEnabled, v => C.TabubEnabled = v, Def.TabubEnabled, apply, "DESC_KEYVIEWER_TABUB");
        Func<bool> mine = () => C.TabubEnabled;
        Slider(tabub, "KEYVIEWER_TABUB_PERCENT", () => C.TabubPercent, v => C.TabubPercent = v, Def.TabubPercent, 0f, 100f, 1f, "0' %'", apply, mine,
            "DESC_KEYVIEWER_TABUB_PERCENT");
        Slider(tabub, "KEYVIEWER_TABUB_SCALE", () => C.TabubScale, v => C.TabubScale = v, Def.TabubScale, 0.1f, 4f, 0.05f, "0.00'x'",
            KeyViewerOverlay.ApplyTabub, mine, "DESC_KEYVIEWER_TABUB_SCALE");
        ShellRows.Info(tabub, "KEYVIEWER_TABUB_IMAGE_STATUS", () => (C.TabubImagePath, KeyViewerOverlay.HasTabubImage), s =>
            !string.IsNullOrWhiteSpace(s.TabubImagePath) ? Path.GetFileName(s.TabubImagePath)
            : QuartzHost.Text(s.HasTabubImage ? "KEYVIEWER_TABUB_IMAGE_DEFAULT" : "KEYVIEWER_TABUB_IMAGE_MISSING",
                s.HasTabubImage ? "Using the built-in picture" : "No picture set"), mine);
        Button(tabub, "KEYVIEWER_TABUB_IMAGE", () => KeyViewerOverlay.ImportTabubImage(out _), "DESC_KEYVIEWER_TABUB_IMAGE", mine);   // it logs its errors
        Button(tabub, "KEYVIEWER_TABUB_IMAGE_CLEAR", KeyViewerOverlay.ClearTabubImage, null, mine);
        Button(tabub, "KEYVIEWER_TABUB_RESETPOS", KeyViewerOverlay.ResetTabubPosition, null, mine);

        VisualElement css = Controls.Card(p, "KEYVIEWER_DM_CSS_ENABLED");
        // The classic page shows an import's error here too; that text is English, so it goes to the log only.
        ShellRows.Info(css, "KEYVIEWER_DM_CSS_STATUS", () => C.DmCssText, t => string.IsNullOrWhiteSpace(t)
            ? QuartzHost.Text("KEYVIEWER_DM_CSS_NONE", "No custom CSS")
            : string.Format(QuartzHost.Text("KEYVIEWER_DM_CSS_LOADED", "Custom CSS: {0} chars"), t.Length));
        Toggle(css, "KEYVIEWER_DM_CSS_ENABLED", () => C.DmCssEnabled, v => C.DmCssEnabled = v, Def.DmCssEnabled, rebuild);
        Button(css, "KEYVIEWER_DM_CSS_IMPORT", () => KeyViewerOverlay.ImportDmNoteCss(out _), "DESC_KEYVIEWER_DM_CSS_IMPORT");
        Button(css, "KEYVIEWER_DM_CSS_CLEAR", () => {
            (C.DmCssText, C.DmCssPath) = ("", "");
            rebuild();
            save();
        });

        Js(Controls.Card(p, "KEYVIEWER_JS_TITLE"));

        VisualElement layout = Controls.Card(p, "MENU_LAYOUT");
        ShellRows.Anchor(layout, "KEYVIEWER_ANCHOR_LABEL", () => C.DmAnchor, () => C.DmAnchorClosest, a => KeyViewerOverlay.SetAnchor((OverlayAnchor)a), v => {
            C.DmAnchorClosest = v;
            save();
        });
        ShellRows.ResetPosition(layout, "KEYVIEWER_RESETPOS", KeyViewerOverlay.ResetPosition);
        Button(layout, "KEYVIEWER_RESETCOUNTS", KeyViewerOverlay.ResetCounts, "DESC_KEYVIEWER_RESETCOUNTS");
    }

    // The plugins, one switch each labelled with its name, as the classic page lists them. An import or a clear changes the
    // list, so the page rebuilds.
    private static void Js(VisualElement js) {
        KeyViewerSettings c = C;
        // The runtime's own summary: the classic page's English notes about an action are left out.
        Controls.Info(js, "", () => KvJsRuntime.Status, o => o.Id = "KEYVIEWER_JS_STATUS");
        ShellRows.Toggle(js, "KEYVIEWER_JS_ENABLED", null, Bind.To(() => c.JsPluginsEnabled, v => {
            c.JsPluginsEnabled = v;
            if(KvJsAssemblies.EnsureLoaded()) KvJsRuntime.Reload(c);
        }), null, KeyViewerOverlay.Save, o => {
            o.Default = Def.JsPluginsEnabled;
            o.Tooltip = "DESC_KEYVIEWER_JS_ENABLED";
        });
        foreach(KvJsPluginRecord plugin in c.JsPlugins.ToArray()) {
            string name = string.IsNullOrEmpty(plugin.Name) ? plugin.PluginId : plugin.Name;
            ShellRows.Toggle(js, name, null, Bind.To(() => plugin.Enabled, v => {
                plugin.Enabled = v;
                KvJsRuntime.Reload(c);
            }), null, KeyViewerOverlay.Save, o => {
                // The classic toggle's id as its key: a plugin id's "-" becomes "_" there, and search drops the classic twin only on a match.
                o.Id = Quartz.UI.Generator.GenerateUI.LocaleKeyFromId("keyviewer_js_plugin_" + plugin.PluginId);
                o.Default = true;
            });
        }
        Button(js, "KEYVIEWER_JS_IMPORT", () => {
            string error = "JavaScript engine could not be loaded.";
            if(KvJsAssemblies.EnsureLoaded() && KvJsRuntime.ImportPlugin(c, out error)) {
                KeyViewerOverlay.Save();
                QuartzShell.Changed();
            } else Log(error);
        }, "DESC_KEYVIEWER_JS_IMPORT");
        Button(js, "KEYVIEWER_JS_RELOAD", () => {
            KvJsRuntime.ReloadFiles(c, out string error);
            KeyViewerOverlay.Save();
            Log(error);
        });
        Button(js, "KEYVIEWER_JS_CLEAR", () => {
            KvJsRuntime.ClearPlugins(c);
            KeyViewerOverlay.Save();
            QuartzShell.Changed();
        });
    }

    // The classic page shows a file action's error beside it, in English; this page leaves it to the log.
    private static void Log(string error) {
        if(!string.IsNullOrEmpty(error)) MainCore.Log.Msg("[KeyViewer] " + error);
    }

    // A switch with no hotkey (the classic page's have none) that applies, or rebuilds, then saves.
    private static void Toggle(VisualElement card, string key, Func<bool> get, Action<bool> set, bool def, Action apply, string tip = null) =>
        ShellRows.Toggle(card, key, null, Bind.To(get, set), apply, KeyViewerOverlay.Save, o => {
            o.Default = def;
            o.Tooltip = tip;
        });

    private static Control<float> Slider(VisualElement card, string key, Func<float> get, Action<float> set, float def, float min, float max, float step,
        string format, Action apply, Func<bool> visible = null, string tip = null) =>
        ShellRows.Slider(card, key, Bind.To(get, set), min, max, step, format, apply, KeyViewerOverlay.Save, o => {
            o.Default = def;
            o.VisibleWhen = visible;
            o.Tooltip = tip;
        });

    private static void Button(VisualElement card, string key, Action onClick, string tip = null, Func<bool> visible = null) =>
        Controls.Action(card, "", key, onClick, o => {
            o.Id = key;
            o.Tooltip = tip;
            o.VisibleWhen = visible;
        });
}
#endif

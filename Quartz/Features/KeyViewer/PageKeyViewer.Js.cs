using Quartz.Core;
using Quartz.Features.KeyViewer;
using Quartz.Features.KeyViewer.Js;
using Quartz.UI.Editor;
using Quartz.UI.Generator;
using Quartz.UI.Objects.Impl;
using TMPro;
using UnityEngine;

namespace Quartz.UI.Factory.Page;

internal static partial class PageKeyViewer {
    private static Action AppendJsPlugins(RectTransform body, KeyViewerSettings conf, bool compact) {
        KeyViewerSettings defaults = new();
        KvWidgets.Header(body, "KEYVIEWER_JS_TITLE", "JavaScript Plugins");
        TextMeshProUGUI status = GenerateUI.AddMutedText(GenerateUI.Row(body, 42f), 16f, 0.55f);
        UIToggle enabled = DmToggle(
            body, compact,
            defaults.JsPluginsEnabled,
            conf.JsPluginsEnabled,
            value => {
                conf.JsPluginsEnabled = value;
                if(KvJsAssemblies.EnsureLoaded()) KvJsRuntime.Reload(conf);
                KeyViewerOverlay.Save();
                RefreshStatus();
            },
            "Enable JavaScript Plugins",
            "keyviewer_js_enabled"
        );
        enabled.Rect.AddToolTip(
            "DESC_KEYVIEWER_JS_ENABLED",
            "Runs imported DM Note-style JavaScript plugins inside a limited Jint sandbox. Only load plugins you trust."
        );
        List<(KvJsPluginRecord record, UIToggle toggle)> pluginToggles = [];
        foreach(KvJsPluginRecord plugin in conf.JsPlugins.ToArray()) {
            KvJsPluginRecord captured = plugin;
            string label = string.IsNullOrEmpty(plugin.Name) ? plugin.PluginId : plugin.Name;
            UIToggle toggle = DmToggle(
                body, compact, true, plugin.Enabled,
                value => {
                    captured.Enabled = value;
                    KvJsRuntime.Reload(conf);
                    KeyViewerOverlay.Save();
                    RefreshStatus();
                },
                label,
                "keyviewer_js_plugin_" + plugin.PluginId
            );
            pluginToggles.Add((captured, toggle));
        }
        DmButton(
            body, compact,
            () => {
                string error = KvJsAssemblies.EnsureLoaded() ? null : "JavaScript engine could not be loaded.";
                if(error == null && KvJsRuntime.ImportPlugin(conf, out error)) {
                    KeyViewerOverlay.Save();
                    RefreshStatus("Imported. Reopen Settings to see its toggle.");
                } else if(!string.IsNullOrEmpty(error)) {
                    RefreshStatus(error);
                }
            },
            "Import JavaScript Plugin",
            "keyviewer_js_import"
        ).Rect.AddToolTip(
            "DESC_KEYVIEWER_JS_IMPORT",
            "Import a .js or .mjs plugin written for DM Note's declarative defineElement API."
        );
        DmButton(
            body, compact,
            () => {
                int count = KvJsRuntime.ReloadFiles(conf, out string error);
                KeyViewerOverlay.Save();
                RefreshStatus(string.IsNullOrEmpty(error) ? $"Reloaded {count} plugin file(s)." : error);
            },
            "Reload Plugin Files",
            "keyviewer_js_reload"
        ).SetSecondary();
        DmButton(
            body, compact,
            () => {
                KvJsRuntime.ClearPlugins(conf);
                KeyViewerOverlay.Save();
                foreach((_, UIToggle toggle) in pluginToggles) toggle.SetBlocked(true, true);
                RefreshStatus("Removed imported plugins. Stored plugin data remains isolated on disk.");
            },
            "Remove All Plugins",
            "keyviewer_js_clear"
        ).SetSecondary();
        RefreshStatus();
        return () => {
            enabled.Set(conf.JsPluginsEnabled, false);
            foreach((KvJsPluginRecord plugin, UIToggle toggle) in pluginToggles)
                toggle.Set(plugin.Enabled, false);
            RefreshStatus();
        };

        void RefreshStatus(string prefix = null) {
            string value = KvJsRuntime.Status;
            status.text = string.IsNullOrEmpty(prefix) ? value : prefix + " " + value;
        }
    }
}

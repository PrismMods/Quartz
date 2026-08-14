using Jint;
using Jint.Native;
using Quartz.Compat.Game;
using Quartz.Core;
using Quartz.Features.KeyViewer.Layout;
using Quartz.Utility;
using UnityEngine;

namespace Quartz.Features.KeyViewer.Js;

internal static partial class KvJsRuntime {
    private const int MaxPluginBytes = 1024 * 1024;
    private static readonly List<PluginRuntime> plugins = [];
    private static RectTransform attached;
    private static float now;
    private static bool rendering;
    private static string lastError = "";

    internal static string Status {
        get {
            int definitions = 0;
            foreach(PluginRuntime plugin in plugins) definitions += plugin.DefinitionCount;
            string summary = $"{plugins.Count} plugin(s), {definitions} panel(s)";
            return string.IsNullOrEmpty(lastError) ? summary : summary + " · " + lastError;
        }
    }

    internal static void Reload(KeyViewerSettings settings) {
        RectTransform target = attached;
        Detach();
        DisposePlugins();
        lastError = "";
        now = KvClock.Now;
        if(settings is not { JsPluginsEnabled: true }) {
            attached = target;
            return;
        }
        foreach(KvJsPluginRecord record in settings.JsPlugins) {
            if(record is not { Enabled: true } || string.IsNullOrWhiteSpace(record.Content)) continue;
            try {
                PluginRuntime runtime = new(record);
                runtime.Load();
                plugins.Add(runtime);
            } catch(Exception e) {
                Report(record.Name, e);
            }
        }
        if(target != null) Attach(target);
    }

    internal static bool ImportPlugin(KeyViewerSettings settings, out string error) {
        error = null;
        string path;
        try {
            path = FileDialog.PickFile("", "JavaScript", ["js", "mjs"], "Import DM Note JavaScript plugin");
        } catch(Exception e) {
            error = "Picker failed: " + e.Message;
            return false;
        }
        if(string.IsNullOrEmpty(path)) return false;
        try {
            FileInfo info = new(path);
            if(!info.Exists) throw new FileNotFoundException("Plugin file not found.", path);
            if(info.Length > MaxPluginBytes) throw new InvalidDataException("Plugin is larger than 1 MiB.");
            string content = File.ReadAllText(path);
            KvJsPluginRecord existing = settings.JsPlugins.FirstOrDefault(p =>
                string.Equals(p.Path, path, StringComparison.OrdinalIgnoreCase));
            if(existing == null) {
                settings.JsPlugins.Add(new KvJsPluginRecord {
                    Name = Path.GetFileName(path),
                    Path = path,
                    Content = content,
                    Enabled = true,
                });
            } else {
                existing.Name = Path.GetFileName(path);
                existing.Content = content;
                existing.Enabled = true;
            }
            settings.JsPluginsEnabled = true;
            Reload(settings);
            return true;
        } catch(Exception e) {
            error = "Import failed: " + e.Message;
            return false;
        }
    }

    internal static int ReloadFiles(KeyViewerSettings settings, out string error) {
        error = null;
        int updated = 0;
        List<string> failures = [];
        foreach(KvJsPluginRecord plugin in settings.JsPlugins) {
            if(string.IsNullOrWhiteSpace(plugin.Path)) continue;
            try {
                FileInfo info = new(plugin.Path);
                if(!info.Exists) throw new FileNotFoundException("not found");
                if(info.Length > MaxPluginBytes) throw new InvalidDataException("larger than 1 MiB");
                plugin.Content = File.ReadAllText(plugin.Path);
                plugin.Name = Path.GetFileName(plugin.Path);
                updated++;
            } catch(Exception e) {
                failures.Add((plugin.Name.Length > 0 ? plugin.Name : plugin.Path) + ": " + e.Message);
            }
        }
        if(failures.Count > 0) error = string.Join("; ", failures);
        Reload(settings);
        return updated;
    }

    internal static void ClearPlugins(KeyViewerSettings settings) {
        settings.JsPlugins.Clear();
        Reload(settings);
    }

    internal static void Attach(RectTransform parent) {
        if(parent == null) return;
        if(attached != null) Detach();
        now = KvClock.Now;
        attached = parent;
        foreach(PluginRuntime plugin in plugins) plugin.Mount();
        RenderAll();
    }

    internal static void Detach() {
        foreach(PluginRuntime plugin in plugins) plugin.Unmount();
        attached = null;
    }

    internal static void Tick(float time) {
        now = time;
        KvJsStorage.Tick(time);
        bool dirty = false;
        foreach(PluginRuntime plugin in plugins) {
            plugin.Tick(time);
            dirty |= plugin.Dirty;
        }
        if(dirty && attached != null && !rendering) RenderAll();
    }

    internal static void OnKeyEvent(KeyCode key, bool down) {
        string label = KvKeyNames.ToGlobalKeyOrRaw(key);
        string state = down ? "DOWN" : "UP";
        string mode = KvStore.Current?.SelectedTab ?? "";
        string device = key >= KeyCode.Mouse0 && key <= KeyCode.Mouse6 ? "mouse"
            : key >= KeyCode.JoystickButton0 ? "gamepad" : "keyboard";
        foreach(PluginRuntime plugin in plugins) plugin.EmitKey(label, state, mode, device);
    }

    internal static void Shutdown() {
        Detach();
        DisposePlugins();
        KvJsStorage.Flush();
        lastError = "";
    }

    private static void RenderAll() {
        if(attached == null) return;
        rendering = true;
        try {
            float x = attached.rect.width + 20f;
            float y = 0f;
            foreach(PluginRuntime plugin in plugins) {
                y = plugin.RenderPanels(attached, x, y);
                plugin.Dirty = false;
            }
            KeyViewerOverlay.RefreshDragBounds();
        } finally {
            rendering = false;
        }
    }

    private static void DisposePlugins() {
        foreach(PluginRuntime plugin in plugins) plugin.Dispose();
        plugins.Clear();
    }

    private static void Report(string owner, Exception e) {
        string name = string.IsNullOrEmpty(owner) ? "plugin" : owner;
        lastError = name + ": " + e.Message;
        MainCore.Log.Msg("[KeyViewerJS] " + lastError);
        Diag.Warn(e, "KeyViewerJS/" + name);
    }

    private static string NormalizeAnchor(string anchor) => anchor switch {
        "top-center" or "top-right" or "center-left" or "center" or "center-right"
        or "bottom-left" or "bottom-center" or "bottom-right" => anchor,
        _ => "top-left",
    };
    private static bool Nullish(JsValue value) => value.IsNull() || value.IsUndefined();
}

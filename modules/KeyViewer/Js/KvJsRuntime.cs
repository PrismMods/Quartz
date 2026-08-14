using Jint;
using Jint.Native;
using Jint.Native.Object;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Quartz.Compat.Game;
using Quartz.Core;
using Quartz.Features.KeyViewer.Layout;
using Quartz.Utility;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Quartz.Features.KeyViewer.Js;

internal static class KvJsRuntime {
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

    private sealed class PluginRuntime {
        private const string Bootstrap = """
            (() => {
              "use strict";
              const host = globalThis.__kvHost;
              const parse = raw => raw == null || raw === "" ? null : JSON.parse(raw);
              const html = (strings, ...values) => host.Html(strings, values);
              const css = (strings, ...values) => strings.reduce((s, part, i) => s + part + (i < values.length ? (values[i] ?? "") : ""), "");
              const styleMap = styles => Object.entries(styles || {}).filter(([, value]) => value != null).map(([key, value]) => `${key.replace(/[A-Z]/g, c => "-" + c.toLowerCase())}: ${value}`).join("; ");
              globalThis.console = Object.freeze({
                log: (...args) => host.Log(args.map(String).join(" ")),
                info: (...args) => host.Log(args.map(String).join(" ")),
                warn: (...args) => host.Warn(args.map(String).join(" ")),
                error: (...args) => host.Warn(args.map(String).join(" ")),
              });
              globalThis.setTimeout = (fn, ms = 0) => host.SetTimer(fn, Number(ms) || 0, false);
              globalThis.clearTimeout = id => host.ClearTimer(Number(id) || 0);
              globalThis.setInterval = (fn, ms = 0) => host.SetTimer(fn, Number(ms) || 0, true);
              globalThis.clearInterval = id => host.ClearTimer(Number(id) || 0);
              const subscribe = (definitionId, eventName, callback) => {
                const token = host.Subscribe(definitionId, eventName, callback);
                return () => host.Unsubscribe(token);
              };
              globalThis.__kvParse = parse;
              globalThis.__kvHelpers = definitionId => Object.freeze({
                html, css, styleMap, locale: "en",
                t: (key, params, fallback) => host.Translate(definitionId, String(key), fallback == null ? "" : String(fallback)),
              });
              globalThis.__kvContext = definitionId => ({
                setState: updates => host.SetState(definitionId, updates),
                getSettings: () => host.GetSettings(definitionId),
                setAnchor: anchor => host.SetAnchor(definitionId, String(anchor)),
                getAnchor: () => host.GetAnchor(definitionId),
                onHook: (eventName, callback) => subscribe(definitionId, String(eventName), callback),
                expose: actions => host.Expose(definitionId, actions),
                locale: "en",
                t: (key, params, fallback) => host.Translate(definitionId, String(key), fallback == null ? "" : String(fallback)),
                onLocaleChange: callback => () => {},
                onSettingsChange: callback => subscribe(definitionId, "settings", callback),
              });
              globalThis.dmn = Object.freeze({
                window: Object.freeze({ type: "overlay" }),
                plugin: Object.freeze({
                  defineElement: definition => host.DefineElement(definition),
                  registerCleanup: cleanup => host.RegisterCleanup(cleanup),
                  storage: Object.freeze({
                    get: async key => parse(host.StorageGet(String(key))),
                    set: async (key, value) => host.StorageSet(String(key), JSON.stringify(value)),
                    remove: async key => host.StorageRemove(String(key)),
                    clear: async () => host.StorageClear(),
                    keys: async () => parse(host.StorageKeys()),
                    hasData: async prefix => host.StorageHasData(String(prefix)),
                    clearByPrefix: async prefix => host.StorageClearByPrefix(String(prefix)),
                  }),
                }),
                keys: Object.freeze({
                  onKeyState: callback => subscribe("", "key", callback),
                  onRawInput: callback => subscribe("", "rawKey", callback),
                }),
                stats: Object.freeze({
                  get: () => parse(host.StatsJson()),
                  subscribe: callback => subscribe("", "stats", callback),
                  reset: () => host.ResetStats(),
                }),
              });
              delete globalThis.__kvHost;
            })();
            """;

        private readonly KvJsPluginRecord record;
        private readonly Engine engine;
        private readonly HostBridge host;
        private readonly List<JsValue> cleanups = [];
        private readonly Dictionary<int, Subscription> subscriptions = [];
        private readonly Dictionary<int, Timer> timers = [];
        private int nextDefinition;
        private int nextSubscription;
        private int nextTimer;
        private float nextStats;
        private string activeDefinition;
        private readonly List<Definition> definitions = [];
        internal int DefinitionCount => definitions.Count;
        internal bool Dirty;

        internal PluginRuntime(KvJsPluginRecord record) {
            this.record = record;
            engine = new Engine(options => options
                .Strict()
                .MaxStatements(250_000)
                .LimitMemory(24_000_000)
                .TimeoutInterval(TimeSpan.FromMilliseconds(250)));
            host = new HostBridge(this);
        }

        internal void Load() {
            engine.SetValue("__kvHost", host);
            engine.Execute(Bootstrap, "quartz-keyviewer-bootstrap.js");
            string source = "(function () {\n\"use strict\";\n" + (record.Content ?? "") + "\n}).call(undefined);";
            engine.Execute(source, string.IsNullOrEmpty(record.Path) ? record.Name : record.Path);
        }

        internal void Mount() {
            foreach(Definition definition in definitions) definition.Mount();
            Dirty = true;
        }

        internal void Unmount() {
            foreach(Definition definition in definitions) definition.Unmount();
            RemoveScopedResources(null, mountedOnly: true);
            foreach(Definition definition in definitions) definition.DestroyVisual();
        }

        internal void Tick(float time) {
            if(timers.Count > 0) {
                int[] due = timers.Values.Where(timer => time >= timer.Due).Select(timer => timer.Id).ToArray();
                foreach(int id in due) {
                    if(!timers.TryGetValue(id, out Timer timer)) continue;
                    if(timer.Repeat) timer.Due = time + timer.Period;
                    else timers.Remove(id);
                    InvokeScoped(timer.DefinitionId, timer.Callback);
                }
            }
            if(time >= nextStats && subscriptions.Values.Any(static sub => sub.EventName == "stats")) {
                nextStats = time + 0.05f;
                JsValue payload = StatsValue();
                Emit("stats", payload);
            }
        }

        internal void EmitKey(string label, string state, string mode, string device) {
            ObjectInstance key = NewObject();
            Put(key, "key", label);
            Put(key, "state", state);
            Put(key, "mode", mode);
            Emit("key", key);
            ObjectInstance raw = NewObject();
            Put(raw, "device", device);
            Put(raw, "label", label);
            Put(raw, "labels", new JsArray(engine, [(JsValue)label]));
            Put(raw, "state", state);
            Emit("rawKey", raw);
        }

        internal float RenderPanels(RectTransform parent, float x, float y) {
            foreach(Definition definition in definitions) {
                Vector2 size = definition.Render(parent, x, y);
                y += Mathf.Max(20f, size.y) + 12f;
            }
            return y;
        }

        internal void Dispose() {
            Unmount();
            foreach(JsValue cleanup in cleanups) InvokeScoped(null, cleanup);
            cleanups.Clear();
            subscriptions.Clear();
            timers.Clear();
        }

        private void Emit(string eventName, JsValue payload) {
            Subscription[] targets = subscriptions.Values.Where(sub => sub.EventName == eventName).ToArray();
            foreach(Subscription sub in targets) InvokeScoped(sub.DefinitionId, sub.Callback, payload);
        }

        private void InvokeScoped(string definitionId, JsValue callback, params object[] args) {
            string previous = activeDefinition;
            activeDefinition = definitionId;
            try {
                engine.Constraints.Reset();
                engine.Invoke(callback, args);
            } catch(Exception e) {
                Report(record.Name, e);
            } finally {
                activeDefinition = previous;
            }
        }

        private ObjectInstance NewObject() => engine.Evaluate("({})").AsObject();
        private static void Put(ObjectInstance target, string name, JsValue value) => target.Set(name, value);
        private JsValue ParseJson(string raw) => string.IsNullOrEmpty(raw)
            ? JsValue.Null
            : engine.Invoke(engine.GetValue("__kvParse"), [raw]);
        private JsValue StatsValue() => ParseJson(KeyViewerOverlay.JsStatsJson());

        private Definition GetDefinition(string id) => definitions.FirstOrDefault(def => def.Id == id);

        private void RemoveScopedResources(string definitionId, bool mountedOnly = false) {
            int[] subIds = subscriptions.Values
                .Where(sub => sub.DefinitionId == definitionId && (!mountedOnly || sub.Mounted))
                .Select(sub => sub.Id).ToArray();
            foreach(int id in subIds) subscriptions.Remove(id);
            int[] timerIds = timers.Values
                .Where(timer => timer.DefinitionId == definitionId && (!mountedOnly || timer.Mounted))
                .Select(timer => timer.Id).ToArray();
            foreach(int id in timerIds) timers.Remove(id);
        }

        private sealed class Subscription {
            internal int Id;
            internal string DefinitionId;
            internal string EventName;
            internal JsValue Callback;
            internal bool Mounted;
        }

        private sealed class Timer {
            internal int Id;
            internal string DefinitionId;
            internal JsValue Callback;
            internal float Due;
            internal float Period;
            internal bool Repeat;
            internal bool Mounted;
        }

        internal sealed class HostBridge {
            private readonly PluginRuntime owner;
            internal HostBridge(PluginRuntime owner) => this.owner = owner;

            public void Log(string message) => MainCore.Log.Msg("[KeyViewerJS:" + owner.record.PluginId + "] " + message);
            public void Warn(string message) => MainCore.Log.Msg("[KeyViewerJS:" + owner.record.PluginId + "] WARN " + message);

            public void DefineElement(JsValue raw) {
                if(!raw.IsObject()) throw new ArgumentException("defineElement expects an object.");
                string id = owner.record.PluginId + ":" + (++owner.nextDefinition);
                owner.definitions.Add(new Definition(owner, id, raw.AsObject()));
            }

            public void RegisterCleanup(JsValue cleanup) {
                if(cleanup.IsCallable()) owner.cleanups.Add(cleanup);
            }

            public object Html(JsValue stringsValue, JsValue valuesValue) {
                JsArray strings = stringsValue.AsArray();
                JsArray values = valuesValue.AsArray();
                string[] chunks = new string[strings.Length];
                for(uint i = 0; i < chunks.Length; i++) chunks[i] = strings.Get(i).ToString();
                object[] args = new object[values.Length];
                for(uint i = 0; i < args.Length; i++) args[i] = TemplateValue(values.Get(i));
                return KvJsTemplate.Get(chunks).Instantiate(args);
            }

            private static object TemplateValue(JsValue value) {
                if(Nullish(value)) return null;
                if(value.IsArray()) {
                    JsArray array = value.AsArray();
                    object[] values = new object[array.Length];
                    for(uint i = 0; i < values.Length; i++) values[i] = TemplateValue(array.Get(i));
                    return values;
                }
                object converted = value.ToObject();
                if(converted is KvJsVNode) return converted;
                if(converted is bool flag && !flag) return null;
                return converted;
            }

            public int SetTimer(JsValue callback, double milliseconds, bool repeat) {
                if(!callback.IsCallable()) return 0;
                float period = Mathf.Max(0.001f, (float)milliseconds / 1000f);
                int id = ++owner.nextTimer;
                owner.timers[id] = new Timer {
                    Id = id,
                    DefinitionId = owner.activeDefinition,
                    Callback = callback,
                    Period = period,
                    Due = now + period,
                    Repeat = repeat,
                    Mounted = owner.activeDefinition != null,
                };
                return id;
            }

            public void ClearTimer(double id) => owner.timers.Remove((int)id);

            public int Subscribe(string definitionId, string eventName, JsValue callback) {
                if(!callback.IsCallable()) return 0;
                int id = ++owner.nextSubscription;
                string scope = string.IsNullOrEmpty(definitionId) ? owner.activeDefinition : definitionId;
                owner.subscriptions[id] = new Subscription {
                    Id = id,
                    DefinitionId = scope,
                    EventName = eventName ?? "",
                    Callback = callback,
                    Mounted = scope != null,
                };
                return id;
            }

            public void Unsubscribe(int id) => owner.subscriptions.Remove(id);

            public void SetState(string definitionId, JsValue updates) {
                Definition definition = owner.GetDefinition(definitionId);
                if(definition == null || !updates.IsObject()) return;
                definition.MergeState(updates.AsObject());
                owner.Dirty = true;
            }

            public JsValue GetSettings(string definitionId) => owner.GetDefinition(definitionId)?.Settings ?? JsValue.Null;
            public void SetAnchor(string definitionId, string anchor) {
                Definition definition = owner.GetDefinition(definitionId);
                if(definition != null) definition.Anchor = NormalizeAnchor(anchor);
            }
            public string GetAnchor(string definitionId) => owner.GetDefinition(definitionId)?.Anchor ?? "top-left";
            public void Expose(string definitionId, JsValue actions) { }

            public string Translate(string definitionId, string key, string fallback) {
                Definition definition = owner.GetDefinition(definitionId);
                return definition?.Translate(key, fallback) ?? (string.IsNullOrEmpty(fallback) ? key : fallback);
            }

            public string StorageGet(string key) => KvJsStorage.Get(Prefix + key);
            public void StorageSet(string key, string json) => KvJsStorage.Set(Prefix + key, json, now);
            public void StorageRemove(string key) => KvJsStorage.Remove(Prefix + key, now);
            public void StorageClear() => KvJsStorage.ClearByPrefix(Prefix, now);
            public string StorageKeys() => JsonConvert.SerializeObject(KvJsStorage.Keys(Prefix));
            public bool StorageHasData(string prefix) => KvJsStorage.HasPrefix(Prefix + prefix);
            public int StorageClearByPrefix(string prefix) => KvJsStorage.ClearByPrefix(Prefix + prefix, now);
            public string StatsJson() => KeyViewerOverlay.JsStatsJson();
            public void ResetStats() => KeyViewerOverlay.ResetJsStats();
            private string Prefix => owner.record.PluginId + ":";
        }

        private sealed class Definition {
            private readonly PluginRuntime owner;
            private readonly JsValue template;
            private readonly JsValue onMount;
            private readonly ObjectInstance messages;
            private JsValue mountCleanup = JsValue.Undefined;
            private GameObject visual;
            private readonly float estimatedWidth;
            private readonly float estimatedHeight;
            internal readonly string Id;
            internal readonly string Name;
            internal readonly ObjectInstance State;
            internal readonly ObjectInstance Settings;
            internal string Anchor;

            internal Definition(PluginRuntime owner, string id, ObjectInstance raw) {
                this.owner = owner;
                Id = id;
                Name = Text(raw.Get("name"), owner.record.Name.Length > 0 ? owner.record.Name : owner.record.PluginId);
                template = raw.Get("template");
                if(!template.IsCallable()) throw new ArgumentException(Name + " has no template function.");
                onMount = raw.Get("onMount");
                messages = raw.Get("messages").IsObject() ? raw.Get("messages").AsObject() : null;
                Anchor = NormalizeAnchor(Text(raw.Get("resizeAnchor"), "top-left"));
                State = owner.NewObject();
                Settings = owner.NewObject();
                Copy(raw.Get("previewState"), State);
                LoadSettings(raw.Get("settings"));
                JsValue estimated = raw.Get("estimatedSize");
                estimatedWidth = estimated.IsObject() ? Number(estimated.AsObject().Get("width"), 220f) : 220f;
                estimatedHeight = estimated.IsObject() ? Number(estimated.AsObject().Get("height"), 140f) : 140f;
            }

            internal void Mount() {
                if(!onMount.IsCallable()) return;
                JsValue context = owner.engine.Invoke(owner.engine.GetValue("__kvContext"), [Id]);
                string previous = owner.activeDefinition;
                owner.activeDefinition = Id;
                try {
                    owner.engine.Constraints.Reset();
                    JsValue result = owner.engine.Invoke(onMount, [context]);
                    if(result.IsCallable()) mountCleanup = result;
                } catch(Exception e) {
                    Report(owner.record.Name, e);
                } finally {
                    owner.activeDefinition = previous;
                }
            }

            internal void Unmount() {
                if(mountCleanup.IsCallable()) owner.InvokeScoped(Id, mountCleanup);
                mountCleanup = JsValue.Undefined;
                owner.RemoveScopedResources(Id, mountedOnly: true);
                DestroyVisual();
            }

            internal Vector2 Render(RectTransform parent, float x, float y) {
                DestroyVisual();
                try {
                    JsValue helpers = owner.engine.Invoke(owner.engine.GetValue("__kvHelpers"), [Id]);
                    owner.engine.Constraints.Reset();
                    JsValue rendered = owner.engine.Invoke(template, [State, Settings, helpers]);
                    object value = rendered.ToObject();
                    KvJsVNode vnode = value as KvJsVNode ?? KvJsVNode.NewText(value?.ToString() ?? "");
                    KvJsRenderer.Result result = KvJsRenderer.Render(
                        parent, vnode, Name, x, y,
                        Mathf.Max(20f, estimatedWidth), Mathf.Max(20f, estimatedHeight));
                    visual = result.Root;
                    return result.Size;
                } catch(Exception e) {
                    Report(owner.record.Name, e);
                    return Vector2.zero;
                }
            }

            internal void DestroyVisual() {
                if(visual != null) Object.Destroy(visual);
                visual = null;
            }

            internal void MergeState(ObjectInstance updates) {
                foreach(var property in updates.GetOwnProperties()) {
                    string key = property.Key.ToString();
                    State.Set(key, updates.Get(key));
                }
            }

            internal string Translate(string key, string fallback) {
                if(messages != null) {
                    JsValue english = messages.Get("en");
                    if(english.IsObject()) {
                        JsValue value = english.AsObject().Get(key);
                        if(!Nullish(value)) return value.ToString();
                    }
                }
                return string.IsNullOrEmpty(fallback) ? key : fallback;
            }

            private void LoadSettings(JsValue schemaValue) {
                if(schemaValue.IsObject()) {
                    ObjectInstance schema = schemaValue.AsObject();
                    foreach(var property in schema.GetOwnProperties()) {
                        string key = property.Key.ToString();
                        JsValue itemValue = schema.Get(key);
                        if(!itemValue.IsObject()) continue;
                        ObjectInstance item = itemValue.AsObject();
                        if(Text(item.Get("type"), "") == "section") continue;
                        JsValue defaultValue = item.Get("default");
                        if(!defaultValue.IsUndefined()) Settings.Set(key, defaultValue);
                    }
                }
                string saved = KvJsStorage.Get(owner.record.PluginId + ":element-settings:" + Id);
                if(string.IsNullOrEmpty(saved)) return;
                try {
                    JsValue parsed = owner.ParseJson(saved);
                    if(parsed.IsObject()) Copy(parsed, Settings);
                } catch(Exception e) {
                    Report(owner.record.Name, e);
                }
            }

            private static void Copy(JsValue source, ObjectInstance target) {
                if(!source.IsObject()) return;
                ObjectInstance obj = source.AsObject();
                foreach(var property in obj.GetOwnProperties()) {
                    string key = property.Key.ToString();
                    target.Set(key, obj.Get(key));
                }
            }

            private static string Text(JsValue value, string fallback) =>
                Nullish(value) ? fallback : value.ToString();
            private static float Number(JsValue value, float fallback) {
                if(Nullish(value)) return fallback;
                try { return (float)Convert.ToDouble(value.ToObject(), System.Globalization.CultureInfo.InvariantCulture); }
                catch(Exception) { return fallback; }
            }
        }
    }

    private static string NormalizeAnchor(string anchor) => anchor switch {
        "top-center" or "top-right" or "center-left" or "center" or "center-right"
        or "bottom-left" or "bottom-center" or "bottom-right" => anchor,
        _ => "top-left",
    };
    private static bool Nullish(JsValue value) => value.IsNull() || value.IsUndefined();
}

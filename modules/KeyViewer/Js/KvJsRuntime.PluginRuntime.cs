using Jint;
using Jint.Native;
using Jint.Native.Object;
using Quartz.Features.KeyViewer.Layout;
using UnityEngine;

namespace Quartz.Features.KeyViewer.Js;

internal static partial class KvJsRuntime {
    private sealed partial class PluginRuntime {
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
        private readonly List<int> dueScratch = [];
        private readonly List<Subscription> emitScratch = [];
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
                dueScratch.Clear();
                foreach(Timer timer in timers.Values)
                    if(time >= timer.Due) dueScratch.Add(timer.Id);
                foreach(int id in dueScratch) {
                    if(!timers.TryGetValue(id, out Timer timer)) continue;
                    if(timer.Repeat) timer.Due = time + timer.Period;
                    else timers.Remove(id);
                    InvokeScoped(timer.DefinitionId, timer.Callback);
                }
            }
            if(time >= nextStats && HasSubscription("stats")) {
                nextStats = time + 0.05f;
                JsValue payload = StatsValue();
                Emit("stats", payload);
            }
        }

        private bool HasSubscription(string eventName) {
            foreach(Subscription sub in subscriptions.Values)
                if(sub.EventName == eventName) return true;
            return false;
        }

        internal bool WantsKeyEvents => HasSubscription("key") || HasSubscription("rawKey");

        internal void EmitKey(string label, string state, string mode, string device) {
            if(HasSubscription("key")) {
                ObjectInstance key = NewObject();
                Put(key, "key", label);
                Put(key, "state", state);
                Put(key, "mode", mode);
                Emit("key", key);
            }
            if(!HasSubscription("rawKey")) return;
            ObjectInstance raw = NewObject();
            Put(raw, "device", device);
            Put(raw, "label", label);
            Put(raw, "labels", new JsArray(engine, [(JsValue)label]));
            Put(raw, "state", state);
            Emit("rawKey", raw);
        }

        internal float RenderPanels(RectTransform parent, float x, float y, ref bool changed) {
            foreach(Definition definition in definitions) {
                Vector2 size = definition.Render(parent, x, y, out bool rendered);
                changed |= rendered;
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
            int start = emitScratch.Count;
            foreach(Subscription sub in subscriptions.Values)
                if(sub.EventName == eventName) emitScratch.Add(sub);
            int count = emitScratch.Count - start;
            try {
                for(int i = start; i < start + count; i++)
                    InvokeScoped(emitScratch[i].DefinitionId, emitScratch[i].Callback, payload);
            } finally {
                emitScratch.RemoveRange(start, count);
            }
        }

        private static readonly object[] NoArgs = [];
        private readonly object[] oneArg = new object[1];

        private void InvokeScoped(string definitionId, JsValue callback) => InvokeScoped(definitionId, callback, NoArgs);

        private void InvokeScoped(string definitionId, JsValue callback, JsValue payload) {
            oneArg[0] = payload;
            try {
                InvokeScoped(definitionId, callback, oneArg);
            } finally {
                oneArg[0] = null;
            }
        }

        private void InvokeScoped(string definitionId, JsValue callback, object[] args) {
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

        private ObjectInstance NewObject() => new JsObject(engine);
        private static void Put(ObjectInstance target, string name, JsValue value) => target.Set(name, value);
        private JsValue ParseJson(string raw) => string.IsNullOrEmpty(raw)
            ? JsValue.Null
            : engine.Invoke(engine.GetValue("__kvParse"), [raw]);
        private JsValue StatsValue() {
            KeyViewerOverlay.JsStats(out int kps, out float kpsAvg, out int kpsMax, out int total);
            ObjectInstance stats = NewObject();
            Put(stats, "kps", kps);
            Put(stats, "kpsAvg", (double)kpsAvg);
            Put(stats, "kpsMax", kpsMax);
            Put(stats, "total", total);
            return stats;
        }

        private Definition GetDefinition(string id) {
            for(int i = 0; i < definitions.Count; i++)
                if(definitions[i].Id == id) return definitions[i];
            return null;
        }

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
    }
}

using Jint;
using Jint.Native;
using Newtonsoft.Json;
using Quartz.Core;
using Quartz.Features.KeyViewer.Layout;
using UnityEngine;

namespace Quartz.Features.KeyViewer.Js;

internal static partial class KvJsRuntime {
    private sealed partial class PluginRuntime {
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
    }
}

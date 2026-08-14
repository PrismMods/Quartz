using Jint;
using Jint.Native;
using Jint.Native.Object;
using Quartz.Core;
using Quartz.Features.KeyViewer.Layout;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Quartz.Features.KeyViewer.Js;

internal static partial class KvJsRuntime {
    private sealed partial class PluginRuntime {
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
                catch(Exception e) {
                    Diag.Ignore(e);
                    return fallback;
                }
            }
        }
    }
}

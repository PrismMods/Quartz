#if !IL2CPP
using System.Globalization;
using System.Reflection;
using Facet;
using Quartz.Addons;
using Quartz.Async;
using UnityEngine;
using UnityEngine.UIElements;
namespace Quartz.UI.FacetUi.CoreMenuPages;
/// <summary>
/// An addon settings tab in the new menu, row for row as AddonSettingsUI: its members in classic's order, each a Form field
/// whose Id is classic's key. Sliders and colours write every frame and save on release; a [ReloadRequired] member reloads
/// the addons once per commit, through Do. Every row's undo is bound to the tab's record, so an undo after the addon unloaded
/// or reloaded writes nothing.
/// </summary>
internal static class AddonForm {
    internal static void Build(VisualElement p, AddonSettingsTabs.Tab tab) {
        VisualElement card = Controls.Card(p, null), into = card;
        var run = new List<Field>();
        void Flush() {
            if(run.Count == 0) return;
            Form.Build(into, run);
            run.Clear();
        }
        foreach(MemberInfo m in Members(tab.Data.GetType())) {
            try {
                string id = "addon_" + tab.AddonId + "_" + m.Name;
                if(m.GetCustomAttribute<SectionAttribute>() is { } section) {
                    Flush();
                    string sectionKey = Quartz.Localization.LocaleKeys.FromId(id + "_section");
                    into = ShellRows.Section(card, QuartzShell.Label(sectionKey, section.Title), o => {
                        o.Id = sectionKey;
                        o.Open = true;
                    });
                }
                Type type = TypeOf(m);
                bool slider = m.GetCustomAttribute<SliderAttribute>() != null;
                bool choices = m.GetCustomAttribute<ChoicesAttribute>() is { Values.Length: > 0 };
                AddonFieldKind kind = AddonFieldRules.Kind(type, slider, choices);
                if(kind == AddonFieldKind.Unsupported) {
                    // Only on a shown build: the search index builds this page detached on every rebuild.
                    if(p.panel != null) tab.Warn($"settings member '{m.Name}': type {type.Name} has no auto UI — add [Ignore] or build it with RegisterTab");
                    continue;
                }
                if(kind is AddonFieldKind.Dict or AddonFieldKind.Pairs) {
                    Flush();
                    Pairs(into, tab, m, id, kind);
                    continue;
                }
                run.Add(FieldOf(into, tab, m, id, kind));
            } catch(Exception e) {
                tab.Warn($"settings UI for '{m.Name}' failed: {e}");
            }
        }
        Flush();
    }

    // Classic's members: public instance fields, then read-write non-indexed properties, minus [Ignore], in metadata order.
    private static IEnumerable<MemberInfo> Members(Type type) =>
        type.GetFields(BindingFlags.Public | BindingFlags.Instance).Cast<MemberInfo>()
            .Concat(type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.CanWrite && p.GetIndexParameters().Length == 0))
            .Where(m => m.GetCustomAttribute<IgnoreAttribute>() == null)
            .OrderBy(m => m.MetadataToken);

    private static Type TypeOf(MemberInfo m) => m is FieldInfo f ? f.FieldType : ((PropertyInfo)m).PropertyType;
    private static object Get(MemberInfo m, object o) => m is FieldInfo f ? f.GetValue(o) : ((PropertyInfo)m).GetValue(o);
    private static void Set(MemberInfo m, object o, object v) {
        if(m is FieldInfo f) f.SetValue(o, v);
        else ((PropertyInfo)m).SetValue(o, v);
    }
    private static string NumText(object v) => v == null ? "" : Convert.ToString(v, CultureInfo.InvariantCulture);

    // Saves, then with [ReloadRequired] reloads every addon on the next pump, as classic's Commit does.
    private static Action Commit(AddonSettingsTabs.Tab tab, MemberInfo m) {
        bool reload = m.GetCustomAttribute<ReloadRequiredAttribute>() != null;
        return () => {
            tab.Save();
            if(reload) QuartzShell.Do("addons.reload", () => MainThread.Enqueue(AddonService.Reload));
        };
    }

    private static Field FieldOf(VisualElement into, AddonSettingsTabs.Tab tab, MemberInfo m, string id, AddonFieldKind kind) {
        object data = tab.Data, defaults = tab.Defaults;
        Type type = TypeOf(m);
        string key = Quartz.Localization.LocaleKeys.FromId(id), desc = m.GetCustomAttribute<DescAttribute>()?.Text;
        Action commit = Commit(tab, m);
        var f = new Field {
            Id = key,
            Label = QuartzShell.Label(key, AddonFieldRules.Label(m.Name, m.GetCustomAttribute<NameAttribute>()?.Label)),
            Tooltip = desc == null ? null : QuartzShell.Label("DESC_" + key, desc),
            Committed = commit,
            UndoDocument = () => AddonSettingsTabs.TryGet(tab.Key, out AddonSettingsTabs.Tab now) ? now : null,
        };
        switch(kind) {
            case AddonFieldKind.Toggle:
                f.Kind = FieldKind.Bool;
                f.Get = () => Get(m, data);
                f.Set = v => Set(m, data, Convert.ToBoolean(v));
                f.Default = Get(m, defaults);
                break;
            case AddonFieldKind.Enum:
            case AddonFieldKind.Choices:
                IList<object> values = kind == AddonFieldKind.Enum
                    ? [.. Enum.GetValues(type).Cast<object>()]
                    : [.. m.GetCustomAttribute<ChoicesAttribute>().Values];
                f.Kind = FieldKind.Dropdown;
                f.Options = [.. values.Select(v => kind == AddonFieldKind.Enum ? AddonFieldRules.Label(v.ToString(), null) : (string)v)];
                f.Get = () => values.IndexOf(Get(m, data));
                f.Set = v => Set(m, data, values[Convert.ToInt32(v)]);
                f.Default = values.IndexOf(Get(m, defaults));
                break;
            case AddonFieldKind.IntSlider:
            case AddonFieldKind.Slider:
                SliderAttribute s = m.GetCustomAttribute<SliderAttribute>();
                f.Kind = kind == AddonFieldKind.IntSlider ? FieldKind.Int : FieldKind.Float;
                (f.Min, f.Max, f.Step) = (s.Min, s.Max, s.Step);
                f.Get = () => Get(m, data);
                f.Set = v => Set(m, data, Convert.ChangeType(v, type, CultureInfo.InvariantCulture));
                f.Default = Get(m, defaults);
                break;
            case AddonFieldKind.Number:
            case AddonFieldKind.NullableNumber:
                // Typed, parsed on commit as classic's input: bad text writes nothing and the field shows the value again.
                bool parsed = true;
                f.Kind = FieldKind.Text;
                f.Get = () => NumText(Get(m, data));
                f.Set = v => {
                    parsed = AddonFieldRules.ParseNumber(v as string, type, out object n);
                    if(parsed) Set(m, data, n);
                };
                f.Committed = () => {
                    if(parsed) commit();
                    else if(into.Q<TextField>(key + "-input") is { } field) field.SetValueWithoutNotify(NumText(Get(m, data)));
                };
                f.Default = NumText(Get(m, defaults));
                break;
            case AddonFieldKind.Text:
                f.Kind = FieldKind.Text;
                f.Get = () => Get(m, data) ?? "";
                f.Set = v => Set(m, data, v as string);
                f.Default = Get(m, defaults);
                break;
            case AddonFieldKind.Colour:
                // Written on every drag frame, saved on release, as classic's picker commits.
                f.Kind = FieldKind.Colour;
                f.Get = () => Get(m, data);
                f.Set = v => Set(m, data, (Color)v);
                f.Default = Get(m, defaults);
                break;
        }
        return f;
    }

    // A string map or pair list: a label row, then a key row and a value row per pair (× removes it), then Add Row. Every edit
    // commits the whole collection, as classic's DictRows does.
    private static void Pairs(VisualElement into, AddonSettingsTabs.Tab tab, MemberInfo m, string id, AddonFieldKind kind) {
        object data = tab.Data;
        string key = Quartz.Localization.LocaleKeys.FromId(id);
        Label label = Ui.Muted(into, QuartzShell.Label(key, AddonFieldRules.Label(m.Name, m.GetCustomAttribute<NameAttribute>()?.Label)));
        label.name = key;
        // The collection the copy was taken from: when the addon or the classic menu puts a new one in the member, the copy is
        // taken again, so an edit here never writes an old copy over it. A change made inside the same collection is not seen.
        object source = Get(m, data);
        var pairs = new List<KeyValuePair<string, string>>((IEnumerable<KeyValuePair<string, string>>)source ?? []);
        int version = 0, keyed = -1;
        string[] keys = [];
        Action commit = Commit(tab, m);
        void Write() {
            if(kind == AddonFieldKind.Dict) {
                Dictionary<string, string> next = [];
                foreach(KeyValuePair<string, string> pair in pairs) next[pair.Key] = pair.Value;
                Set(m, data, source = next);
            } else Set(m, data, source = new List<KeyValuePair<string, string>>(pairs));
            commit();
        }
        void Reshape() {
            version++;
            QuartzShell.Surface?.Refresh();
        }
        // Before each refresh and each edit: an edit made before a refresh pass has shown the new collection lands on it.
        void Sync() {
            object now = Get(m, data);
            if(ReferenceEquals(now, source)) return;
            source = now;
            pairs.Clear();
            if(now is IEnumerable<KeyValuePair<string, string>> all) pairs.AddRange(all);
            version++;
        }
        // Keyed by place and shape: an add or remove rebuilds every pair's rows, so each shows its own pair.
        IReadOnlyList<string> Items() {
            Sync();
            if(keyed != version || keys.Length != pairs.Count) {
                keys = [.. Enumerable.Range(0, pairs.Count).Select(i => version + ":" + i)];
                keyed = version;
            }
            return keys;
        }
        Controls.Repeat<string>(into, Items, k => k, (box, k) => {
            int i = int.Parse(k[(k.IndexOf(':') + 1)..], CultureInfo.InvariantCulture), n = i + 1;
            Controls.Text(box, "", Bind.To(() => i < pairs.Count ? pairs[i].Key : "", v => {
                Sync();
                if(i >= pairs.Count) return;
                pairs[i] = new(v ?? "", pairs[i].Value);
                Write();
            }), o => {
                o.Id = Quartz.Localization.LocaleKeys.FromId(id + "_key") + "#" + n;
                o.Placeholder = "Key";
                o.Undoable = false;
            });
            Control<string> value = Controls.Text(box, "", Bind.To(() => i < pairs.Count ? pairs[i].Value : "", v => {
                Sync();
                if(i >= pairs.Count) return;
                pairs[i] = new(pairs[i].Key, v ?? "");
                Write();
            }), o => {
                o.Id = Quartz.Localization.LocaleKeys.FromId(id + "_value") + "#" + n;
                o.Placeholder = "Value";
                o.Undoable = false;
            });
            Button remove = Ui.Btn(value.Input.parent, "DELETE_BUTTON", () => {
                Sync();
                if(i >= pairs.Count) return;
                pairs.RemoveAt(i);
                Write();
                Reshape();
            });
            remove.name = Quartz.Localization.LocaleKeys.FromId(id + "_remove") + "#" + n + "-input";
            remove.style.marginLeft = Theme.For(remove).Sizes.Gap;
        });
        string add = Quartz.Localization.LocaleKeys.FromId(id + "_add");
        Controls.Action(into, "", QuartzShell.Label(add, "Add Row"), () => {
            Sync();
            pairs.Add(new("", ""));
            Write();
            Reshape();
        }, o => o.Id = add);
    }
}
#endif

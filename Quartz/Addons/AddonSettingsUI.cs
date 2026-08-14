using Quartz.Async;
using Quartz.Core;
using Quartz.UI.Generator;
using Quartz.UI.Objects.Impl;
using System.Globalization;
using System.Reflection;
using System.Text;
using UnityEngine;
namespace Quartz.Addons;
internal static class AddonSettingsUI {
    internal static void Build(Transform content, AddonContext ctx, object data, object defaults, Action save) {
        foreach(MemberInfo member in Members(data.GetType())) {
            try {
                BuildMember(content, ctx, data, defaults, save, member);
            } catch(Exception e) {
                ctx.Err($"settings UI for '{member.Name}' failed: {e}");
            }
        }
    }
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
    private static void BuildMember(
        Transform content,
        AddonContext ctx,
        object data,
        object defaults,
        Action save,
        MemberInfo member
    ) {
        string id = $"addon_{ctx.Id}_{member.Name}";
        SectionAttribute section = member.GetCustomAttribute<SectionAttribute>();
        if(section != null) GenerateUI.Header(content, section.Title, id + "_section");
        Type type = TypeOf(member);
        string label = member.GetCustomAttribute<NameAttribute>()?.Label ?? SplitPascal(member.Name);
        string desc = member.GetCustomAttribute<DescAttribute>()?.Text;
        bool reload = member.GetCustomAttribute<ReloadRequiredAttribute>() != null;
        void Commit(object value) {
            Set(member, data, value);
            save();
            if(reload) MainThread.Enqueue(AddonService.Reload);
        }
        object defVal = Get(member, defaults);
        object val = Get(member, data);
        RectTransform tipTarget = null;
        if(type == typeof(bool)) {
            UIToggle toggle = GenerateUI.Toggle(
                GenerateUI.Row(content), (bool)defVal, (bool)val, v => Commit(v), label, id
            );
            tipTarget = toggle.Rect;
        } else if(type.IsEnum) {
            List<object> values = Enum.GetValues(type).Cast<object>().ToList();
            UIDropDown<object> dd = GenerateUI.DropDown<object>(
                GenerateUI.Row(content), defVal, val, values,
                o => SplitPascal(o.ToString()), o => Commit(o), id, 260f, label
            );
            tipTarget = dd.Rect;
        } else if(type == typeof(string) && member.GetCustomAttribute<ChoicesAttribute>() is { Values.Length: > 0 } choices) {
            UIDropDown<string> dd = GenerateUI.DropDown<string>(
                GenerateUI.Row(content), (string)defVal, (string)val, choices.Values,
                s => s, s => Commit(s), id, 260f, label
            );
            tipTarget = dd.Rect;
        } else if(IsNumeric(type) && member.GetCustomAttribute<SliderAttribute>() is { } slider) {
            bool integral = type == typeof(int);
            float step = slider.Step;
            Func<float, float> filter = integral
                ? Mathf.Round
                : step > 0f ? v => Mathf.Round(v / step) * step : null;
            UISlider ui = GenerateUI.Slider(
                GenerateUI.Row(content),
                Convert.ToSingle(defVal), slider.Min, slider.Max, Convert.ToSingle(val),
                filter,
                v => Set(member, data, Convert.ChangeType(v, type)),
                v => Commit(Convert.ChangeType(v, type)),
                label, id
            );
            tipTarget = ui.Rect;
        } else if(IsNumeric(type)) {
            UIInput input = null;
            input = GenerateUI.Input(
                GenerateUI.Row(content),
                Convert.ToString(defVal, CultureInfo.InvariantCulture),
                Convert.ToString(val, CultureInfo.InvariantCulture),
                null, label, null, id
            );
            input.OnComplete = text => {
                if(double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed)) {
                    Commit(Convert.ChangeType(parsed, type));
                } else {
                    input.Set(Convert.ToString(Get(member, data), CultureInfo.InvariantCulture), invoke: false);
                }
            };
            tipTarget = input.Rect;
        } else if(type == typeof(string)) {
            UIInput input = GenerateUI.Input(
                GenerateUI.Row(content), (string)defVal, (string)val,
                v => Set(member, data, v), label, null, id
            );
            input.OnComplete = _ => Commit(Get(member, data));
            tipTarget = input.Rect;
        } else if(type == typeof(Color)) {
            UIColorPicker picker = GenerateUI.ColorPicker(
                GenerateUI.Row(content), (Color)defVal, (Color)val,
                c => Set(member, data, c), c => Commit(c), label, id
            );
            tipTarget = picker.Rect;
        } else if(type == typeof(Dictionary<string, string>)) {
            LabelRow(content, label, id);
            UIDictRows dict = GenerateUI.DictRows(
                content,
                (Dictionary<string, string>)val,
                pairs => {
                    Dictionary<string, string> next = [];
                    foreach(KeyValuePair<string, string> pair in pairs) next[pair.Key] = pair.Value;
                    Commit(next);
                },
                id
            );
            tipTarget = dict.Rect;
        } else if(type == typeof(List<KeyValuePair<string, string>>)) {
            LabelRow(content, label, id);
            UIDictRows dict = GenerateUI.DictRows(
                content,
                (List<KeyValuePair<string, string>>)val,
                pairs => Commit(new List<KeyValuePair<string, string>>(pairs)),
                id
            );
            tipTarget = dict.Rect;
        } else {
            ctx.Wrn($"settings member '{member.Name}': type {type.Name} has no auto UI — add [Ignore] or build it with RegisterTab");
            return;
        }
        if(desc != null && tipTarget != null) tipTarget.AddToolTip("DESC_" + GenerateUI.LocaleKeyFromId(id), desc);
    }
    private static void LabelRow(Transform content, string label, string id) {
        var text = GenerateUI.AddMutedText(GenerateUI.Row(content, 34f), 18f, 0.6f);
        text.text = label;
        GenerateUI.LocalizeById(text, id, label);
    }
    private static bool IsNumeric(Type type) => type == typeof(int) || type == typeof(float) || type == typeof(double);
    private static string SplitPascal(string name) {
        StringBuilder sb = new(name.Length + 8);
        for(int i = 0; i < name.Length; i++) {
            char c = name[i];
            if(c == '_') {
                sb.Append(' ');
                continue;
            }
            if(i > 0 && char.IsUpper(c) && (char.IsLower(name[i - 1]) || char.IsDigit(name[i - 1])))
                sb.Append(' ');
            sb.Append(c);
        }
        return sb.ToString();
    }
}

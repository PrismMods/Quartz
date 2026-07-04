using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Xml;
using System.Xml.Linq;
using Quartz.Core;
using Quartz.Features.UiHider;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Quartz.Features.Interop;

// Generic reflection/XML/JSON reading helpers shared across the per-source-mod
// readers in Quartz/Features/Interop/Readers/. Nothing here is specific to any
// one source mod's schema.
public static class ReflectionHelpers {
    private const BindingFlags AllMembers =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    // ===== reflection helpers =====

    public static object GetStaticMember(Type type, string name) {
        if(type == null || string.IsNullOrEmpty(name)) return null;
        FieldInfo field = type.GetField(name, AllMembers);
        if(field != null) return field.GetValue(null);
        PropertyInfo prop = type.GetProperty(name, AllMembers);
        return prop?.GetValue(null, null);
    }

    public static object GetMemberValue(object obj, string name) {
        if(obj == null || string.IsNullOrEmpty(name)) return null;
        Type type = obj as Type ?? obj.GetType();
        object instance = obj is Type ? null : obj;
        FieldInfo field = type.GetField(name, AllMembers);
        if(field != null) return field.GetValue(instance);
        PropertyInfo prop = type.GetProperty(name, AllMembers);
        return prop?.GetValue(instance, null);
    }

    public static object ReadMember(object target, string name) {
        if(target == null) return null;
        try {
            Type t = target.GetType();
            FieldInfo f = t.GetField(name, BindingFlags.Public | BindingFlags.Instance);
            if(f != null) return f.GetValue(target);
            PropertyInfo p = t.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            return p?.GetValue(target);
        } catch {
            return null;
        }
    }

    public static object ReadNested(object target, string first, string second) =>
        ReadMember(ReadMember(target, first), second);

    public static bool TryGetBool(object obj, string name, out bool value) => TryConvertBool(GetMemberValue(obj, name), out value);

    public static bool TryConvertBool(object obj, out bool value) {
        value = false;
        switch(obj) {
            case null:
                return false;
            case bool b:
                value = b;
                return true;
            case JValue jv when jv.Type == JTokenType.Boolean:
                value = jv.Value<bool>();
                return true;
        }
        string text = obj.ToString();
        if(bool.TryParse(text, out value)) return true;
        if(int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int i)) {
            value = i != 0;
            return true;
        }
        return false;
    }

    public static bool TryGetInt(object obj, string name, out int value) => TryConvertInt(GetMemberValue(obj, name), out value);

    public static bool TryConvertInt(object raw, out int value) {
        value = 0;
        if(raw == null) return false;
        try {
            value = Convert.ToInt32(raw is JValue jv ? jv.Value : raw, CultureInfo.InvariantCulture);
            return true;
        } catch {
            return int.TryParse(raw.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }
    }

    public static bool TryGetFloat(object obj, string name, out float value) => TryConvertFloat(GetMemberValue(obj, name), out value);

    public static bool TryConvertFloat(object raw, out float value) {
        value = 0f;
        if(raw == null) return false;
        try {
            value = Convert.ToSingle(raw is JValue jv ? jv.Value : raw, CultureInfo.InvariantCulture);
            return true;
        } catch {
            return float.TryParse(raw.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }
    }

    public static bool TryGetColor(object value, out Color color) {
        color = Color.white;
        if(value == null) return false;
        if(value is Color c) {
            color = c;
            return true;
        }
        if(TryGetFloat(value, "r", out float r) && TryGetFloat(value, "g", out float g) && TryGetFloat(value, "b", out float b)) {
            float a = TryGetFloat(value, "a", out float aa) ? aa : 1f;
            color = new Color(Mathf.Clamp01(r), Mathf.Clamp01(g), Mathf.Clamp01(b), Mathf.Clamp01(a));
            return true;
        }
        return false;
    }

    // The source mods store stat colors as a "ColorRange": a list of
    // progress→color points plus a PerfectColor. v2 takes flat colors, so pull
    // the lowest- and highest-progress points as the low/high endpoints.
    public static bool TryGetColorRangeEndpoints(object value, out Color low, out Color high) {
        low = high = Color.white;
        if(value == null) return false;

        List<(float progress, Color color)> points = [];
        if(GetMemberValue(value, "List") is IEnumerable list) {
            foreach(object item in list) {
                if(TryGetFloat(item, "Progress", out float p) && TryGetColor(item, out Color c)) {
                    points.Add((Mathf.Clamp01(p), c));
                }
            }
        }

        if(points.Count > 0) {
            points.Sort((x, y) => x.progress.CompareTo(y.progress));
            low = points[0].color;
            high = points[^1].color;
            return true;
        }

        if(TryGetColor(GetMemberValue(value, "PerfectColor"), out Color perfect)) {
            low = high = perfect;
            return true;
        }
        return false;
    }

    // ===== key/array readers =====

    public static int[] ReadKeyCodesFromMember(object obj, string member) => ReadKeyCodeEnumerable(GetMemberValue(obj, member));

    public static int[] ReadKeyCodeEnumerable(object value) {
        if(value is not IEnumerable enumerable || value is string) return [];
        List<int> result = [];
        foreach(object item in enumerable) {
            if(TryConvertKeyCode(item, out int key) && !result.Contains(key)) {
                result.Add(key);
            }
        }
        return [.. result];
    }

    public static int[] ReadKeyCodesFromJson(JToken token) {
        if(token is not JArray arr) return [];
        List<int> result = [];
        foreach(JToken t in arr) {
            if(TryConvertKeyCode(t is JValue jv ? jv.Value : t, out int key) && !result.Contains(key)) {
                result.Add(key);
            }
        }
        return [.. result];
    }

    public static bool TryConvertKeyCode(object value, out int key) {
        key = 0;
        if(value == null) return false;
        if(value is KeyCode kc) {
            key = NormalizeKeyInt((int)kc);
            return true;
        }
        if(value.GetType().IsEnum) {
            try { key = NormalizeKeyInt(Convert.ToInt32(value, CultureInfo.InvariantCulture)); return true; } catch { return false; }
        }
        if(value is IConvertible and not string) {
            try { key = NormalizeKeyInt(Convert.ToInt32(value, CultureInfo.InvariantCulture)); return true; } catch { }
        }
        string text = value.ToString();
        if(int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) {
            key = NormalizeKeyInt(parsed);
            return true;
        }
        if(Enum.TryParse(text, true, out KeyCode named)) {
            key = NormalizeKeyInt((int)named);
            return true;
        }
        return false;
    }

    public static int NormalizeKeyInt(int raw) => (int)Features.KeyLimiter.KeyLimiter.NormalizeKey((KeyCode)raw);

    public static string[] ReadStringArray(object value) {
        if(value is not IEnumerable enumerable || value is string) return null;
        List<string> result = [];
        foreach(object item in enumerable) {
            result.Add(item?.ToString() ?? "");
        }
        return result.Count > 0 ? [.. result] : null;
    }

    public static string[] ReadStringArrayJson(JToken token) {
        if(token is not JArray arr || arr.Count == 0) return null;
        string[] result = new string[arr.Count];
        for(int i = 0; i < arr.Count; i++) {
            result[i] = arr[i].Type == JTokenType.String ? arr[i].ToString() : "";
        }
        return result;
    }

    public static Color? ReadJsonColor(JToken token) {
        if(token is not JObject obj) return null;
        if(TryConvertFloat(JsonValue(obj, "r"), out float r)
            && TryConvertFloat(JsonValue(obj, "g"), out float g)
            && TryConvertFloat(JsonValue(obj, "b"), out float b)) {
            float a = TryConvertFloat(JsonValue(obj, "a"), out float aa) ? aa : 1f;
            return new Color(Mathf.Clamp01(r), Mathf.Clamp01(g), Mathf.Clamp01(b), Mathf.Clamp01(a));
        }
        return null;
    }

    public static object JsonValue(JObject obj, string name) =>
        obj != null && obj.TryGetValue(name, StringComparison.OrdinalIgnoreCase, out JToken t) ? t : null;

    public static bool[] ReadBoolArray(object value) {
        if(value is not IEnumerable enumerable || value is string) return [];
        List<bool> values = [];
        foreach(object item in enumerable) {
            if(TryConvertBool(item, out bool b)) values.Add(b);
        }
        return [.. values];
    }

    // ===== XML / file helpers =====

    public static XDocument LoadXml(SettingsImportOption option, string fileName) {
        if(string.IsNullOrEmpty(option.Directory)) return null;
        string path = Path.Combine(option.Directory, fileName);
        if(!File.Exists(path)) return null;
        try {
            using XmlReader reader = XmlReader.Create(path, new XmlReaderSettings {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
            });
            return XDocument.Load(reader);
        } catch { return null; }
    }

    public static int[] ReadKeyCodesFromXml(XElement parent, string listName) {
        if(parent == null) return [];
        XElement list = FindFirstDescendant(parent, listName);
        if(list == null) return [];
        List<int> result = [];
        foreach(XElement item in list.Elements()) {
            if(TryConvertKeyCode(item.Value, out int key) && !result.Contains(key)) {
                result.Add(key);
            }
        }
        return [.. result];
    }

    public static bool[] ReadXmlBoolArray(XDocument doc, string arrayName) {
        XElement parent = FindFirstDescendant(doc, arrayName);
        if(parent == null) return [];
        List<bool> values = [];
        foreach(XElement item in parent.Elements()) {
            if(TryParseBool(item.Value, out bool b)) values.Add(b);
        }
        return [.. values];
    }

    public static bool TryReadXmlBool(XContainer root, string name, out bool value) {
        value = false;
        XElement element = FindFirstDescendant(root, name);
        return element != null && TryParseBool(element.Value, out value);
    }

    public static bool TryReadXmlInt(XContainer root, string name, out int value) {
        value = 0;
        XElement element = FindFirstDescendant(root, name);
        return element != null && int.TryParse(element.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    public static bool TryReadXmlKeyCode(XContainer root, string name, out int value) {
        value = 0;
        XElement element = FindFirstDescendant(root, name);
        if(element == null) return false;
        if(int.TryParse(element.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out value)) return true;
        try { value = (int)(KeyCode)Enum.Parse(typeof(KeyCode), element.Value, true); return true; } catch { return false; }
    }

    public static bool TryParseBool(string text, out bool value) {
        value = false;
        if(bool.TryParse(text, out value)) return true;
        if(int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int i)) {
            value = i != 0;
            return true;
        }
        return false;
    }

    public static XElement FindFirstDescendant(XContainer root, string name) {
        if(root == null || string.IsNullOrEmpty(name)) return null;
        return root.Descendants().FirstOrDefault(e => e.Name.LocalName == name);
    }

    public static XElement FindSelectedProfileElement(XDocument doc, string profileName) {
        if(doc == null) return null;
        XElement first = null;
        foreach(XElement profile in doc.Descendants().Where(e => e.Name.LocalName == profileName)) {
            first ??= profile;
            if(TryReadXmlBool(profile, "isSelected", out bool selected) && selected) return profile;
        }
        return first;
    }

    public static string ReadFirstText(IEnumerable<string> paths) {
        foreach(string path in paths) {
            try {
                if(!string.IsNullOrEmpty(path) && File.Exists(path)) return File.ReadAllText(path);
            } catch { }
        }
        return null;
    }

    // ===== UI-hiding profile copy (shared by AdofaiTweaks + KorenResourcePackV1) =====

    public static int ApplyAdofaiHideUiProfile(object profile, UiHiderProfile target) =>
        profile == null ? 0 : ApplyHideUiProfile(name => TryGetBool(profile, name, out bool v) ? v : null, target);

    public static int ApplyAdofaiHideUiProfileXml(XElement profile, UiHiderProfile target) =>
        profile == null ? 0 : ApplyHideUiProfile(name => TryReadXmlBool(profile, name, out bool v) ? v : null, target);

    private static int ApplyHideUiProfile(Func<string, bool?> read, UiHiderProfile target) {
        if(target == null) return 0;
        int count = 0;
        void Flag(string name, Action<bool> set) {
            if(read(name) is { } v) { set(v); count++; }
        }
        Flag("HideEverything", v => target.HideEverything = v);
        Flag("HideJudgment", v => target.HideJudgment = v);
        Flag("HideMissIndicators", v => target.HideMissIndicators = v);
        Flag("HideTitle", v => target.HideTitle = v);
        Flag("HideOtto", v => target.HideOtto = v);
        Flag("HideTimingTarget", v => target.HideTimingTarget = v);
        Flag("HideNoFailIcon", v => target.HideNoFailIcon = v);
        Flag("HideBeta", v => target.HideBeta = v);
        Flag("HideResult", v => target.HideResult = v);
        Flag("HideHitErrorMeter", v => target.HideHitErrorMeter = v);
        Flag("HideLastFloorFlash", v => target.HideLastFloorFlash = v);
        return count;
    }

    public static void ApplyShortcutModifier(object shortcut) =>
        SetShortcutModifier(name => TryGetBool(shortcut, name, out bool v) && v);

    public static void ApplyShortcutModifierXml(XElement shortcut) =>
        SetShortcutModifier(name => TryReadXmlBool(shortcut, name, out bool v) && v);

    private static void SetShortcutModifier(Func<string, bool> pressed) {
        Features.UiHider.UiHider.Conf.ShortcutModifier = (int)(
            pressed("PressCtrl") ? Keybind.KeyModifier.Ctrl
            : pressed("PressAlt") ? Keybind.KeyModifier.Alt
            : pressed("PressShift") ? Keybind.KeyModifier.Shift
            : Keybind.KeyModifier.None
        );
    }
}

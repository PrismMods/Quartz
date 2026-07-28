using UnityEngine;
using Quartz.Core;
namespace Quartz.Interop;
public static class ImportSourceKind {
    public const string KeyboardChatterBlocker = "KeyboardChatterBlocker";
    public const string JipperKeyViewer = "JipperKeyViewer";
    public const string JipperResourcePack = "JipperResourcePack";
    public const string AdofaiTweaks = "AdofaiTweaks";
    public const string EnhancedEffectRemover = "EnhancedEffectRemover";
    public const string KorenResourcePackV1 = "KorenResourcePackV1";
}
public sealed class ImportSource(
    string kind,
    Func<string, object> scalar,
    Func<string, int[]> keys = null,
    Func<string, string[]> labels = null
) {
    public string Kind { get; } = kind ?? "";
    private readonly Dictionary<string, object> extras = new(StringComparer.Ordinal);
    public object Scalar(string name) {
        try {
            return scalar?.Invoke(name);
        } catch(Exception e) {
            Diag.Ignore(e);
            return null;
        }
    }
    public int[] Keys(string name) {
        try {
            return keys?.Invoke(name);
        } catch(Exception e) {
            Diag.Ignore(e);
            return null;
        }
    }
    public string[] Labels(string name) {
        try {
            return labels?.Invoke(name);
        } catch(Exception e) {
            Diag.Ignore(e);
            return null;
        }
    }
    public ImportSource Put(string name, object value) {
        if(!string.IsNullOrEmpty(name)) extras[name] = value;
        return this;
    }
    public bool TryExtra<T>(string name, out T value) {
        value = default;
        if(name == null || !extras.TryGetValue(name, out object raw) || raw is not T typed) return false;
        value = typed;
        return true;
    }
    public bool TryBool(string name, out bool value) =>
        Quartz.Features.Interop.ReflectionHelpers.TryConvertBool(Scalar(name), out value);
    public bool TryInt(string name, out int value) =>
        Quartz.Features.Interop.ReflectionHelpers.TryConvertInt(Scalar(name), out value);
    public bool TryFloat(string name, out float value) =>
        Quartz.Features.Interop.ReflectionHelpers.TryConvertFloat(Scalar(name), out value);
    public Color? Color(string prefix) {
        if(!TryFloat(prefix + "R", out float r) || !TryFloat(prefix + "G", out float g) || !TryFloat(prefix + "B", out float b))
            return null;
        float a = TryFloat(prefix + "A", out float alpha) ? alpha : 1f;
        return new Color(Mathf.Clamp01(r), Mathf.Clamp01(g), Mathf.Clamp01(b), Mathf.Clamp01(a));
    }
}

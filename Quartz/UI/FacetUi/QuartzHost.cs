#if !IL2CPP
using Facet;
using Quartz.Core;
using Quartz.Localization;
using UnityEngine;
namespace Quartz.UI.FacetUi;
/// <summary>
/// Quartz's Facet host. Names Facet types, so only code in this folder may touch it
/// (everything else goes through <see cref="FacetBridge"/>). Created on first use.
/// </summary>
internal static class QuartzHost {
    private static Host host;
    private static bool hooked;
    private static readonly List<Surface> surfaces = [];
    internal static Host Host {
        get {
            host ??= Host.Create("Quartz", o => {
                o.Log = m => MainCore.Log.Msg(m);
                o.Translate = Translate;
                o.Language = () => LanguageCode(MainCore.Tr.Language);
                o.Read = key => MainCore.Conf.FacetWindows.TryGetValue(key, out string v) ? v : null;
                o.Write = Write;
                o.Theme = Theme.FromAccent(MainCore.Conf.GetAccentColor());
            });
            if(!hooked) {
                hooked = true;
                MainCore.Tr.OnLanguageChanged += OnLanguageChanged;
            }
            return host;
        }
    }
    // Labels are Lang keys; the DEFAULT language answers every Get with its fallback, so that is the English text.
    // Facet's faces draw Latin, plus Hangul only when the host says "ko": any other language would draw boxes, so it reads English.
    internal static string Translate(string s) {
        string english = MainCore.Tr.GetForLanguage(s, "en-US", s);
        return LanguageCode(MainCore.Tr.Language) is "en" or "ko" ? MainCore.Tr.Get(s, english) : english;
    }
    /// <summary>Re-tints every Quartz window. Before the host exists there is nothing to tint: it starts from the accent.</summary>
    internal static void Recolour(Color accent) => host?.Theme.Recolour(accent);
    /// <summary>A surface on this host that is relabelled when Quartz's language changes and disposed on shutdown.</summary>
    internal static Surface NewSurface(string name, Layer layer) {
        var surface = new Surface(Host, name, layer);
        surfaces.Add(surface);
        return surface;
    }
    // Facet's Korean font fallback fires only on exactly "ko"; Quartz says "ko-KR", "en-US" or "DEFAULT".
    internal static string LanguageCode(string language) {
        if(string.IsNullOrEmpty(language) || language.Equals(Translator.FALLBACK_LANGUAGE, StringComparison.OrdinalIgnoreCase)) return "en";
        int cut = language.IndexOfAny(['-', '_']);
        return (cut > 0 ? language[..cut] : language).ToLowerInvariant();
    }
    // Writes arrive every frame of a window drag; only a changed value asks for a save, and
    // SettingsFile.RequestSave debounces those (the write lands 500 ms after the last change).
    internal static void Write(string key, string value) {
        var map = MainCore.Conf.FacetWindows;
        if(value == null) {
            if(!map.Remove(key)) return;
        } else {
            if(map.TryGetValue(key, out string old) && old == value) return;
            map[key] = value;
        }
        MainCore.ConfMgr.RequestSave();
    }
    private static void OnLanguageChanged(string _) {
        foreach(Surface s in surfaces) s.Relabel();
    }
    internal static void Shutdown() {
        if(hooked) MainCore.Tr.OnLanguageChanged -= OnLanguageChanged;
        hooked = false;
        FacetEffectRemover.Shutdown();
        foreach(Surface s in surfaces) s.Dispose();
        surfaces.Clear();
    }
}
#endif

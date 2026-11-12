#if !IL2CPP
using Facet;
using Quartz.Core;
using Quartz.Localization;
using Quartz.Prism;
using Quartz.UI.Utility;
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
    /// <summary>The theme wears the Prism suite look (PrismLib.UI's skin), or Facet's own look without it.</summary>
    internal static bool Suited { get; private set; }
    internal static Host Host {
        get {
            host ??= Host.Create("Quartz", o => {
                o.Log = m => MainCore.Log.Msg(m);
                o.Translate = Translate;
                o.Language = () => LanguageCode(MainCore.Tr.Language);
                o.Read = key => MainCore.Conf.FacetWindows.TryGetValue(key, out string v) ? v : null;
                o.Write = Write;
                o.Theme = MakeTheme(MainCore.Conf.GetAccentColor());
                o.Cursor = OsCursor;
                o.WheelSpeed = WheelSpeed;
            });
            if(!hooked) {
                hooked = true;
                MainCore.Tr.OnLanguageChanged += OnLanguageChanged;
                MainCore.Tr.OnLoadEnd += OnLoadEnd;
            }
            return host;
        }
    }
    // Labels are Lang keys; the DEFAULT language answers every Get with its fallback, so that is the English text.
    // Facet's faces draw Latin, plus Hangul, Han and kana through its CJK fallback; any other language would draw boxes, so it reads English.
    internal static string Translate(string s) => Text(s, MainCore.Tr.GetForLanguage(s, "en-US", s));
    /// <summary>Text Quartz formats itself: the key's translation when Facet can draw the language, else the given English.</summary>
    internal static string Text(string key, string english) =>
        Drawable(LanguageCode(MainCore.Tr.Language)) ? MainCore.Tr.Get(key, english) : english;
    /// <summary>True for the languages Facet draws: Latin, plus the Korean, Chinese and Japanese fallbacks.</summary>
    internal static bool Drawable(string code) => code is "en" or "ko" or "zh" or "ja";
    /// <summary>Re-tints every Quartz window. Before the host exists there is nothing to tint: it starts from the accent.</summary>
    internal static void Recolour(Color accent) {
        if(host == null) return;
        if(Suited) Accent(host.Theme, accent);
        else host.Theme.Recolour(accent);
    }
    private static Theme MakeTheme(Color accent) {
        var t = new Theme();
        Suited = PrismBridge.WearSuite(t);
        if(!Suited) return Theme.FromAccent(accent);
        Accent(t, accent);
        return t;
    }
    // The suite leaves Accent, Focus and AccentText to the host; a derived scheme knows the text that reads on this accent.
    private static void Accent(Theme t, Color accent) {
        t[Role.Accent] = t[Role.Focus] = accent;
        t[Role.AccentText] = Theme.FromAccent(accent)[Role.AccentText];
        t.Notify();
    }
    /// <summary>A surface on this host that is relabelled when Quartz's language changes and disposed on shutdown.</summary>
    internal static Surface NewSurface(string name, Layer layer) {
        var surface = new Surface(Host, name, layer);
        surfaces.Add(surface);
        return surface;
    }
    // The OS's own cursors, as the classic menu's resize edges show; where the OS has none, Facet draws its own.
    private static bool OsCursor(Cursors.Kind kind) {
        if(!NativeCursor.Native) return false;
        switch(kind) {
            case Cursors.Kind.ResizeHorizontal: NativeCursor.Apply(ResizeCursorShape.Horizontal); break;
            case Cursors.Kind.ResizeCorner: NativeCursor.Apply(ResizeCursorShape.DiagNWSE); break;
            case Cursors.Kind.Text: NativeCursor.IBeam(); break;
            default: NativeCursor.Reset(); break;
        }
        return true;
    }
    // Panel px per wheel unit, asked on every scroll so Scroll Speed applies live. The classic menu moves Scroll Speed of
    // its canvas px; both canvases scale with the screen from 1920x1080, the classic one by its UI scale on top and this
    // window by its own Scale, so the page moves as far on screen as the classic menu's.
    private static float WheelSpeed() {
        float ui = MainCore.Conf.UIScale <= 0f ? 1f : MainCore.Conf.UIScale;
        float scale = QuartzShell.Window?.Scale ?? ui;
        return MainCore.Conf.ScrollSpeed * ui / scale;
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
    // While the files reload every lookup reads the fallback, so a language change landing then drew English and kept it.
    // Relabel once they are in, as the classic menu refreshes its labels after a reload.
    private static void OnLoadEnd(TranslationFailState _) {
        Loads++;
        OnLanguageChanged(null);
    }
    /// <summary>Counts finished loads of the language files, a reload of the same language included: text made before one is stale.</summary>
    internal static int Loads { get; private set; }
    internal static void Shutdown() {
        if(hooked) {
            MainCore.Tr.OnLanguageChanged -= OnLanguageChanged;
            MainCore.Tr.OnLoadEnd -= OnLoadEnd;
        }
        hooked = false;
        QuartzShell.Shutdown();
        foreach(Surface s in surfaces) s.Dispose();
        surfaces.Clear();
    }
}
#endif

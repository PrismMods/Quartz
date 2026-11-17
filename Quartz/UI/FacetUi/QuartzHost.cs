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
                o.SansFonts = SuitFiles();
            });
            if(!hooked) {
                hooked = true;
                MainCore.Tr.OnLanguageChanged += OnLanguageChanged;
                MainCore.Tr.OnLoadEnd += OnLoadEnd;
            }
            return host;
        }
    }
    /// <summary>SUIT, Quartz's own UI face, as Facet's sans face (Medium standing in for bold): it has every Hangul syllable,
    /// so Korean draws from one face instead of the OS Gothic sampled behind Arial, whose syllables came out uneven. The
    /// files are the ones ResourceManager writes to Temp for TMP; null (Facet's own lists) when they cannot be written.</summary>
    internal static string[][] SuitFiles() {
        try {
            string Write(string resource, string name) {
                byte[] data = MainCore.Res?.Load(resource);
                if(data == null || data.Length == 0) return null;
                string path = System.IO.Path.Combine(MainCore.Paths.TempPath, name);
                System.IO.Directory.CreateDirectory(MainCore.Paths.TempPath);
                if(!System.IO.File.Exists(path) || new System.IO.FileInfo(path).Length != data.Length) System.IO.File.WriteAllBytes(path, data);
                return path;
            }
            string regular = Write("Font.SUIT-Regular.otf", "SUIT_Regular.otf"), medium = Write("Font.SUIT-Medium.otf", "SUIT_Medium.otf");
            return regular == null ? null : [medium == null ? [regular] : [regular, medium]];
        } catch(Exception e) {
            MainCore.Log.Err($"[{nameof(QuartzHost)}] SUIT for Facet: {e.Message}");
            return null;
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
        ClassicSkin.Wear(host.Theme);   // UIColors already follow the new accent
        host.Theme.SetAccent(accent);
        QuartzShortcut.Recolour();
    }
    /// <summary>Puts Quartz's classic skin back on the host theme, over whatever dressed it since.</summary>
    internal static void Reskin() {
        if(host == null) return;
        ClassicSkin.Wear(host.Theme);
        host.Theme.SetAccent(MainCore.Conf.GetAccentColor());   // redraws what wears it
    }
    private static Theme MakeTheme(Color accent) {
        // Quartz's own design system (the team's call): the classic palette and shapes on the new layout.
        var t = ClassicSkin.Wear(new Theme());
        t.SetAccent(accent);
        return t;
    }
    /// <summary>A surface on this host that is relabelled when Quartz's language changes and disposed on shutdown.</summary>
    internal static Surface NewSurface(string name, Layer layer) {
        var surface = new Surface(Host, name, layer);
        PrismBridge.PointerBlurs(surface.Root);   // a pointer click leaves no focus ring (Facet paints it on FocusIn)
        surfaces.Add(surface);
        return surface;
    }
    // The OS's own cursors, as the classic menu's resize edges show; where the OS has none, Facet draws its own.
    private static bool OsCursor(Cursors.Kind kind) {
        if(!NativeCursor.Native) return false;
        switch(kind) {
            case Cursors.Kind.ResizeHorizontal: NativeCursor.Apply(ResizeCursorShape.Horizontal); break;
            case Cursors.Kind.ResizeCorner: NativeCursor.Apply(ResizeCursorShape.DiagNWSE); break;
            case Cursors.Kind.ResizeVertical: NativeCursor.Apply(ResizeCursorShape.Vertical); break;
            case Cursors.Kind.ResizeCornerNESW: NativeCursor.Apply(ResizeCursorShape.DiagNESW); break;
            case Cursors.Kind.Move: NativeCursor.Move(); break;
            case Cursors.Kind.Text: NativeCursor.IBeam(); break;
            case Cursors.Kind.Default: NativeCursor.Reset(); break;
            default: return false;   // a kind Facet adds later: it draws its own
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
    // Every surface made on this host: Quartz's own and the new menu's, which its settings window made.
    private static void OnLanguageChanged(string _) => host?.RelabelSurfaces();
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
        QuartzShortcut.Shutdown();
        foreach(Surface s in surfaces) s.Dispose();
        surfaces.Clear();
    }
}
#endif

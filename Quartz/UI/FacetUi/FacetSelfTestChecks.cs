#if DEBUG && !IL2CPP
using System.Collections;
using System.Runtime.CompilerServices;
using Facet;
using Quartz.Core;
using Quartz.Features.KeyLimiter;
using Quartz.UI.Objects;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UIElements;
namespace Quartz.UI.FacetUi;
/// <summary>
/// The Facet-touching self-test checks. Run only when FacetBridge.Available(). Each check calls
/// the same decision methods the real input poll calls, since a test cannot press a key.
/// </summary>
internal static partial class FacetSelfTestChecks {
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static IEnumerator Run(Action<string, bool, string> check, Action<string> info) {
        info($"facet assembly {typeof(Host).Assembly.Location} loaded={AppDomain.CurrentDomain.GetAssemblies().Count(a => a.GetName().Name == "Facet.0.2")}");
        // Every window these checks open writes its geometry into the live settings: put the map back.
        var windows = new Dictionary<string, string>(MainCore.Conf.FacetWindows);
        try {
            yield return Suite(check, info);
            yield return Shell(check, info);   // first: its open is the new menu's first of the session
            yield return EffectRemoverWindow(check, info);
            yield return SwitchHandOff(check);
            yield return Escape(check);
            yield return TextFocus(check);
            yield return Block(check);
            check("translate and language", TranslateAndLanguage(out string d), d);
            yield return ChineseDraws(check);
            check("FilterLabel follows the game's script", FilterLabelScripts(out d), d);
            check("read/write round trip", ReadWrite(out d), d);
            yield return Teardown(check);   // last: it disposes Quartz's surfaces, as quitting does
        } finally {
            MainCore.Conf.FacetWindows.Clear();
            foreach(var kv in windows) MainCore.Conf.FacetWindows[kv.Key] = kv.Value;
            MainCore.ConfMgr.RequestSave();
        }
        check("no window keys left in the settings", MainCore.Conf.FacetWindows.Keys.All(windows.ContainsKey),
            string.Join(",", MainCore.Conf.FacetWindows.Keys.Where(k => !windows.ContainsKey(k))));
    }
    // P26: clicks may reach the game behind a Quartz-only preview. What a click at the open window's centre
    // would hit through the uGUI EventSystem, and whether KeyLimiter's block holds right now. Not asserted.
    private static void ClickReach(Action<string> info) {
        VisualElement root = QuartzShell.Window.Root;
        Vector2 centre = root.worldBound.center;
        float scale = Screen.width / root.panel.visualTree.layout.width;
        var screen = new Vector2(centre.x * scale, Screen.height - (centre.y * scale));   // panel y runs down
        var hits = new List<RaycastResult>();
        EventSystem es = EventSystem.current;
        if(es != null) es.RaycastAll(new PointerEventData(es) { position = screen }, hits);
        string what = es == null ? "no EventSystem" : string.Join(", ",
            hits.Select(h => $"{(h.gameObject != null ? h.gameObject.name : "null")} ({(h.module != null ? h.module.GetType().Name : "null")})"));
        info($"raycast at the window centre {screen} (panel {centre}, scale {scale:0.###}) hits {hits.Count}: {what}");
        ModState state = MainCore.Runtime.State;
        bool wasEnabled = state.IsEnabled, asIs = KeyLimiter.IsMenuBlockActive(), modOn;
        state.IsEnabled = true;
        try { modOn = KeyLimiter.IsMenuBlockActive(); } finally { state.IsEnabled = wasEnabled; }
        info($"keylimiter block as-is={asIs} (mod enabled={wasEnabled}), with the mod on={modOn}; bridge blocks={FacetBridge.BlocksGameInput()}"
            + $" window blocks={QuartzShell.Surface.BlocksGameInput} setting={MainCore.Conf.BlockInputsWhileMenuOpen}");
    }
    private static IEnumerator Frames(int n) {
        for(int i = 0; i < n; i++) {
            int f = Time.frameCount;
            while(Time.frameCount == f) yield return null;
        }
    }
    private static Surface Window(string name, out Window w) {
        var s = new Surface(QuartzHost.Host, name, Layer.Window);
        w = new Window(s.Root, name, name, new Rect(100, 100, 400, 300));
        return s;
    }
    private static IEnumerator Escape(Action<string, bool, string> check) {
        bool wasOpen = UICore.IsOpen;
        Surface s = null;
        try {
            // Menu open: the first Escape is Facet's, the second (a later frame) is the menu's.
            if(!UICore.IsOpen) UICore.Open(true);
            s = Window("QuartzSelfTestEsc1", out _);
            yield return Frames(2);
            UICore.EscapeDown();
            yield return Frames(3); // the deferred menu Escape would have run by now
            check("escape 1 with menu open: facet window hides", !s.Visible, null);
            check("escape 1 with menu open: menu stays open", UICore.IsOpen, null);
            UICore.EscapeDown();
            yield return Frames(3);
            check("escape 2 in a later frame closes the menu", !UICore.IsOpen, null);
            s.Dispose();
            // Another mod already took this frame's Escape (as a PrismLib mod polling first would).
            if(!UICore.IsOpen) UICore.Open(true);
            s = Window("QuartzSelfTestEscShared", out _);
            yield return Frames(2);
            bool otherTook = FacetBridge.HandleEscape();
            UICore.EscapeDown(); // same frame: Facet's stamp answers true again
            check("shared escape: other mod's call took it", otherTook, null);
            check("shared escape: facet window hidden, menu open in the same frame", !s.Visible && UICore.IsOpen, null);
            yield return Frames(3);
            check("shared escape: no deferred close a frame later", UICore.IsOpen, null);
            s.Dispose();
            // Menu closed.
            if(UICore.IsOpen) UICore.Close(true);
            s = Window("QuartzSelfTestEsc2", out _);
            yield return Frames(2);
            UICore.EscapeDown();
            yield return Frames(3);
            check("escape with menu closed: facet window hides", !s.Visible, null);
            check("escape with menu closed: menu stays closed", !UICore.IsOpen, null);
        } finally {
            s?.Dispose();
            if(wasOpen && !UICore.IsOpen) UICore.Open(true);
            else if(!wasOpen && UICore.IsOpen) UICore.Close(true);
        }
    }
    private static IEnumerator TextFocus(Action<string, bool, string> check) {
        Surface s = Window("QuartzSelfTestText", out Window w);
        try {
            var tf = new TextField();
            w.Body.Add(tf);
            yield return Frames(2);
            check("hotkey allowed before focus", UICore.ToggleHotkeyAllowed(true, true), null);
            tf.Focus();
            yield return Frames(2);
            check("TextFocused true with a Facet field focused", FacetBridge.TextFocused(), null);
            check("editing notion includes the Facet field", UIInputBlocker.IsEditing, null);
            check("menu hotkey does not toggle while typing", !UICore.ToggleHotkeyAllowed(true, true), null);
            tf.Blur();
            yield return Frames(2);
            check("hotkey allowed again after blur", UICore.ToggleHotkeyAllowed(true, true), null);
        } finally {
            s.Dispose();
        }
    }
    // The block only runs with the mod on; this rig may have it off, so flip the flag (no feature
    // enable/disable, no save) for the duration and restore it.
    private static IEnumerator Block(Action<string, bool, string> check) {
        ModState state = MainCore.Runtime.State;
        bool wasEnabled = state.IsEnabled;
        state.IsEnabled = true;
        try {
            yield return BlockChecks(check);
        } finally {
            state.IsEnabled = wasEnabled;
        }
    }
    private static IEnumerator BlockChecks(Action<string, bool, string> check) {
        bool wasOpen = UICore.IsOpen;
        if(wasOpen) UICore.Close(true);
        yield return Frames(2);
        check("no block with nothing open", !KeyLimiter.IsMenuBlockActive(), null);
        Surface s = Window("QuartzSelfTestBlock", out _);
        yield return Frames(2);
        check("block active while a blocking facet window is visible", KeyLimiter.IsMenuBlockActive(), null);
        s.Visible = false;
        check("block still active the frame the window hides", KeyLimiter.IsMenuBlockActive(), null);
        yield return Frames(1);
        check("block inactive one frame later", !KeyLimiter.IsMenuBlockActive(), null);
        s.Dispose();
        if(wasOpen) UICore.Open(true);
    }
    private static bool TranslateAndLanguage(out string detail) {
        string key = "FXRM_MODE_SIMPLE";
        string expected = MainCore.Tr.Get(key, key);
        string got = QuartzHost.Host.Translate(key);
        string unknown = QuartzHost.Host.Translate("Not A Quartz Key");
        string lang = MainCore.Tr.Language ?? "";
        string code = QuartzHost.Host.Language;
        bool ok = got == expected && got != key && unknown == "Not A Quartz Key"
            && (!lang.StartsWith("en") || got == "Simple")
            && (!lang.StartsWith("ko") || code == "ko")
            && (!lang.StartsWith("en") || code == "en")
            && QuartzHost.LanguageCode("ko-KR") == "ko" && QuartzHost.LanguageCode("en-US") == "en"
            && QuartzHost.LanguageCode("pt-BR") == "pt" && QuartzHost.LanguageCode("DEFAULT") == "en"
            && QuartzHost.LanguageCode("") == "en" && QuartzHost.LanguageCode(null) == "en";
        detail = $"lang={lang} code={code} got={got} unknown={unknown}";
        return ok;
    }
    // Facet draws Latin, Hangul, Han and kana. The language is swapped and put back in the finally; Quartz's
    // surfaces relabel a frame after the change, and that is what loads Facet's CJK fallback face.
    private static IEnumerator ChineseDraws(Action<string, bool, string> check) {
        const string key = "FXRM_MODE_SIMPLE";
        string was = MainCore.Tr.Language;
        string english = MainCore.Tr.GetForLanguage(key, "en-US", key);
        bool ok = false;
        string detail = "";
        try {
            MainCore.Tr.Language = "zh-CN";
            // The relabel lands within a frame or two; wait for the font to hold the text instead of a fixed count.
            for(int i = 0; i < 30 && !Fonts.Has(Fonts.Regular(QuartzHost.Host), QuartzHost.Host.Translate(key)); i++) yield return null;
            string zh = MainCore.Tr.Get(key, english), shown = QuartzHost.Host.Translate(key);
            bool has = Fonts.Has(Fonts.Regular(QuartzHost.Host), shown);
            bool codes = QuartzHost.Drawable("ja") && !QuartzHost.Drawable("pt");
            bool filter = RDString.language != SystemLanguage.English
                || FacetEffectRemover.FilterLabel(Filter.NightVision) == RDString.GetEnumValue(Filter.NightVision);
            detail = $"game={RDString.language} en={english} zh={zh} shown={shown} has={has} codes={codes} filter={filter}";
            ok = shown == zh && zh != english && has && codes && filter;
        } finally {
            MainCore.Tr.Language = was;
        }
        yield return null;   // let the restored language relabel before the next check
        check("ChineseDraws", ok, detail);
    }
    // The game's own filter name shows only when Facet can draw its script: the game's language and Quartz's agree.
    private static bool FilterLabelScripts(out string detail) {
        string was = MainCore.Tr.Language;
        SystemLanguage game = RDString.language;
        try {
            string Own() => RDString.GetEnumValue(Filter.NightVision);
            string Label() => FacetEffectRemover.FilterLabel(Filter.NightVision);
            RDString.language = SystemLanguage.Japanese;
            MainCore.Tr.Language = "ja-JP"; bool ja = Label() == Own();
            MainCore.Tr.Language = "zh-CN"; bool jaUnderZh = Label() == "Night Vision";
            RDString.language = SystemLanguage.ChineseSimplified;
            bool zh = Label() == Own();
            RDString.language = SystemLanguage.ChineseTraditional;
            bool zhTw = Label() == Own();
            MainCore.Tr.Language = "ko-KR"; bool zhUnderKo = Label() == "Night Vision";
            detail = $"ja={ja} jaUnderZh={jaUnderZh} zh={zh} zhTw={zhTw} zhUnderKo={zhUnderKo}";
            return ja && jaUnderZh && zh && zhTw && zhUnderKo;
        } finally {
            RDString.language = game;
            MainCore.Tr.Language = was;
        }
    }
    private static bool ReadWrite(out string detail) {
        const string key = "selftest.roundtrip";
        Host h = QuartzHost.Host;
        bool missing = h.Read(key) == null;
        h.Write(key, "v1");
        bool back = h.Read(key) == "v1" && MainCore.Conf.FacetWindows[key] == "v1"
            && (string)MainCore.Conf.Serialize()[nameof(MainCore.Conf.FacetWindows)]?[key] == "v1";
        h.Write(key, null);
        bool gone = h.Read(key) == null;
        detail = $"missing={missing} back={back} gone={gone}";
        return missing && back && gone;
    }
}
#endif

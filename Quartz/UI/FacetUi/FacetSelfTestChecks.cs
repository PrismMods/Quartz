#if DEBUG && !IL2CPP
using System.Collections;
using System.Runtime.CompilerServices;
using Facet;
using Quartz.Core;
using Quartz.Features.KeyLimiter;
using Quartz.UI.Objects;
using UnityEngine;
using UnityEngine.UIElements;
namespace Quartz.UI.FacetUi;
/// <summary>
/// The Facet-touching self-test checks. Run only when FacetBridge.Available(). Each check calls
/// the same decision methods the real input poll calls, since a test cannot press a key.
/// </summary>
internal static partial class FacetSelfTestChecks {
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static IEnumerator Run(Action<string, bool, string> check) {
        // Every window these checks open writes its geometry into the live settings: put the map back.
        var windows = new Dictionary<string, string>(MainCore.Conf.FacetWindows);
        try {
            yield return EffectRemoverWindow(check);
            yield return Escape(check);
            yield return TextFocus(check);
            yield return Block(check);
            check("translate and language", TranslateAndLanguage(out string d), d);
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

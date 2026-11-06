#if DEBUG && !IL2CPP
using System.Collections;
using Facet;
using Newtonsoft.Json.Linq;
using Quartz.Core;
using Quartz.Features.EffectRemover;
using Quartz.IO;
using Quartz.Resource;
using TMPro;
using UnityEngine;
using UnityEngine.UIElements;
namespace Quartz.UI.FacetUi;
/// <summary>
/// The Effect Remover window checks. Run first, so its open is Quartz's first Facet window of the
/// session. Every setting they touch is put back, the settings file byte-for-byte (Run restores window keys).
/// </summary>
internal static partial class FacetSelfTestChecks {
    private const float Within = 0.15f;
    private static EffectRemoverSettings C => EffectRemover.Conf;
    private static VisualElement Body => FacetEffectRemover.Window.Body;
    // One top-level toggle per mode (always on screen, never folded away): the field behind it.
    private static (string key, string field, Func<EffectRemoverSettings, bool> get, Action<EffectRemoverSettings, bool> set) Probe() =>
        C.IsSimple
            ? ("FXRM_S_FILTER", nameof(EffectRemoverSettings.SimpleFilter), c => c.SimpleFilter, (c, v) => c.SimpleFilter = v)
            : ("FXRM_ENABLE_SAVE", nameof(EffectRemoverSettings.EnableSave), c => c.EnableSave, (c, v) => c.EnableSave = v);
    private static IEnumerator EffectRemoverWindow(Action<string, bool, string> check) {
        EffectRemover.EnsureConf();
        string path = EffectRemover.ConfMgr.Path;
        byte[] file = File.Exists(path) ? File.ReadAllBytes(path) : null;
        string saved = C.Serialize().ToString();
        bool wasOpen = UICore.IsOpen;
        try {
            if(!UICore.IsOpen) UICore.Open(true);
            yield return Frames(2);
            yield return FirstOpen(check);
            if(FacetEffectRemover.Window == null) yield break;
            yield return PageMirrorsMenu(check);
            yield return ClickFlips(check);
            yield return ShowsBehindWindow(check);
            yield return ProfileSwitch(check, path, file);
            AccentFollows(check);
            yield return MenuHandOff(check);
        } finally {
            FacetEffectRemover.Hide();
            RestoreSettings(path, file, saved);
            if(wasOpen && !UICore.IsOpen) UICore.Open(true);
            else if(!wasOpen && UICore.IsOpen) UICore.Close(true);
        }
        // A debounced save already handed to a worker can land after the finally: settle, then put it back again.
        float until = Time.realtimeSinceStartup + 1f;
        while(Time.realtimeSinceStartup < until) yield return null;
        RestoreSettings(path, file, saved);
        check("effect remover settings restored byte-for-byte", SameBytes(path, file) && C.Serialize().ToString() == saved, null);
    }
    // QuartzRuntime.Dispose reaches QuartzHost.Shutdown with the window open: no menu may come back.
    private static IEnumerator Teardown(Action<string, bool, string> check) {
        bool wasOpen = UICore.IsOpen;
        if(!wasOpen) UICore.Open(true);
        FacetBridge.OpenEffectRemover();
        yield return Frames(2);
        Surface s = FacetEffectRemover.Surface;
        bool shown = s?.Visible == true && !UICore.IsOpen;
        QuartzHost.Shutdown();
        yield return Frames(2);
        check("teardown disposes the open window without reopening the menu",
            shown && s?.Visible == false && !UICore.IsOpen && FacetEffectRemover.Surface == null && FacetEffectRemover.Window == null,
            $"shown={shown} window={s?.Visible} menu={UICore.IsOpen} kept={FacetEffectRemover.Surface != null}");
        if(wasOpen && !UICore.IsOpen) UICore.Open(true);
        else if(!wasOpen && UICore.IsOpen) UICore.Close(true);
    }
    private static void RestoreSettings(string path, byte[] file, string saved) {
        EffectRemover.ConfMgr.CancelPendingSave();
        C.Deserialize(JToken.Parse(saved));
        if(file == null) { if(File.Exists(path)) File.Delete(path); }
        else if(!SameBytes(path, file)) File.WriteAllBytes(path, file);
    }
    private static bool SameBytes(string path, byte[] file) =>
        file == null ? !File.Exists(path) : File.Exists(path) && File.ReadAllBytes(path).SequenceEqual(file);
    private static IEnumerator FirstOpen(Action<string, bool, string> check) {
        float until = Time.realtimeSinceStartup + 10f;
        while(!WarmUp.Done && Time.realtimeSinceStartup < until) yield return null;
        check("facet warm-up done before the first open", WarmUp.Done, null);
        bool first = FacetEffectRemover.Surface == null;
        FacetBridge.OpenEffectRemover();
        yield return Frames(2);
        double ms = FacetEffectRemover.FirstOpenMs;
        check("[perf] first open is at most 50 ms", first && ms >= 0 && ms <= 50, $"first={first} ms={ms:0.0}");
        check("opening hides the menu and shows the window",
            !UICore.IsOpen && !UICore.CanvasObj.activeSelf && FacetEffectRemover.Surface?.Visible == true,
            $"menu={UICore.IsOpen} canvas={UICore.CanvasObj.activeSelf} window={FacetEffectRemover.Surface?.Visible}");
    }
    // Toggles are the rows whose input is a bare VisualElement (Facet's switch track); every row has an Id.
    private static int FacetToggles() =>
        Body.Query<VisualElement>().Where(e => e.GetType() == typeof(VisualElement) && e.name != null && e.name.EndsWith("-input")).ToList().Count;
    // The uGUI page built as the menu builds it; every GenerateUI.Toggle has one ToggleCircle.
    private static T WithMenuPage<T>(Func<RectTransform, T> read) {
        var host = new GameObject("FacetSelfTestPage", typeof(RectTransform)).GetComponent<RectTransform>();
        host.SetParent(UICore.Panel, false);
        try {
            PageEffectRemover.Create(host);
            return read(host);
        } finally { UnityEngine.Object.Destroy(host.gameObject); }
    }
    private static int MenuToggles() =>
        WithMenuPage(page => page.GetComponentsInChildren<Transform>(true).Count(t => t.name == "ToggleCircle"));
    private static IEnumerator PageMirrorsMenu(Action<string, bool, string> check) {
        check("the menu page offers the preview button", FacetSelfTest.PreviewButtonOnPage(), null);
        int facet = FacetToggles(), menu = MenuToggles();
        check("one facet toggle per menu toggle in the current mode", facet == menu && facet > 0, $"mode={C.Mode} facet={facet} menu={menu}");
        VisualElement mode = Body.Q(name: "FXRM_MODE");
        check("the mode dropdown exists", mode != null && Body.Q<Button>("FXRM_MODE-input") != null, null);
        // A mode change behind the window swaps only the mode rows.
        string was = C.Mode;
        VisualElement old = Body.Q(name: Probe().key + "-input");
        C.Mode = C.IsSimple ? EffectRemoverSettings.ModeEnhanced : EffectRemoverSettings.ModeSimple;
        float t0 = Time.realtimeSinceStartup;
        while(old != null && old.panel != null && Time.realtimeSinceStartup - t0 < 1f) yield return null;
        float ms = (Time.realtimeSinceStartup - t0) * 1000f;
        int facetOther = FacetToggles(), menuOther = MenuToggles();
        check("a mode change behind the window rebuilds the mode rows within 150 ms",
            old != null && old.panel == null && ms <= Within * 1000f && facetOther == menuOther,
            $"mode={C.Mode} {ms:0} ms facet={facetOther} menu={menuOther}");
        check("only the mode rows are rebuilt: the dropdown is the same element", mode != null && Body.Q(name: "FXRM_MODE") == mode, null);
        C.Mode = was;
        t0 = Time.realtimeSinceStartup;
        while(FacetToggles() != facet && Time.realtimeSinceStartup - t0 < 1f) yield return null;
    }
    // Pooled through reflection: the typed GetPooled overloads name IMGUI types Quartz does not reference,
    // so neither a positioned pointer press nor the typed submit compiles here.
    private static T Pooled<T>() where T : EventBase<T>, new() =>
        (T)typeof(EventBase<T>).GetMethod("GetPooled", Type.EmptyTypes).Invoke(null, null);
    // A click on the element itself, as Facet's AutoTest ClickEl sends it.
    private static void Click(VisualElement e) {
        using ClickEvent ev = Pooled<ClickEvent>();
        ev.target = e;
        e.SendEvent(ev);
    }
    private static void Submit(Button b) {
        using NavigationSubmitEvent ev = Pooled<NavigationSubmitEvent>();
        ev.target = b;
        b.SendEvent(ev);
    }
    private static bool MenuShows(string key, bool value) => WithMenuPage(page => {
        Sprite on = MainCore.Spr.Get(UISprite.Circle256), off = MainCore.Spr.Get(UISprite.ToggleCircle128);
        string label = QuartzHost.Host.Translate(key);
        foreach(Transform t in page.GetComponentsInChildren<Transform>(true)) {
            if(t.name != "ToggleCircle" || t.parent.GetComponentInChildren<TextMeshProUGUI>(true)?.text != label) continue;
            return t.GetComponent<UnityEngine.UI.Image>().sprite == (value ? on : off);
        }
        return false;
    });
    private static IEnumerator ClickFlips(Action<string, bool, string> check) {
        var p = Probe();
        VisualElement track = Body.Q(name: p.key + "-input");
        if(track == null) { check("clicking a facet toggle flips the setting", false, $"no {p.key}"); yield break; }
        bool before = p.get(C);
        Body.Q<ScrollView>()?.ScrollTo(track);
        yield return Frames(2);
        Click(track);
        yield return Frames(2);
        bool now = p.get(C);
        check("clicking a facet toggle flips the setting", now == !before, $"{p.field} {before} -> {now}");
        check("a rebuilt menu page shows the new value", MenuShows(p.key, now) && now != before, null);
        p.set(C, before);
        EffectRemover.Save();
        yield return Frames(1);
    }
    private static bool Shows(VisualElement track, bool on) {
        Color want = FacetEffectRemover.Surface.Theme[on ? Role.Accent : Role.RowAlt], got = track.style.backgroundColor.value;
        return Mathf.Abs(want.r - got.r) < 0.01f && Mathf.Abs(want.g - got.g) < 0.01f && Mathf.Abs(want.b - got.b) < 0.01f && Mathf.Abs(want.a - got.a) < 0.01f;
    }
    // Waits for track to show `on`; the elapsed ms, or -1 when it never did within a second.
    private static IEnumerator Await(VisualElement track, bool on, float[] ms) {
        float t0 = Time.realtimeSinceStartup;
        while(!Shows(track, on) && Time.realtimeSinceStartup - t0 < 1f) yield return null;
        ms[0] = Shows(track, on) ? (Time.realtimeSinceStartup - t0) * 1000f : -1f;
    }
    private static IEnumerator ShowsBehindWindow(Action<string, bool, string> check) {
        var p = Probe();
        VisualElement track = Body.Q(name: p.key + "-input");
        if(track == null) { check("a value set behind the window shows within 150 ms", false, $"no {p.key}"); yield break; }
        bool v = p.get(C);
        float[] ms = [0f];
        yield return Await(track, v, ms);   // the restore above has landed
        p.set(C, !v);
        yield return Await(track, !v, ms);
        check("a value set behind the window shows within 150 ms", ms[0] >= 0 && ms[0] <= Within * 1000f, $"{p.field}={!v} {ms[0]:0} ms");
        p.set(C, v);
        yield return Await(track, v, ms);
    }
    // ProfileManager.Apply rewrites the live settings files, then ReloadAll runs CancelPendingSave + LoadOrDefaults
    // on each handle. The same two steps on EffectRemover.json alone; the original bytes go back after.
    private static IEnumerator ProfileSwitch(Action<string, bool, string> check, string path, byte[] file) {
        var p = Probe();
        VisualElement track = Body.Q(name: p.key + "-input");
        if(track == null) { check("a profile switch shows the new value within 150 ms in the same control", false, $"no {p.key}"); yield break; }
        bool v = p.get(C);
        var json = (JObject)C.Serialize();
        json[p.field] = !v;
        AtomicFile.WriteAllText(path, json.ToString());
        EffectRemover.ConfMgr.CancelPendingSave();
        EffectRemover.ConfMgr.LoadOrDefaults();
        float[] ms = [0f];
        yield return Await(track, !v, ms);
        VisualElement after = Body.Q(name: p.key + "-input");
        check("a profile switch shows the new value within 150 ms in the same control",
            p.get(C) == !v && ms[0] >= 0 && ms[0] <= Within * 1000f && ReferenceEquals(after, track),
            $"{p.field}={p.get(C)} {ms[0]:0} ms same={ReferenceEquals(after, track)}");
        if(file == null) File.Delete(path); else File.WriteAllBytes(path, file);
        EffectRemover.ConfMgr.CancelPendingSave();
        EffectRemover.ConfMgr.LoadOrDefaults();
        yield return Await(track, v, ms);
    }
    private static bool Near(Color a, Color b) =>
        Mathf.Abs(a.r - b.r) < 0.01f && Mathf.Abs(a.g - b.g) < 0.01f && Mathf.Abs(a.b - b.b) < 0.01f;
    private static void AccentFollows(Action<string, bool, string> check) {
        VisualElement bar = FacetEffectRemover.Window.Root[0];
        Color accent = MainCore.Conf.GetAccentColor();
        // Red, unless the accent already is red-ish and the change would not show.
        Color test = Near(Theme.FromAccent(accent)[Role.TitleBar], Theme.FromAccent(Color.red)[Role.TitleBar]) ? Color.cyan : Color.red;
        UIColors.ApplyAccent(test);
        Color mid = bar.style.backgroundColor.value;   // same frame
        UIColors.ApplyAccent(accent);
        Color back = bar.style.backgroundColor.value;
        check("the accent recolours the title bar in the same frame", Near(mid, Theme.FromAccent(test)[Role.TitleBar]), $"test={test} bar={mid}");
        check("restoring the accent recolours it back", !Near(mid, back) && Near(back, Theme.FromAccent(accent)[Role.TitleBar]), $"bar={back}");
    }
    private static IEnumerator MenuHandOff(Action<string, bool, string> check) {
        Surface s = FacetEffectRemover.Surface;
        if(!s.Visible) FacetBridge.OpenEffectRemover();
        yield return Frames(2);
        bool hidden = !UICore.IsOpen;   // each hand-back below only counts when the menu was really gone
        Button close = FacetEffectRemover.Window.TitleBarRight.Query<Button>().ToList().LastOrDefault();
        if(close != null) Submit(close);
        yield return Frames(2);
        check("the close button hides the window and brings the menu back", hidden && !s.Visible && UICore.IsOpen, $"hidden={hidden} window={s.Visible} menu={UICore.IsOpen}");
        FacetBridge.OpenEffectRemover();
        yield return Frames(2);
        check("reopening reuses the surface and hides the menu",
            ReferenceEquals(FacetEffectRemover.Surface, s) && s.Visible && !UICore.IsOpen && !UICore.CanvasObj.activeSelf,
            $"same={ReferenceEquals(FacetEffectRemover.Surface, s)} window={s.Visible} menu={UICore.IsOpen}");
        hidden = !UICore.IsOpen;
        UICore.EscapeDown();
        yield return Frames(3);
        check("Escape hides the window and brings the menu back", hidden && !s.Visible && UICore.IsOpen, $"hidden={hidden} window={s.Visible} menu={UICore.IsOpen}");
        FacetBridge.OpenEffectRemover();
        yield return Frames(2);
        hidden = !UICore.IsOpen;
        UICore.Toggle();   // the menu hotkey's action
        yield return Frames(2);
        check("the menu hotkey hides the window and the menu comes back once",
            hidden && !s.Visible && UICore.IsOpen && !UICore.IsReorganizing, $"hidden={hidden} window={s.Visible} menu={UICore.IsOpen} hub={UICore.IsReorganizing}");
    }
}
#endif

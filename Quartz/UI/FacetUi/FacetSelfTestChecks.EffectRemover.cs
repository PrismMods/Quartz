#if DEBUG && !IL2CPP
using System.Collections;
using System.Reflection;
using Facet;
using Newtonsoft.Json.Linq;
using Quartz.Core;
using Quartz.Features;
using Quartz.Features.EffectRemover;
using Quartz.IO;
using Quartz.Resource;
using Quartz.UI.Nav;
using TMPro;
using UnityEngine;
using UnityEngine.UIElements;
namespace Quartz.UI.FacetUi;
/// <summary>
/// The Effect Remover page in the new menu, opened from the classic menu's button. Every setting they touch is put
/// back, the settings file byte-for-byte (Run restores window keys).
/// </summary>
internal static partial class FacetSelfTestChecks {
    private const float Within = 0.15f;
    private static EffectRemoverSettings C => EffectRemover.Conf;
    // The new menu's scrollers in tree order are the rail's, then the content's; Body is the open page.
    private static ScrollView ContentScroll => QuartzShell.Window.Body.Query<ScrollView>().Where(s => s != RailScroll).First();
    private static VisualElement Body => ContentScroll.contentContainer;
    // One top-level toggle per mode (always on screen, never folded away): the field behind it.
    private static (string key, string field, Func<EffectRemoverSettings, bool> get, Action<EffectRemoverSettings, bool> set) Probe() =>
        C.IsSimple
            ? ("FXRM_S_FILTER", nameof(EffectRemoverSettings.SimpleFilter), c => c.SimpleFilter, (c, v) => c.SimpleFilter = v)
            : ("FXRM_ENABLE_SAVE", nameof(EffectRemoverSettings.EnableSave), c => c.EnableSave, (c, v) => c.EnableSave = v);
    private static IEnumerator EffectRemoverWindow(Action<string, bool, string> check, Action<string> info) {
        EffectRemover.EnsureConf();
        string path = EffectRemover.ConfMgr.Path;
        byte[] file = File.Exists(path) ? File.ReadAllBytes(path) : null;
        string saved = C.Serialize().ToString();
        bool wasOpen = UICore.IsOpen, newMenu = MainCore.Conf.ExperimentalNewMenu;
        MainCore.Conf.ExperimentalNewMenu = true;   // the classic page's button, which these checks stand in for, shows only with it on
        try {
            if(!UICore.IsOpen) UICore.Open(true);
            yield return Frames(2);
            FacetBridge.OpenNewMenu("visuals.effectremover", fromClassic: true);
            yield return Frames(2);
            check("the menu's button hides it and shows Effect Remover in the new menu, which counts as a menu showing",
                !UICore.IsOpen && !UICore.CanvasObj.activeSelf && QuartzShell.Surface?.Visible == true && UICore.MenuShowing
                    && QuartzShell.CurrentPage == "visuals.effectremover",
                $"menu={UICore.IsOpen} canvas={UICore.CanvasObj.activeSelf} window={QuartzShell.Surface?.Visible} showing={UICore.MenuShowing} page={QuartzShell.CurrentPage}");
            if(QuartzShell.Window == null) yield break;
            ClickReach(info);
            yield return HeaderAndCards(check);
            yield return PageMirrorsMenu(check);
            yield return ClickFlips(check);
            yield return ShowsBehindWindow(check);
            yield return ProfileSwitch(check, path, file);
            yield return ClassicAfterHandBack(check);
            yield return MenuHandOff(check);
        } finally {
            MainCore.Conf.ExperimentalNewMenu = newMenu;
            QuartzShell.Hide();
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
    // QuartzRuntime.Dispose reaches QuartzHost.Shutdown with the new menu open from the classic one: no menu may come back.
    private static IEnumerator Teardown(Action<string, bool, string> check) {
        bool wasOpen = UICore.IsOpen;
        if(!wasOpen) UICore.Open(true);
        FacetBridge.OpenNewMenu(fromClassic: true);
        yield return Frames(2);
        Surface s = QuartzShell.Surface;
        bool shown = s?.Visible == true && !UICore.IsOpen;
        QuartzHost.Shutdown();
        yield return Frames(2);
        check("teardown disposes the open window without reopening the menu",
            shown && s?.Visible == false && !UICore.IsOpen && QuartzShell.Surface == null && QuartzShell.Window == null,
            $"shown={shown} window={s?.Visible} menu={UICore.IsOpen} kept={QuartzShell.Surface != null}");
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
    // Toggles are the rows whose input is a bare VisualElement (Facet's switch track); every row has an Id.
    // The header's switch is skipped: a classic page built directly has no "Use this feature" row to match it.
    private static int FacetToggles() =>
        Body.Query<VisualElement>().Where(e => e.GetType() == typeof(VisualElement) && e.name != null && e.name.EndsWith("-input")
            && e.name != "FXRM_HEADER-input").ToList().Count;
    private static bool HasCaps(string key) {
        string want = QuartzHost.Host.Translate(key).ToUpperInvariant();
        return Body.Query<Label>().Where(l => l.text == want).ToList().Count > 0;
    }
    // The page opens with its header (title, switch) and, in Enhanced, a card per heading.
    private static IEnumerator HeaderAndCards(Action<string, bool, string> check) {
        Label title = Body.Q(name: "FXRM_HEADER")?.Q<Label>();
        string want = QuartzHost.Host.Translate("SECTION_EFFECT_REMOVER");
        float size = QuartzHost.Host.Theme.Sizes.Title;
        check("the page header reads the feature's title, bold, at the title size",
            title != null && title.text == want && title.resolvedStyle.unityFontStyleAndWeight == FontStyle.Bold && title.resolvedStyle.fontSize == size,
            $"text={title?.text} want={want} weight={title?.resolvedStyle.unityFontStyleAndWeight} size={title?.resolvedStyle.fontSize} want={size}");
        VisualElement track = Body.Q(name: "FXRM_HEADER-input");
        bool on = FeatureService.IsEnabled("effectremover");
        check("the header's switch shows whether the feature is on", track != null && Shows(track, on), $"on={on} track={track != null}");
        string was = C.Mode;
        C.Mode = EffectRemoverSettings.ModeEnhanced;
        float t0 = Time.realtimeSinceStartup;
        while(!HasCaps("HEADING_MISC") && Time.realtimeSinceStartup - t0 < 1f) yield return null;
        bool cards = HasCaps("HEADING_NON_DLC_EVENTS") && HasCaps("HEADING_DLC_EVENTS") && HasCaps("HEADING_MISC");
        check("Enhanced has a card headed by each of the three groups", cards,
            $"nonDlc={HasCaps("HEADING_NON_DLC_EVENTS")} dlc={HasCaps("HEADING_DLC_EVENTS")} misc={HasCaps("HEADING_MISC")}");
        yield return ToggleAllLast(check);
        C.Mode = was;
        t0 = Time.realtimeSinceStartup;
        while(HasCaps("HEADING_MISC") != (was == EffectRemoverSettings.ModeEnhanced) && Time.realtimeSinceStartup - t0 < 1f) yield return null;
    }
    // Each Toggle All row comes after its section's switches; a pointer click on one leaves no focus (so no ring) on it.
    // The settings it flips are put back by EffectRemoverWindow's restore.
    private static IEnumerator ToggleAllLast(Action<string, bool, string> check) {
        string[] last = [];
        foreach((string all, string row) in new[] { ("FXRM_PLANET_ALL", "FXRM_PLANET_RADIUS"), ("FXRM_TRACK_ALL", "FXRM_TRACK_COLORS"),
                     ("FXRM_DECO_TYPES_ALL", "FXRM_DECO_FAILHITBOX") }) {
            VisualElement a = Body.Q(name: all), r = Body.Q(name: row);
            if(a == null || r == null || a.parent != r.parent || a.parent.IndexOf(a) != a.parent.childCount - 1) last = [.. last, all];
        }
        check("effect remover: every Toggle All sits under its section's switches", last.Length == 0, "not last: " + string.Join(",", last));
        VisualElement section = Body.Q(name: "FXRM_PLANET_ALL")?.parent;
        Button b = Body.Q(name: "FXRM_PLANET_ALL")?.Q<Button>();
        if(section == null || b == null) { check("effect remover: a clicked Toggle All holds no focus", false, "no FXRM_PLANET_ALL button"); yield break; }
        StyleEnum<DisplayStyle> shown = section.style.display;
        section.style.display = DisplayStyle.Flex;   // the section starts shut; a hidden button cannot take focus
        yield return Frames(2);
        Vector2 at = b.worldBound.center;
        Pointer<PointerDownEvent>(b, at);
        b.Focus();   // as a real press would
        Pointer<PointerUpEvent>(b, at);
        yield return Frames(3);
        VisualElement focused = b.focusController?.focusedElement as VisualElement;
        check("effect remover: a clicked Toggle All holds no focus", focused != b,
            $"focused={focused?.name ?? focused?.GetType().Name ?? "none"}");
        section.style.display = shown;
    }
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
        ContentScroll.ScrollTo(track);
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
    // A switch's track: Accent on, Off off (Facet's Looks.Switch).
    private static bool Shows(VisualElement track, bool on) {
        Color want = QuartzShell.Surface.Theme[on ? Role.Accent : Role.Off], got = track.style.backgroundColor.value;
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
    // The classic page's button hands off to this window, and its close hands back: a row written here shows on that page,
    // though a classic toggle never reads its field again. Written twice, so the second puts it back the same way.
    private static IEnumerator ClassicAfterHandBack(Action<string, bool, string> check) {
        var p = Probe();
        int state = NavRegistry.StateFor(FxPage);
        bool before = p.get(C), ok = true;
        var seen = new string[2];
        for(int i = 0; i < 2; i++) {
            QuartzShell.Hide();
            if(!UICore.IsOpen) UICore.Open(true);
            Factory.MenuFactory.SetState(state);   // its page built and shown, as the user sees it before the button
            yield return Frames(2);
            FacetBridge.OpenNewMenu(FxPage, fromClassic: true);
            yield return Frames(2);
            VisualElement track = Body.Q(name: p.key + "-input");
            if(track != null) Click(track);
            yield return Frames(2);
            QuartzShell.Hide();
            yield return Frames(2);
            if(UICore.CurrentMenuState != state) Factory.MenuFactory.SetState(state);
            var live = LiveToggle(p.key.ToLowerInvariant(), state);
            bool want = i == 0 ? !before : before;
            ok &= p.get(C) == want && live != null && live.Value == want && UICore.IsOpen && !FacetBridge.NewMenuVisible();
            seen[i] = $"field={p.get(C)} classic={live?.Value.ToString() ?? "none"} menu={UICore.IsOpen} window={FacetBridge.NewMenuVisible()}";
        }
        check("a row written in the window shows on the classic page its close hands back to, and so does writing it back", ok,
            $"{p.field} before={before}: {string.Join("; ", seen)}");
    }
    private static bool Near(Color a, Color b) =>
        Mathf.Abs(a.r - b.r) < 0.01f && Mathf.Abs(a.g - b.g) < 0.01f && Mathf.Abs(a.b - b.b) < 0.01f;
    private static IEnumerator MenuHandOff(Action<string, bool, string> check) {
        Surface s = QuartzShell.Surface;
        if(!s.Visible) FacetBridge.OpenNewMenu("visuals.effectremover", fromClassic: true);
        yield return Frames(2);
        bool hidden = !UICore.IsOpen;   // each hand-back below only counts when the menu was really gone
        Button close = QuartzShell.Window.TitleBarRight.Query<Button>().ToList().LastOrDefault();
        if(close != null) Submit(close);
        yield return Frames(2);
        check("the close button hides the window and brings the menu back", hidden && !s.Visible && UICore.IsOpen, $"hidden={hidden} window={s.Visible} menu={UICore.IsOpen}");
        FacetBridge.OpenNewMenu("visuals.effectremover", fromClassic: true);
        yield return Frames(2);
        check("reopening reuses the surface and hides the menu",
            ReferenceEquals(QuartzShell.Surface, s) && s.Visible && !UICore.IsOpen && !UICore.CanvasObj.activeSelf,
            $"same={ReferenceEquals(QuartzShell.Surface, s)} window={s.Visible} menu={UICore.IsOpen}");
        hidden = !UICore.IsOpen;
        UICore.EscapeDown();
        yield return Frames(3);
        check("Escape hides the window and brings the menu back", hidden && !s.Visible && UICore.IsOpen, $"hidden={hidden} window={s.Visible} menu={UICore.IsOpen}");
        FacetBridge.OpenNewMenu("visuals.effectremover", fromClassic: true);
        yield return Frames(2);
        hidden = !UICore.IsOpen;
        UICore.Toggle();   // the menu hotkey's action
        yield return Frames(2);
        check("the menu hotkey hides the window and the menu comes back once",
            hidden && !s.Visible && UICore.IsOpen && !UICore.IsReorganizing, $"hidden={hidden} window={s.Visible} menu={UICore.IsOpen} hub={UICore.IsReorganizing}");
        // Any other opener (the update toast's "open settings") hides the window rather than stacking the menu over it.
        FacetBridge.OpenNewMenu("visuals.effectremover", fromClassic: true);
        yield return Frames(2);
        hidden = !UICore.IsOpen;
        UICore.Open();
        yield return Frames(2);
        check("an opener hides the window and the menu comes back once", hidden && !s.Visible && UICore.IsOpen && UICore.CanvasObj.activeSelf,
            $"hidden={hidden} window={s.Visible} menu={UICore.IsOpen}");
        // The window's own mode dropdown rebuilds the menu behind it; with show-on-startup that re-runs an open.
        bool startup = MainCore.Conf.ShowOnStartup;
        MainCore.Conf.ShowOnStartup = true;
        try {
            FacetBridge.OpenNewMenu("visuals.effectremover", fromClassic: true);
            yield return Frames(2);
            UICore.Rebuild();
            yield return Frames(2);
            check("a menu rebuild behind the window keeps it up and the menu shut", s.Visible && !UICore.IsOpen && !UICore.CanvasObj.activeSelf,
                $"window={s.Visible} menu={UICore.IsOpen} canvas={UICore.CanvasObj.activeSelf}");
        } finally {
            MainCore.Conf.ShowOnStartup = startup;
        }
        // Opened from the settings panel over the hub (the hub's Settings button), it comes back there.
        QuartzShell.Hide();
        UICore.Close(true);
        UICore.OpenToHub();
        typeof(UICore).GetMethod("ShowPanelOverHub", BindingFlags.NonPublic | BindingFlags.Static)?.Invoke(null, null);
        yield return Frames(2);
        bool overHub = UICore.IsReorganizing && UICore.PanelInteractive;
        FacetBridge.OpenNewMenu("visuals.effectremover", fromClassic: true);
        yield return Frames(2);
        hidden = !UICore.IsOpen && !UICore.IsReorganizing;
        QuartzShell.Hide();
        yield return Frames(2);
        check("closing the window returns to the settings panel over the hub",
            overHub && hidden && UICore.IsOpen && UICore.IsReorganizing && UICore.PanelInteractive,
            $"before={overHub} hidden={hidden} menu={UICore.IsOpen} hub={UICore.IsReorganizing} panel={UICore.PanelInteractive}");
        UICore.Close(true);
    }
}
#endif

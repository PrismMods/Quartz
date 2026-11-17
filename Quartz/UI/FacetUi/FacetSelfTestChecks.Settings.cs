#if DEBUG && !IL2CPP
using System.Collections;
using System.Reflection;
using Facet;
using Quartz.Compat;
using Quartz.Core;
using Quartz.Prism;
using Quartz.UI.Nav;
using Quartz.UI.Utility;
using UnityEngine;
using UnityEngine.UIElements;
namespace Quartz.UI.FacetUi;
/// <summary>
/// The menu's Settings page: its cards in order, each switch, the sliders, the hotkey and its refusals, the rows only looked
/// at, and the language, last.
/// </summary>
internal static partial class FacetSelfTestChecks {
    private static readonly string[] SettingsCardKeys =
        ["LANGUAGE", "PRISM_GROUP_MENU", "MENU_GROUP_APPEARANCE", "FONT", "SETTINGS_FONT", "HEADING_ANIMATION", "MENU_GROUP_BEHAVIOUR", "MENU_GROUP_MAINTENANCE",
            "UPDATES", "CREDITS"];
    // Each switch row and its field.
    private static readonly (string row, Func<bool> get)[] SettingsSwitches = [
        ("SHOW_OVERLAYER_PANEL_AT_STARTUP", () => MainCore.Conf.ShowOnStartup),
        ("BLOCK_INPUTS_WHILE_MENU_OPEN", () => MainCore.Conf.BlockInputsWhileMenuOpen),
        ("SMOOTH_SCROLL", () => PrismBridge.SmoothScroll),   // PrismLib's: the page puts its settings file back
    ];

    private static IEnumerator SettingsPage(Action<string, bool, string> check) {
        IO.CoreSettings c = MainCore.Conf;
        var was = (c.ShowOnStartup, c.BlockInputsWhileMenuOpen, c.UIScale, c.PanelOpacity, c.ScrollSpeed, c.ToggleKey, c.ToggleModifier, c.Language,
            c.LanguageAutoDetected);
        string trLang = MainCore.Tr.Language;
        Action restore = PrismBridge.KeepPrismConfig();
        try {
            FacetBridge.OpenNewMenu(CorePages.SettingsPageKey);
            yield return Frames(3);
            SettingsCardOrder(check, "");
            yield return SettingsSwitchRows(check);
            yield return SettingsFromToast(check);
            yield return SettingsSliders(check);
            yield return SettingsSidebar(check);
            yield return SettingsHotkey(check);
            SettingsRest(check);
            yield return SettingsSmooth(check);
            yield return SettingsLanguage(check);
        } finally {
            restore();
            PrismBridge.Tick();
            MainCore.Tr.Language = trLang;
            c = MainCore.Conf;
            (c.ShowOnStartup, c.BlockInputsWhileMenuOpen, c.UIScale, c.PanelOpacity, c.ScrollSpeed, c.ToggleKey, c.ToggleModifier, c.Language,
                c.LanguageAutoDetected) = was;
            MainCore.ConfMgr.RequestSave();
            PrismBridge.RebindMenuKey();
            QuartzShell.ApplyLook();
            QuartzShell.Hide();
        }
        yield return Frames(3);
    }

    private static VisualElement SettingsCard(string key) => Body.Q(name: "menu-settings-" + key);

    // A card's title is its first label, upper-cased.
    private static void SettingsCardOrder(Action<string, bool, string> check, string when) {
        List<VisualElement> all = Body.Query<VisualElement>().ToList();
        var shown = new List<string>();
        bool ok = true;
        int last = -1;
        foreach(string key in SettingsCardKeys) {
            VisualElement card = SettingsCard(key);
            string caps = card?.Q<Label>()?.text, want = QuartzHost.Host.Translate(key).ToUpperInvariant();
            int at = card == null ? -1 : all.IndexOf(card);
            ok &= at > last && caps == want;
            last = at;
            shown.Add($"{caps}@{at}");
        }
        check($"Settings shows its cards in order{when}", ok, string.Join("|", shown));
    }

    // Each switch: a click flips its setting, and a second click puts it back.
    private static IEnumerator SettingsSwitchRows(Action<string, bool, string> check) {
        foreach(var (row, get) in SettingsSwitches) {
            if(row == "SMOOTH_SCROLL" && !PrismBridge.Available) continue;   // that switch is PrismLib's
            bool before = get();
            VisualElement track = Body.Q(name: row + "-input");
            if(track == null) { check($"{row}: its switch", false, "no " + row + "-input"); continue; }
            Click(track);
            yield return Frames(2);
            bool flipped = get() == !before;
            track = Body.Q(name: row + "-input");
            if(track != null) Click(track);
            yield return Frames(2);
            Step(check, $"{row}: a click flips its setting, and a second click puts it back",
                flipped && get() == before && FacetBridge.NewMenuVisible() && QuartzShell.CurrentPage == CorePages.SettingsPageKey,
                $"before={before} flipped={flipped} now={get()}");
        }
    }

    // The update toast's link opens this page.
    private static IEnumerator SettingsFromToast(Action<string, bool, string> check) {
        QuartzShell.Go(CorePages.HomePageKey);
        yield return Frames(2);
        typeof(UpdateToast).GetMethod("OpenSettings", Static).Invoke(null, null);
        yield return Frames(2);
        Step(check, "the update toast's link opens the Settings page",
            FacetBridge.NewMenuVisible() && QuartzShell.CurrentPage == CorePages.SettingsPageKey, null);
    }

    // A slider's track runs 0..1 over its range; setting it is a whole change, as the arrow keys make.
    private static IEnumerator SetSlider(string id, float min, float max, float v) {
        Slider s = Body.Q<Slider>(id + "-input");
        if(s != null) s.value = (v - min) / (max - min);
        yield return Frames(2);
    }

    private static IEnumerator SettingsSliders(Action<string, bool, string> check) {
        float was = MainCore.Conf.UIScale;
        yield return SetSlider("UI_SCALE", 0.8f, 1.6f, 1.2f);
        float ui = MainCore.Conf.UIScale, scale = QuartzShell.Window.Scale;
        check($"UI scale at 1.2 is saved, and scales the menu by 1.2 / 0.85 * {QuartzShell.BaseScale} (its base)",
            Mathf.Abs(ui - 1.2f) < 0.001f && Mathf.Abs(scale - (1.2f / 0.85f * QuartzShell.BaseScale)) < 0.001f, $"UIScale={ui} window={scale}");
        // An undo raises the slider's commit while it is on the panel; the setter applies the scale either way.
        string undid = History.NextUndo;
        History.Undo();
        yield return Frames(2);
        ui = MainCore.Conf.UIScale;
        scale = QuartzShell.Window.Scale;
        check($"undoing the UI scale puts back {was} in the setting and the menu ({was} / 0.85 * {QuartzShell.BaseScale})",
            was != 1.2f && Mathf.Abs(ui - was) < 0.001f && Mathf.Abs(scale - (was / 0.85f * QuartzShell.BaseScale)) < 0.001f, $"undid={undid} UIScale={ui} window={scale}");
        // Undone with the page switched away: the row is off the panel and raises no commit, so the setter alone applies it.
        yield return SetSlider("UI_SCALE", 0.8f, 1.6f, 1.2f);
        QuartzShell.Go(CorePages.HomePageKey);
        yield return Frames(2);
        undid = History.NextUndo;
        bool undone = Body.Q<Slider>("UI_SCALE-input") == null && History.Undo();
        yield return Frames(2);
        ui = MainCore.Conf.UIScale;
        scale = QuartzShell.Window.Scale;
        check($"undoing the UI scale from another page puts back {was} in the setting and the menu",
            undone && Mathf.Abs(ui - was) < 0.001f && Mathf.Abs(scale - (was / 0.85f * QuartzShell.BaseScale)) < 0.001f, $"undid={undid} undone={undone} UIScale={ui} window={scale}");
        QuartzShell.Go(CorePages.SettingsPageKey);
        yield return Frames(3);
        yield return SetSlider("WINDOW_OPACITY", 20f, 100f, 70f);
        float opacity = QuartzShell.Window.Root.resolvedStyle.opacity;
        check("window opacity at 70 is saved as 0.7, and the new menu takes it",
            Mathf.Abs(MainCore.Conf.PanelOpacity - 0.7f) < 0.001f && Mathf.Abs(opacity - 0.7f) < 0.01f, $"PanelOpacity={MainCore.Conf.PanelOpacity} window={opacity}");
        yield return SetSlider("SCROLL_SPEED", 20f, 300f, 120f);
        check("scroll speed at 120 is saved", MainCore.Conf.ScrollSpeed == 120f, $"ScrollSpeed={MainCore.Conf.ScrollSpeed}");
    }

    // A capture on the hotkey row, ended with `key` as the capture runner would end it (None: cancelled). Lines logged since
    // `mark` that say the hotkey was refused.
    // The menu hotkey would fire in the frame the last capture ended: the key just bound is still down then.
    private static bool hotkeyOnRelease;

    private static IEnumerator CaptureKey(KeyCode key, string mark, int[] refused) {
        MainCore.Log.Msg(mark);
        Button b = Body.Q<Button>("PRISM_MENU_HOTKEY-input");
        if(b != null) Submit(b);
        yield return Frames(1);
        bool capturing = KeyCaptureService.IsCapturing;
        typeof(KeyCaptureService).GetMethod("End", Static).Invoke(null, [key]);
        hotkeyOnRelease = UICore.ToggleHotkeyAllowed(true, true);
        yield return Frames(2);
        string[] lines = QuartzLogger.Recent();
        int at = Array.FindLastIndex(lines, l => l.Contains(mark));
        refused[0] = capturing ? lines.Skip(at + 1).Count(l => l.Contains("Menu hotkey refused")) : -1;
    }

    private static IEnumerator SettingsHotkey(Action<string, bool, string> check) {
        yield return PickItem("PRISM_MENU_MODIFIER", QuartzHost.Host.Translate(Keybind.ModifierName(Keybind.KeyModifier.Alt)));
        int[] n = [0];
        yield return CaptureKey(KeyCode.J, "[selftest] hotkey J", n);
        bool j = MainCore.Conf.ToggleModifier == (int)Keybind.KeyModifier.Alt && MainCore.Conf.ToggleKey == (int)KeyCode.J && n[0] == 0;
        check("the hotkey rows set Alt, then a captured J: the menu hotkey is Alt+J, and it does not fire in the capture's own frame", j && !hotkeyOnRelease,
            $"mod={MainCore.Conf.ToggleModifier} key={(KeyCode)MainCore.Conf.ToggleKey} refused={n[0]} firesOnRelease={hotkeyOnRelease}");
        yield return CaptureKey(KeyCode.D, "[selftest] hotkey D", n);
        check("a captured D (Alt+D, a Prism chord) is refused with one log line, and the hotkey stays Alt+J",
            MainCore.Conf.ToggleKey == (int)KeyCode.J && n[0] == 1, $"key={(KeyCode)MainCore.Conf.ToggleKey} refused={n[0]}");
        yield return CaptureKey(KeyCode.LeftShift, "[selftest] hotkey LeftShift", n);
        check("a captured LeftShift (a bare modifier) is refused with one log line, and the hotkey stays Alt+J",
            MainCore.Conf.ToggleKey == (int)KeyCode.J && n[0] == 1, $"key={(KeyCode)MainCore.Conf.ToggleKey} refused={n[0]}");
        yield return CaptureKey(KeyCode.None, "[selftest] hotkey cancel", n);
        check("a cancelled capture writes nothing and logs nothing", MainCore.Conf.ToggleKey == (int)KeyCode.J && n[0] == 0,
            $"key={(KeyCode)MainCore.Conf.ToggleKey} refused={n[0]}");
        yield return CaptureEscape(check);
    }

    // Escape during a capture, whichever polls it first: offered to Facet by another mod before Quartz's runner reads it,
    // the window keeps it; the runner then cancels on it, writing nothing. The new menu stays open either way.
    private static IEnumerator CaptureEscape(Action<string, bool, string> check) {
        int key = MainCore.Conf.ToggleKey;
        Button b = Body.Q<Button>("PRISM_MENU_HOTKEY-input");
        if(b != null) Submit(b);
        yield return Frames(1);
        bool capturing = KeyCaptureService.IsCapturing, blocking = FacetBridge.BlocksGameInput();
        bool took = FacetBridge.HandleEscape();
        bool kept = FacetBridge.NewMenuVisible() && KeyCaptureService.IsCapturing;
        if(blocking) PressNextFrame(KeyCode.Escape);   // the game reads keys too, unless the window blocks them
        yield return Frames(3);
        bool ended = !KeyCaptureService.IsCapturing;
        KeyCaptureService.Cancel();
        check("Escape during a hotkey capture: another mod offering it first leaves the new menu open, then the capture runner cancels on it",
            blocking && capturing && took && kept && ended && FacetBridge.NewMenuVisible() && MainCore.Conf.ToggleKey == key,
            $"blocking={blocking} capturing={capturing} took={took} kept={kept} ended={ended} key={(KeyCode)MainCore.Conf.ToggleKey} {Showing()}");
        if(FacetBridge.NewMenuVisible()) yield break;
        FacetBridge.OpenNewMenu(CorePages.SettingsPageKey);   // the checks after this one start from the open page
        yield return Frames(3);
    }

    // A key probe: Input.GetKeyDown(probeKey) reads as pressed in probeFrame only. Patched on first use, inert after.
    private static int probeFrame = -1;
    private static KeyCode probeKey;
    private static HarmonyLib.Harmony keyProbe;

    private static void PressNextFrame(KeyCode key) {
        if(keyProbe == null) {
            keyProbe = new HarmonyLib.Harmony("Quartz.FacetSelfTest");
            keyProbe.Patch(typeof(UnityEngine.Input).GetMethod(nameof(UnityEngine.Input.GetKeyDown), [typeof(KeyCode)]),
                prefix: new HarmonyLib.HarmonyMethod(typeof(FacetSelfTestChecks).GetMethod(nameof(ProbeKeyDown), Static)));
        }
        probeKey = key;
        probeFrame = Time.frameCount + 1;
    }

    private static bool ProbeKeyDown(KeyCode __0, ref bool __result) {
        if(__0 != probeKey || Time.frameCount != probeFrame) return true;
        __result = true;
        return false;
    }

    private static void SettingsRest(Action<string, bool, string> check) {
        bool resets = Body.Q(name: "HOME_RESET_ALL") != null && Body.Q(name: "HOME_RESET_CLEAN") != null;
        check("the two reset rows are there (not clicked)", resets, null);
        VisualElement anim = Body.Q(name: "MENU_ANIM");
        bool prism = PrismBridge.Available && PrismBridge.UiAvailable;
        check("the animation row shows exactly when Prism · Settings can open", anim != null && Displayed(anim) == prism, $"row={anim != null} prism={prism}");
        string[] credits = ["CREDITS_TEAM", "CREDITS_TESTERS", "CREDITS_PORTS", "CREDITS_DONORS"];
        VisualElement card = SettingsCard("CREDITS");
        var bad = credits.Where(k => card?.Q<TextElement>(k + "-input")?.text != QuartzHost.Translate(k + "_BODY")).ToList();
        check("the Credits card holds its four rows, each showing its text", card != null && bad.Count == 0, string.Join(",", bad));
        // Its licence note, in the header, ends on the rows' right edge as their text does.
        string license = QuartzHost.Translate("CREDITS_LICENSE");
        Label note = card?.Query<Label>().Where(l => l.text == license).First();
        VisualElement value = card?.Q<TextElement>("CREDITS_TEAM-input");
        check("the Credits card's licence note ends on the rows' right edge (±1)",
            note != null && value != null && Mathf.Abs(note.worldBound.xMax - value.worldBound.xMax) <= 1f,
            $"note={note?.worldBound.xMax:0.0} row={value?.worldBound.xMax:0.0}");
    }

    // The open rail's width, which a drag of its handle may have changed.
    private static float RailFull => (float)typeof(Facet.Nav).GetField("_railWidth", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(QuartzShell.Nav);
    // The sidebar toggle in the window's title bar; a hidden one counts as none.
    private static VisualElement RailButton => QuartzShell.Window?.Root.Q(name: "sidebar-toggle") is { } b && Displayed(b) ? b : null;
    // Layout puts each edge on a whole screen pixel, so a width reads up to a pixel off the one set (at 1.287 px/pt the 36 pt
    // strip lays out as 47 px, 36.5 pt): one pixel, in points.
    private static float RailTol => 1.01f / PanelPx(RailScroll);

    // Waits up to a second for the rail to land on a width, and reads it.
    private static IEnumerator RailSettles(float width, float[] got) {
        for(float t0 = Time.realtimeSinceStartup; Time.realtimeSinceStartup - t0 < 1f && Mathf.Abs(RailScroll.resolvedStyle.width - width) > RailTol;) yield return null;
        got[0] = RailScroll.resolvedStyle.width;
    }

    // The Sidebar row, picked as a click would, applies at once. Toggleable shows the rail open with a button that folds it to
    // its icons, kept through a rebuild and a reopen; Always visible has no button; Always hidden is the icon strip, which
    // widens over the page while the pointer rests on it, without moving the page, and narrows when it leaves.
    private static IEnumerator SettingsSidebar(Action<string, bool, string> check) {
        string mode = QuartzHost.Host.Read(QuartzShell.SidebarStore), compact = QuartzHost.Host.Read(QuartzShell.CompactStore);
        Sizes sz = QuartzHost.Host.Theme.Sizes;
        float strip = 2f * 10f + sz.Icon, full = RailFull;
        var got = new float[1];
        try {
            QuartzHost.Host.Write(QuartzShell.CompactStore, null);
            yield return PickItem("MENU_SIDEBAR", QuartzHost.Translate("MENU_SIDEBAR_TOGGLE"));
            yield return RailSettles(full, got);
            bool button = RailButton != null && Displayed(RailButton);
            check("Sidebar, Toggleable: the rail is open with its button", QuartzShell.SidebarMode == 0 && button && !QuartzShell.Nav.Compact && Mathf.Abs(got[0] - full) <= RailTol,
                $"mode={QuartzShell.SidebarMode} button={button} width={got[0]:0.0}/{full:0.0}");
            if(RailButton != null) Click(RailButton);
            yield return RailSettles(strip, got);
            string kept = QuartzHost.Host.Read(QuartzShell.CompactStore);
            check("its button folds the rail to the icon strip, and the fold is kept", QuartzShell.Nav.Compact && kept == "1" && Mathf.Abs(got[0] - strip) <= RailTol,
                $"kept={kept} width={got[0]:0.0}/{strip:0.0}");
            // As an added or removed category rebuilds the rail, then a close and an open.
            typeof(QuartzShell).GetMethod("BuildNav", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
            QuartzShell.Go(CorePages.SettingsPageKey);
            yield return Frames(2);
            bool rebuilt = QuartzShell.Nav.Compact && RailButton != null;
            QuartzShell.Hide();
            yield return Frames(2);
            FacetBridge.OpenNewMenu(CorePages.SettingsPageKey);
            yield return RailSettles(strip, got);
            check("the fold survives a rebuild of the rail and a reopen", rebuilt && QuartzShell.Nav.Compact && RailButton != null && Mathf.Abs(got[0] - strip) <= RailTol,
                $"rebuilt={rebuilt} width={got[0]:0.0}");
            if(RailButton != null) Click(RailButton);
            yield return RailSettles(full, got);
            check("the button opens it again, and the open rail is kept", !QuartzShell.Nav.Compact && QuartzHost.Host.Read(QuartzShell.CompactStore) == null
                && Mathf.Abs(got[0] - full) <= RailTol, $"width={got[0]:0.0}");
            yield return RailDragStops(check, "Toggleable");
            QuartzHost.Host.Write(QuartzShell.CompactStore, "1");   // a fold kept from Toggleable: the other modes ignore it
            yield return PickItem("MENU_SIDEBAR", QuartzHost.Translate("MENU_SIDEBAR_SHOWN"));
            yield return RailSettles(full, got);
            check("Sidebar, Always visible: the rail is open, with no button", QuartzShell.SidebarMode == 1 && !QuartzShell.Nav.Compact && RailButton == null
                && Mathf.Abs(got[0] - full) <= RailTol, $"button={RailButton != null} width={got[0]:0.0}");
            yield return RailDragStops(check, "Always visible");
            yield return PickItem("MENU_SIDEBAR", QuartzHost.Translate("MENU_SIDEBAR_HIDDEN"));
            yield return RailSettles(strip, got);
            check("Sidebar, Always hidden: the rail is the icon strip, with no button", QuartzShell.SidebarMode == 2 && QuartzShell.Nav.Compact && RailButton == null
                && Mathf.Abs(got[0] - strip) <= RailTol, $"button={RailButton != null} width={got[0]:0.0}");
            VisualElement page = Body;
            float x = page.worldBound.xMin;
            Leave(RailScroll);
            Hover(RailScroll);
            yield return RailSettles(full, got);
            bool still = Mathf.Abs(page.worldBound.xMin - x) <= 0.5f;
            check("resting on the strip widens it over the page, which does not move", Mathf.Abs(got[0] - full) <= RailTol && still,
                $"width={got[0]:0.0}/{full:0.0} page={x:0.0}->{page.worldBound.xMin:0.0}");
            Leave(RailScroll);
            yield return RailSettles(strip, got);
            check("leaving it narrows it again", Mathf.Abs(got[0] - strip) <= RailTol, $"width={got[0]:0.0}");
        } finally {
            QuartzHost.Host.Write(QuartzShell.CompactStore, compact);
            QuartzHost.Host.Write(QuartzShell.SidebarStore, mode);
            QuartzShell.ApplySidebar();
        }
        yield return Frames(2);
    }

    // The Sidebar setting alone decides whether the rail shows: its handle dragged far left stops at the rail's minimum width
    // instead of shutting it. A second drag puts the width back. The rail's open animation writes its width each frame until
    // it ends, over a drag's, so the drag waits for the width the animation writes to stay put for three frames.
    private static IEnumerator RailDragStops(Action<string, bool, string> check, string mode) {
        var handle = (VisualElement)typeof(Facet.Nav).GetField("_handle", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(QuartzShell.Nav);
        float last = float.NaN;
        for(int still = 0, f = 0; still < 3 && f < 120; f++) {
            float now = RailScroll.style.width.value.value;
            still = now == last ? still + 1 : 0;
            last = now;
            yield return null;
        }
        float was = RailScroll.resolvedStyle.width;
        DragHandle(handle, -400f);
        yield return Frames(2);
        bool open = QuartzShell.Nav.RailOpen;
        float w = RailScroll.resolvedStyle.width;
        check($"Sidebar, {mode}: dragging the rail's handle far left stops at its minimum width",
            open && Displayed(RailScroll) && Mathf.Abs(w - Facet.Nav.MinRail) <= RailTol, $"open={open} width={w:0.0}/{Facet.Nav.MinRail}");
        DragHandle(handle, was - (open ? w : 0f));   // from a shut rail a drag starts at no width
        var got = new float[1];
        yield return RailSettles(was, got);
    }

    // Down, a move by dx and up in one frame, so the real pointer's moves cannot land between them.
    private static void DragHandle(VisualElement handle, float dx) {
        Vector2 at = handle.worldBound.center;
        Pointer<PointerDownEvent>(handle, at);
        Pointer<PointerMoveEvent>(handle, at + new Vector2(dx, 0f));
        Pointer<PointerUpEvent>(handle, at + new Vector2(dx, 0f));
    }

    private static void Pointer<T>(VisualElement e, Vector2 at) where T : PointerEventBase<T>, new() {
        using T ev = Pooled<T>();
        typeof(PointerEventBase<T>).GetProperty("button").SetValue(ev, 0);
        typeof(PointerEventBase<T>).GetProperty("position").SetValue(ev, (Vector3)at);
        ev.target = e;
        e.SendEvent(ev);
    }

    private static void Leave(VisualElement e) {
        using MouseLeaveEvent ev = Pooled<MouseLeaveEvent>();
        ev.target = e;
        e.SendEvent(ev);
    }

    // Picked from the row, as a click would: both languages follow and the card titles relabel.
    private static IEnumerator SettingsLanguage(Action<string, bool, string> check) {
        string ko = MainCore.Tr.GetForLanguage("0NATIVELANG", "ko-KR", "ko-KR") + " (ko-KR)";
        yield return PickItem("LANGUAGE", ko);
        string want = MainCore.Tr.GetForLanguage("MENU_GROUP_APPEARANCE", "ko-KR", "?").ToUpperInvariant();
        int frames = 0;
        for(; frames < 30 && SettingsCard("MENU_GROUP_APPEARANCE")?.Q<Label>()?.text != want; frames++) yield return null;
        check("Language set to Korean sets both the translator's and the settings' language",
            MainCore.Tr.Language == "ko-KR" && MainCore.Conf.Language == "ko-KR", $"tr={MainCore.Tr.Language} conf={MainCore.Conf.Language} frames={frames}");
        SettingsCardOrder(check, ", relabelled in Korean");
    }
}
#endif

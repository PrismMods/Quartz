#if DEBUG && !IL2CPP
using System.Collections;
using System.Reflection;
using Facet;
using Quartz.Core;
using Quartz.Features;
using Quartz.UI.Nav;
using Quartz.UI.Objects.Impl;
using Quartz.UI.Utility;
using UnityEngine;
using UnityEngine.UIElements;
using HandOff = Quartz.UI.MenuHandOff;
namespace Quartz.UI.FacetUi;
/// <summary>
/// What every moved Overlay page is checked for against its classic page: the same rows, a switch flipped here that the
/// classic page then shows, and a hotkey bound here that fires from the value written here. Each page's own checks are in
/// FacetSelfTestChecks.OverlayPages.&lt;Page&gt;.cs.
/// </summary>
internal static partial class FacetSelfTestChecks {
    /// <summary>The open page is pageKey drawn in Facet, and each key names a shown row on it (its Id, or its input's): the row
    /// and everything around it up to the page display. A fold the rows sit in is opened first, as a player opens it, and shut
    /// again after; a switch's group is not, so the caller makes every row's switch and VisibleWhen hold first. hidden names
    /// rows that must not show.</summary>
    internal static IEnumerator Parity(Action<string, bool, string> check, string pageKey, string[] keys, string[] hidden = null) {
        VisualElement page = PageContent();
        VisualElement Row(string k) => page.Q(name: k) ?? page.Q(name: k + "-input");
        List<Action<bool, bool>> opened = [];
        foreach(string k in keys)
            for(VisualElement e = Row(k)?.parent; e != null && e != page; e = e.parent)
                if(FindFold.Invoke(null, [e]) is { } fold && !((Func<bool>)FoldIsOpen.GetValue(fold))()) {
                    var set = (Action<bool, bool>)FoldSet.GetValue(fold);
                    set(true, false);
                    opened.Add(set);
                }
        yield return Frames(1);   // the resolved styles catch up
        bool Shown(string k) {
            VisualElement e = Row(k);
            for(; e != null && e != page; e = e.parent) if(e.resolvedStyle.display != DisplayStyle.Flex) return false;
            return e == page;
        }
        List<string> missing = [.. keys.Where(k => !Shown(k))], shows = [.. (hidden ?? []).Where(Shown)];
        foreach(Action<bool, bool> set in opened) set(false, false);
        bool moved = QuartzShell.CurrentPage == pageKey && page.Q(name: "menu-classic") == null;
        check($"{pageKey}: drawn in the new menu with every row of the classic page ({keys.Length})"
            + (hidden == null ? "" : $", and none of the {hidden.Length} it hides"), moved && missing.Count == 0 && shows.Count == 0,
            $"page={QuartzShell.CurrentPage} classicCard={!moved} missing={string.Join(",", missing)} shown={string.Join(",", shows)} unfolded={opened.Count}");
    }

    // Facet's record of a fold (an Advanced or a Section body), internal to it.
    private static readonly Type FoldType = typeof(Controls).GetNestedType("Fold", BindingFlags.NonPublic);
    private static readonly MethodInfo FindFold = typeof(Data).GetMethod(nameof(Data.Find)).MakeGenericMethod(FoldType);
    private static readonly FieldInfo FoldSet = FoldType.GetField("Set"), FoldIsOpen = FoldType.GetField("IsOpen");

    /// <summary>A switch flipped on the open moved page shows on the live classic page opened next; a second flip puts it back.
    /// The classic toggle's id is the row's key in lower case. field writes the setting as its row does, for the restore.</summary>
    internal static IEnumerator ClassicSees(Action<string, bool, string> check, string pageKey, string toggleKey, Bind<bool> field) {
        string tag = $"{pageKey} {toggleKey}", classicId = toggleKey.ToLowerInvariant();
        int state = NavRegistry.StateFor(pageKey);
        bool before = field.Get();
        VisualElement track = Body.Q(name: toggleKey + "-input");
        if(track == null) {
            check($"{tag}: the classic page shows a switch flipped here", false, "no " + toggleKey + "-input");
            yield break;
        }
        try {
            Click(track);
            yield return Frames(2);
            bool flipped = field.Get() == !before;
            QuartzShell.OpenClassic(state);
            yield return Frames(2);
            UIToggle live = LiveToggle(classicId, state);
            bool shows = live != null && live.Value == field.Get();
            UICore.Close();
            yield return Frames(2);
            track = Body.Q(name: toggleKey + "-input");
            if(track != null) Click(track);
            yield return Frames(2);
            Step(check, $"{tag}: a flip here shows on the classic page opened next, and a second flip puts it back",
                flipped && shows && field.Get() == before && FacetBridge.NewMenuVisible() && QuartzShell.CurrentPage == pageKey,
                $"before={before} flipped={flipped} classic={live?.Value.ToString() ?? "none"} now={field.Get()}");
        } finally {
            PutBack(field, before, classicId);
        }
    }

    // The player's value, written straight into the setting whatever the row or the menus show now, as a write from a row
    // leaves it: the classic menu rebuilds before it next shows, and its live toggles show the value.
    private static void PutBack(Bind<bool> field, bool before, string classicId) {
        if(field.Get() == before) return;
        field.Set(before);
        UICore.ClassicStale = true;
        ToggleBinds.Show(classicId, before);
    }

    /// <summary>The open moved page's switch toggleKey, written here first, then bound through its context menu: the key
    /// captured binds classicId, fires from the value written here, and the row follows within 150 ms; the menu then clears
    /// it, and offers Clear again for a bind made in the classic menu. descKey is the row's description (null: none), which
    /// its tooltip shows translated above the key. A bind of the player's and the setting are put back.</summary>
    internal static IEnumerator Hotkey(Action<string, bool, string> check, string pageKey, string toggleKey, string classicId, string descKey,
        Bind<bool> field) {
        const KeyCode key = KeyCode.F13;
        string tag = $"{pageKey} {toggleKey}", shown = Keybind.Format(Keybind.KeyModifier.None, key);
        VisualElement row = Body.Q(name: toggleKey), track = Body.Q(name: toggleKey + "-input");
        if(row == null || track == null) {
            check($"{tag}: a hotkey bound here", false, $"row={row != null} track={track != null}");
            yield break;
        }
        bool before = field.Get(), had = ToggleBinds.TryGet(classicId, out Keybind.KeyModifier hadMod, out KeyCode hadKey);
        if(had) ShellRows.ClearHotkey(classicId);
        string set = QuartzHost.Host.Translate("MENU_SET_HOTKEY"), clear = QuartzHost.Host.Translate("MENU_CLEAR_HOTKEY");
        // The description in the player's language: a key with no translation would come back as the key itself.
        string desc = descKey == null ? "" : QuartzHost.Host.Translate(descKey);
        bool described = descKey == null || (desc.Length > 0 && desc != descKey && MainCore.Tr.HasKeyForLanguage(descKey, "en-US"));
        Hover(row);
        bool kept = described && (row.tooltip ?? "") == desc;
        try {
            // A write from here first: the hotkey flips from it, not from what the classic toggle showed before it.
            Click(track);
            yield return Frames(2);
            bool wrote = field.Get() == !before;
            UICore.ClassicStale = false;   // whether the bind marks the classic page stale is read below
            RightClick(row);
            yield return Frames(2);
            VisualElement setRow = MenuRow(set);
            bool offersClear = MenuRow(clear) != null;
            if(setRow != null) Pick(setRow);
            yield return Frames(1);
            bool capturing = KeyCaptureService.IsCapturing, prompt = Popped("menu-hotkey-press") != null;
            typeof(KeyCaptureService).GetMethod("End", Static).Invoke(null, [key]);   // as the capture runner ends it
            yield return Frames(2);
            bool bound = ToggleBinds.TryGet(classicId, out Keybind.KeyModifier mod, out KeyCode got) && got == key && mod == Keybind.KeyModifier.None;
            bool prompted = prompt && Popped("menu-hotkey-press") == null, stale = UICore.ClassicStale;
            Hover(row);
            string tip = row.tooltip, want = desc.Length == 0 ? shown : desc + "\n" + shown;
            check($"{tag}: right-click offers Set hotkey and no Clear, a prompt shows while it captures, and the key captured binds {classicId}, "
                + "marks the classic page stale and shows in the tooltip under the description",
                kept && wrote && setRow != null && !offersClear && capturing && prompted && bound && stale && tip == want,
                $"kept={kept} described={described} wrote={wrote} set={setRow != null} clear={offersClear} capturing={capturing} prompt={prompt} bound={got}/{mod} "
                + $"stale={stale} tip='{tip}' want='{want}'");
            float t0 = Time.realtimeSinceStartup, ms = -1f;
            PressNextFrame(key);
            while(Time.realtimeSinceStartup - t0 < 1f) {
                yield return null;
                if(field.Get() == before && Shows(track, before)) {
                    ms = (Time.realtimeSinceStartup - t0) * 1000f;
                    break;
                }
            }
            check($"{tag}: the bound key flips the setting back from the value written here, and the row shows it within 150 ms",
                ms >= 0f && ms <= 150f, $"ms={ms:0} field={field.Get()} before={before} shows={Shows(track, field.Get())}");
            RightClick(row);
            yield return Frames(2);
            VisualElement clearRow = MenuRow(clear);
            if(clearRow != null) Pick(clearRow);
            yield return Frames(1);
            Hover(row);
            check($"{tag}: once bound, right-click offers Clear hotkey, which removes the bind and its tooltip line",
                clearRow != null && !ToggleBinds.TryGet(classicId, out _, out _) && row.tooltip == desc, $"clear={clearRow != null} tip='{row.tooltip}'");
            // The menu is offered afresh as the pointer comes over the row: a bind the classic menu makes meanwhile shows there.
            ToggleBinds.Set(classicId, Keybind.KeyModifier.None, key);
            Hover(row);
            RightClick(row);
            yield return Frames(2);
            clearRow = MenuRow(clear);
            if(clearRow != null) Pick(clearRow);
            yield return Frames(1);
            check($"{tag}: a bind made in the classic menu shows in the row's menu once the pointer comes over it, and clears from there",
                clearRow != null && !ToggleBinds.TryGet(classicId, out _, out _), $"clear={clearRow != null}");
        } finally {
            KeyCaptureService.Cancel();
            Popup.CloseAll();
            if(had) ShellRows.SetHotkey(classicId, hadMod, hadKey);
            else if(ToggleBinds.TryGet(classicId, out _, out _)) ShellRows.ClearHotkey(classicId);
            PutBack(field, before, classicId);
        }
    }

    /// <summary>macOS: an Option+letter chord binds through the row's context menu. Under Option, Unity reports the Option key's
    /// keydown and no letter's; the letter's comes from the window server, faked here. The bind must not fire as it is made.</summary>
    internal static IEnumerator OptionChord(Action<string, bool, string> check, string pageKey, string toggleKey, string classicId, Bind<bool> field) {
        if(!Keybind.IsMac) yield break;   // elsewhere Unity reports the letter, as the hotkey check covers
        const KeyCode key = KeyCode.L;   // bound by nothing on the rig (the menu's own hotkey is Option+K there)
        string tag = $"{pageKey} {toggleKey}";
        VisualElement row = Body.Q(name: toggleKey);
        bool before = field.Get(), had = ToggleBinds.TryGet(classicId, out Keybind.KeyModifier hadMod, out KeyCode hadKey);
        if(had) ShellRows.ClearHotkey(classicId);
        try {
            if(row != null) RightClick(row);
            yield return Frames(2);
            VisualElement setRow = MenuRow(QuartzHost.Host.Translate("MENU_SET_HOTKEY"));
            if(setRow != null) Pick(setRow);
            yield return Frames(1);
            FakeMacKeys(true, KeyCode.None);   // Option goes down: the window server sees it, and Unity reports its keydown,
            typeof(KeyCaptureService).GetMethod("End", Static).Invoke(null, [KeyCode.LeftAlt]);   // which ends the capture as the runner does
            yield return Frames(2);
            bool waits = KeyCaptureService.IsCapturing && Popped("menu-hotkey-press") != null;
            FakeMacKeys(true, key);   // the letter goes down next frame, seen by the window server only
            int frames = 0;
            for(; frames < 10 && KeyCaptureService.IsCapturing; frames++) yield return null;
            bool bound = ToggleBinds.TryGet(classicId, out Keybind.KeyModifier mod, out KeyCode got) && got == key && mod == Keybind.KeyModifier.Alt;
            check($"{tag}: an Option+letter chord, whose letter Unity never reports, binds through the context menu without firing",
                row != null && setRow != null && waits && bound && Popped("menu-hotkey-press") == null && field.Get() == before,
                $"row={row != null} set={setRow != null} waits={waits} frames={frames} capturing={KeyCaptureService.IsCapturing} bound={got}/{mod} "
                + $"field={field.Get()} before={before} focused={Application.isFocused}");
        } finally {
            FakeMacKeys(false, KeyCode.None);
            KeyCaptureService.Cancel();
            Popup.CloseAll();
            if(had) ShellRows.SetHotkey(classicId, hadMod, hadKey);
            else if(ToggleBinds.TryGet(classicId, out _, out _)) ShellRows.ClearHotkey(classicId);
            PutBack(field, before, classicId);   // a bind that fired as it was made flipped it
        }
    }

    /// <summary>Runs checks on the moved page pageKey, opened from a closed menu with Overlay Core and featureId on. show makes
    /// every row a switch hides show; restore puts the player's values back, whatever happens. The features and the menu are
    /// put back as found.</summary>
    internal static IEnumerator OnPage(string featureId, string pageKey, Action show, Action restore, Func<IEnumerator> checks) {
        bool wasOpen = UICore.IsOpen;
        // Every feature is read before any is flipped, and put back in reverse: "overlay" is the feature's dependency.
        var saved = FeatureService.Features.Select(h => (id: h.Id, was: h.Enabled, had: FeatureService.State.Features.TryGetValue(h.Id, out bool v), value: v)).ToList();
        if(wasOpen) UICore.Close(true);
        HandOff.ReturnTo = HandOff.Menu.None;
        try {
            foreach(string id in new[] { "overlay", featureId }) if(!FeatureService.IsEnabled(id)) FeatureService.SetEnabled(id, true);
            yield return Frames(3);
            show();
            FacetBridge.OpenNewMenu(pageKey);
            yield return Frames(3);
            yield return checks();
        } finally {
            restore();
            HandOff.ReturnTo = HandOff.Menu.None;
            QuartzShell.Hide();
            if(UICore.IsOpen) UICore.Close(true);
            for(int i = saved.Count - 1; i >= 0; i--) RestoreFeature(saved[i].id, saved[i].was, saved[i].had, saved[i].value);
            if(wasOpen) UICore.Open(true);
        }
        yield return Frames(3);
    }

    /// <summary>The open page's two columns: side by side, the page's parts share the header's edges, the settings cards their
    /// own and the preview ends on the header's right edge. The same once the window is narrowed until the columns stack.</summary>
    internal static IEnumerator ColumnEdges(Action<string, bool, string> check, string pageKey, string overlayId) {
        VisualElement page = PageContent(), right = page.Q(name: overlayId + "-preview-pane")?.parent, left = right?.parent?[0];
        if(right == null || left == null || left == right) {
            check($"{pageKey}: the settings and the preview sit in two columns", false, $"pane={right != null}");
            yield break;
        }
        bool Stacked() => right.worldBound.yMin >= left.worldBound.yMax - 1f;
        bool side = !Stacked();
        string misfits = EdgeMisfits(page, left, right);
        check($"{pageKey}: side by side, the page's parts share the header's edges, the cards their own, and the preview ends on the header's right (±1)",
            side && misfits == "", $"side={side} {misfits}");
        Rect was = QuartzShell.Window.Rect;
        try {
            // The window's layout width is its rect over its scale; the columns stack below 760 of theirs.
            float columns = right.parent.layout.width, cut = (columns - 700f) * QuartzShell.Window.Scale;
            QuartzShell.Window.Rect = new Rect(was.x, was.y, was.width - cut, was.height);
            int frames = 0;
            for(; frames < 10 && !Stacked(); frames++) yield return null;
            yield return Frames(2);
            misfits = EdgeMisfits(page, left, right);
            check($"{pageKey}: narrowed below 760, the preview stacks under the cards and the edges still hold (±1)", Stacked() && misfits == "",
                $"stacked={Stacked()} frames={frames} width={right.parent.layout.width:0} {misfits}");
        } finally {
            QuartzShell.Window.Rect = was;
        }
        yield return Frames(2);
    }

    // A colour row's red, green and blue, set through its picker's channel sliders as dragging them would; alpha stays.
    private static bool SetChannels(string key, Color c) {
        List<Slider> channels = QuartzShell.Surface?.Root.Q(name: key)?.Query<Slider>().ToList();
        if(channels == null || channels.Count < 3) return false;
        (channels[0].value, channels[1].value, channels[2].value) = (c.r, c.g, c.b);
        return true;
    }

    private static bool SameRgb(Color a, Color b) => Mathf.Abs(a.r - b.r) < 0.01f && Mathf.Abs(a.g - b.g) < 0.01f && Mathf.Abs(a.b - b.b) < 0.01f;

    // The window server's keys, as Keybind reads them through PrismBridge, while faked: Option held, and `macKey` from the
    // frame after it was set. Patched on first use, inert after.
    private static bool macFake, macPatched;
    private static KeyCode macKey;
    private static int macKeyFrame;

    private static void FakeMacKeys(bool on, KeyCode key) {
        if(!macPatched) {
            macPatched = true;
            new HarmonyLib.Harmony("Quartz.FacetSelfTest.MacKeys").Patch(typeof(Quartz.Prism.PrismBridge).GetMethod("TryPhysical", Static),
                prefix: new HarmonyLib.HarmonyMethod(typeof(FacetSelfTestChecks).GetMethod(nameof(FakePhysical), Static)));
        }
        macFake = on;
        macKey = key;
        macKeyFrame = Time.frameCount + 1;
    }

    private static bool FakePhysical(KeyCode key, ref bool held, ref bool __result) {
        if(!macFake || (key != KeyCode.LeftAlt && key != macKey)) return true;
        held = key == KeyCode.LeftAlt || Time.frameCount >= macKeyFrame;
        __result = true;
        return false;
    }

    // A right-click on a row, which opens its context menu.
    private static void RightClick(VisualElement e) => Press(e, 1);

    // A mouse button going down on a row: 1 is the right button, 2 the middle.
    private static void Press(VisualElement e, int button) {
        using PointerDownEvent ev = Pooled<PointerDownEvent>();
        typeof(PointerEventBase<PointerDownEvent>).GetProperty("button").SetValue(ev, button);
        ev.target = e;
        e.SendEvent(ev);
    }

    private static void Hover(VisualElement e) {
        using MouseEnterEvent ev = Pooled<MouseEnterEvent>();
        ev.target = e;
        e.SendEvent(ev);
    }

    // The open popup menu's row reading `shown`; menus live on Facet's popup surface, a document of its own.
    private static VisualElement MenuRow(string shown) {
        foreach(UIDocument d in UnityEngine.Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None))
            if(d.rootVisualElement?.Query<Label>().Where(l => l.text == shown && l.panel != null).First() is { } label) return label.parent;
        return null;
    }

    // Picks a popup menu's row, as a click would: the menu takes the primary button's release.
    private static void Pick(VisualElement row) {
        using PointerUpEvent ev = Pooled<PointerUpEvent>();
        typeof(PointerEventBase<PointerUpEvent>).GetProperty("button").SetValue(ev, 0);
        ev.target = row;
        row.SendEvent(ev);
    }
}
#endif

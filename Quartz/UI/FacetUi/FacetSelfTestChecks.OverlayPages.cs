#if DEBUG && !IL2CPP
using System.Collections;
using Facet;
using Quartz.Core;
using Quartz.UI.Nav;
using Quartz.UI.Objects.Impl;
using Quartz.UI.Utility;
using UnityEngine;
using UnityEngine.UIElements;
namespace Quartz.UI.FacetUi;
/// <summary>
/// What every moved Overlay page is checked for against its classic page: the same rows, a switch flipped here that the
/// classic page then shows, and a hotkey bound here that fires from the value written here. Each page's own checks are in
/// FacetSelfTestChecks.OverlayPages.&lt;Page&gt;.cs.
/// </summary>
internal static partial class FacetSelfTestChecks {
    /// <summary>The open page is pageKey drawn in Facet, and each key names a shown row on it (its Id, or its input's). The
    /// caller makes every row's VisibleWhen hold first.</summary>
    internal static void Parity(Action<string, bool, string> check, string pageKey, string[] keys) {
        VisualElement page = PageContent();
        List<string> missing = [.. keys.Where(k => (page.Q(name: k) ?? page.Q(name: k + "-input")) is not { } e || e.style.display == DisplayStyle.None)];
        bool moved = QuartzShell.CurrentPage == pageKey && page.Q(name: "menu-classic") == null;
        check($"{pageKey}: drawn in the new menu with every row of the classic page ({keys.Length})", moved && missing.Count == 0,
            $"page={QuartzShell.CurrentPage} classicCard={!moved} missing={string.Join(",", missing)}");
    }

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
    private static void RightClick(VisualElement e) {
        using PointerDownEvent ev = Pooled<PointerDownEvent>();
        typeof(PointerEventBase<PointerDownEvent>).GetProperty("button").SetValue(ev, 1);
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

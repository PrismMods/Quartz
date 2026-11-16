#if DEBUG && !IL2CPP
using System.Collections;
using System.Reflection;
using Facet;
using Quartz.Core;
using Quartz.Features;
using Quartz.Overlay;
using Quartz.UI.Nav;
using Quartz.UI.Utility;
using UnityEngine;
using UnityEngine.UIElements;
namespace Quartz.UI.FacetUi;
/// <summary>
/// What every Overlay page is checked for: its rows, a switch that flips and flips back, and a hotkey bound here that fires
/// from the value written here. Each page's own checks are in
/// FacetSelfTestChecks.OverlayPages.&lt;Page&gt;.cs.
/// </summary>
internal static partial class FacetSelfTestChecks {
    /// <summary>The open page is pageKey drawn in Facet, and each key names a shown row on it (its Id, or its input's): the row
    /// and everything around it up to the page display. A fold the rows sit in is opened first, as a player opens it, and shut
    /// again after; a switch's group is not, so the caller makes every row's switch and VisibleWhen hold first. hidden names
    /// rows that must not show, their folds opened too: a row in a shut fold would not show whatever hides it. A page with its
    /// own tab row is read tab by tab (only the selected tab is built), and its tab is put back after.</summary>
    internal static IEnumerator Parity(Action<string, bool, string> check, string pageKey, string[] keys, string[] hidden = null) {
        VisualElement page = PageContent();
        VisualElement Row(string k) => page.Q(name: k) ?? page.Q(name: k + "-input");
        bool Shown(string k) {
            VisualElement e = Row(k);
            for(; e != null && e != page; e = e.parent) if(e.resolvedStyle.display != DisplayStyle.Flex) return false;
            return e == page;
        }
        bool folds = Folds(out string miss);
        if(!folds) check($"{pageKey}: Facet's fold record is found, so the rows in folds are opened before they are read", false, miss);
        global::Facet.Tabs tabs = PageTabs();
        int was = tabs?.Selected ?? -1, unfolded = 0;
        HashSet<string> seen = [];
        for(int t = 0; t < (tabs == null ? 1 : 16); t++) {
            if(tabs != null) {
                tabs.Select(t);
                if(tabs.Selected != t) continue;   // past the last tab, or a hidden one
            }
            List<Action<bool, bool>> opened = [];
            if(folds)
                foreach(string k in keys.Concat(hidden ?? []))
                    for(VisualElement e = Row(k)?.parent; e != null && e != page; e = e.parent)
                        if(findFold.Invoke(null, [e]) is { } fold && !((Func<bool>)foldIsOpen.GetValue(fold))()) {
                            var set = (Action<bool, bool>)foldSet.GetValue(fold);
                            set(true, false);
                            opened.Add(set);
                        }
            yield return Frames(1);   // the resolved styles catch up
            seen.UnionWith(keys.Concat(hidden ?? []).Where(Shown));
            foreach(Action<bool, bool> set in opened) set(false, false);
            unfolded += opened.Count;
        }
        if(tabs != null && was >= 0) tabs.Select(was);
        List<string> missing = [.. keys.Where(k => !seen.Contains(k))], shows = [.. (hidden ?? []).Where(seen.Contains)];
        bool moved = QuartzShell.CurrentPage == pageKey && page.Q(name: "menu-classic") == null;
        check($"{pageKey}: drawn in the new menu with every row it should show ({keys.Length})"
            + (hidden == null ? "" : $", and none of the {hidden.Length} it hides"), moved && missing.Count == 0 && shows.Count == 0,
            $"page={QuartzShell.CurrentPage} classicCard={!moved} missing={string.Join(",", missing)} shown={string.Join(",", shows)} unfolded={unfolded}");
    }

    /// <summary>The open page's own tab row (Tabs.Pages on the page, not the shell's category tabs above it), found through the
    /// Ref Facet keeps on its bar; null when the page has none.</summary>
    internal static global::Facet.Tabs PageTabs() => AllPageTabs().FirstOrDefault();

    /// <summary>Every tab row on the open page, in tree order (a row inside another row's tab after it).</summary>
    internal static List<global::Facet.Tabs> AllPageTabs() {
        Type refType = typeof(global::Facet.Tabs).GetNestedType("Ref", BindingFlags.NonPublic);
        MethodInfo find = refType == null ? null : typeof(Data).GetMethod(nameof(Data.Find))?.MakeGenericMethod(refType);
        List<global::Facet.Tabs> all = [];
        if(find == null) return all;
        foreach(VisualElement v in PageContent().Query<VisualElement>().ToList())
            if(find.Invoke(null, [v]) is { } r) all.Add((global::Facet.Tabs)refType!.GetField("Tabs")!.GetValue(r));
        return all;
    }

    /// <summary>Selects the open page's tab titled title (its key), as a click on it does, and lets the layout catch up.</summary>
    internal static IEnumerator ShowTab(string title) {
        global::Facet.Tabs tabs = PageTabs();
        for(int i = 0; tabs != null && tabs.SelectedName != title && i < 16; i++) tabs.Select(i);
        yield return Frames(1);
    }

    // Facet's record of a fold (an Advanced or a Section body), internal to it. Looked up on first use, so a rename in Facet
    // fails Parity's check instead of the self-test's class initialiser, which would take every check down.
    private static MethodInfo findFold;
    private static FieldInfo foldSet, foldIsOpen;

    private static bool Folds(out string miss) {
        miss = null;
        if(findFold != null) return true;
        Type fold = typeof(Controls).GetNestedType("Fold", BindingFlags.NonPublic);
        (foldSet, foldIsOpen) = (fold?.GetField("Set"), fold?.GetField("IsOpen"));
        if(fold == null || foldSet == null || foldIsOpen == null) {
            miss = $"Controls.Fold={fold != null} Set={foldSet != null} IsOpen={foldIsOpen != null}";
            return false;
        }
        findFold = typeof(Data).GetMethod(nameof(Data.Find))?.MakeGenericMethod(fold);
        miss = findFold == null ? "Data.Find" : null;
        return findFold != null;
    }

    /// <summary>A switch on the open page: a click flips its setting, a second puts it back. field writes the setting as its
    /// row does, for the restore.</summary>
    internal static IEnumerator Flips(Action<string, bool, string> check, string pageKey, string toggleKey, Bind<bool> field) {
        string tag = $"{pageKey} {toggleKey}";
        bool before = field.Get();
        VisualElement track = Body.Q(name: toggleKey + "-input");
        if(track == null) {
            check($"{tag}: a click flips its setting", false, "no " + toggleKey + "-input");
            yield break;
        }
        try {
            Click(track);
            yield return Frames(2);
            bool flipped = field.Get() == !before;
            track = Body.Q(name: toggleKey + "-input");
            if(track != null) Click(track);
            yield return Frames(2);
            Step(check, $"{tag}: a click flips its setting, and a second click puts it back",
                flipped && field.Get() == before && FacetBridge.NewMenuVisible() && QuartzShell.CurrentPage == pageKey,
                $"before={before} flipped={flipped} now={field.Get()}");
        } finally {
            PutBack(field, before);
        }
    }

    // The player's value, written straight into the setting whatever the row shows now.
    private static void PutBack(Bind<bool> field, bool before) {
        if(field.Get() != before) field.Set(before);
    }

    /// <summary>The open moved page's switch toggleKey, written here first, then bound through its context menu: the key
    /// captured binds classicId, fires from the value written here, and the row follows within 150 ms; the menu then clears
    /// it, and offers Clear again for a bind made elsewhere. descKey is the row's description (null: none), which
    /// its tooltip shows translated above the key. A bind of the player's and the setting are put back.</summary>
    internal static IEnumerator Hotkey(Action<string, bool, string> check, string pageKey, string toggleKey, string classicId, string descKey,
        Bind<bool> field) {
        string tag = $"{pageKey} {toggleKey}";
        // A key none of the player's other binds uses: pressing it must flip this row alone, or another toggle flips and is saved.
        KeyCode key = new[] { KeyCode.F13, KeyCode.F14, KeyCode.F15 }.FirstOrDefault(k => (int)k != MainCore.Conf.ToggleKey
            && !MainCore.Conf.ToggleKeybinds.Any(b => b.Key != classicId && b.Value.Key == (int)k));
        VisualElement row = Body.Q(name: toggleKey), track = Body.Q(name: toggleKey + "-input");
        if(row == null || track == null || key == KeyCode.None) {
            check($"{tag}: a hotkey bound here", false, $"row={row != null} track={track != null}{(key == KeyCode.None ? " F13-F15 all bound by the player" : "")}");
            yield break;
        }
        string shown = Keybind.Format(Keybind.KeyModifier.None, key);
        bool before = field.Get(), had = ToggleBinds.TryGet(classicId, out Keybind.KeyModifier hadMod, out KeyCode hadKey);
        if(had) ShellRows.ClearHotkey(classicId);
        string set = QuartzHost.Host.Translate("MENU_SET_HOTKEY"), clear = QuartzHost.Host.Translate("MENU_CLEAR_HOTKEY");
        // The description in the player's language: a key with no translation would come back as the key itself.
        string desc = descKey == null ? "" : QuartzHost.Host.Translate(descKey);
        bool described = descKey == null || (desc.Length > 0 && desc != descKey && MainCore.Tr.HasKeyForLanguage(descKey, "en-US"));
        Hover(row);
        bool kept = described && (row.tooltip ?? "") == desc;
        try {
            // A write from here first: the hotkey flips from it.
            Click(track);
            yield return Frames(2);
            bool wrote = field.Get() == !before;
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
            bool prompted = prompt && Popped("menu-hotkey-press") == null;
            Hover(row);
            string tip = row.tooltip, want = desc.Length == 0 ? shown : desc + "\n" + shown;
            check($"{tag}: right-click offers Set hotkey and no Clear, a prompt shows while it captures, and the key captured binds {classicId}, "
                + "and shows in the tooltip under the description",
                kept && wrote && setRow != null && !offersClear && capturing && prompted && bound && tip == want,
                $"kept={kept} described={described} wrote={wrote} set={setRow != null} clear={offersClear} capturing={capturing} prompt={prompt} bound={got}/{mod} "
                + $"tip='{tip}' want='{want}'");
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
            // The menu is offered afresh as the pointer comes over the row: a bind made elsewhere meanwhile shows there.
            ToggleBinds.Set(classicId, Keybind.KeyModifier.None, key);
            Hover(row);
            RightClick(row);
            yield return Frames(2);
            clearRow = MenuRow(clear);
            if(clearRow != null) Pick(clearRow);
            yield return Frames(1);
            check($"{tag}: a bind made elsewhere shows in the row's menu once the pointer comes over it, and clears from there",
                clearRow != null && !ToggleBinds.TryGet(classicId, out _, out _), $"clear={clearRow != null}");
        } finally {
            KeyCaptureService.Cancel();
            Popup.CloseAll();
            if(had) ShellRows.SetHotkey(classicId, hadMod, hadKey);
            else if(ToggleBinds.TryGet(classicId, out _, out _)) ShellRows.ClearHotkey(classicId);
            PutBack(field, before);
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
            PutBack(field, before);   // a bind that fired as it was made flipped it
        }
    }

    /// <summary>Runs checks on the moved page pageKey, opened from a closed menu with Overlay Core and featureId on. show makes
    /// every row a switch hides show; restore puts the player's values back, whatever happens. The features and the menu are
    /// put back as found.</summary>
    internal static IEnumerator OnPage(string featureId, string pageKey, Action show, Action restore, Func<IEnumerator> checks) {
        // Every feature is read before any is flipped, and put back in reverse: "overlay" is the feature's dependency.
        var saved = FeatureService.Features.Select(h => (id: h.Id, was: h.Enabled, had: FeatureService.State.Features.TryGetValue(h.Id, out bool v), value: v)).ToList();
        try {
            foreach(string id in new[] { "overlay", featureId }) if(!FeatureService.IsEnabled(id)) FeatureService.SetEnabled(id, true);
            yield return Frames(3);
            show();
            FacetBridge.OpenNewMenu(pageKey);
            yield return Frames(3);
            yield return checks();
        } finally {
            restore();
            QuartzShell.Hide();
            for(int i = saved.Count - 1; i >= 0; i--) RestoreFeature(saved[i].id, saved[i].was, saved[i].had, saved[i].value);
        }
        yield return Frames(3);
    }

    /// <summary>The page header's switch ends on the cards' control edge, over a row's switch (±1).</summary>
    internal static void HeaderSwitchEdge(Action<string, bool, string> check, string pageKey, string headerId, string rowId) {
        VisualElement head = Body.Q(name: headerId + "-input"), row = Body.Q(name: rowId + "-input");
        float Scale = QuartzShell.Window?.Scale is float k && k > 0f ? k : 1f;
        check($"{pageKey}: the header's switch ends over the rows' switches (±1)",
            // ±1 layout px: world px are layout px times the window's Scale.
            head != null && row != null && Displayed(head) && Displayed(row) && Mathf.Abs(head.worldBound.xMax - row.worldBound.xMax) / Scale <= 1f,
            $"header={head?.worldBound.xMax:0.0} row={row?.worldBound.xMax:0.0} scale={Scale:0.###}");
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

    /// <summary>The pop-in animation measures an overlay's root over its active rects (RectTransformUtility's relative bounds).
    /// With no preview, the root's PreviewFrame is not one of them: the bounds are those of every other rect. The root is
    /// shown for the reading, as in a level, and put back in the same frame.</summary>
    private static void MotionBounds(Action<string, bool, string> check, string pageKey, RectTransform root) {
        string tag = $"{pageKey}: with no preview the pop-in animation's bounds leave the preview frame out";
        if(root == null) {
            check(tag, false, "no root");
            return;
        }
        bool was = root.gameObject.activeSelf, any = false;
        Bounds with, without = default;
        var corners = new Vector3[4];
        try {
            root.gameObject.SetActive(true);
            with = RectTransformUtility.CalculateRelativeRectTransformBounds(root, root);
            foreach(RectTransform r in root.GetComponentsInChildren<RectTransform>(false)) {
                if(r.name == "PreviewFrame") continue;
                r.GetWorldCorners(corners);
                foreach(Vector3 c in corners) {
                    Vector3 local = root.InverseTransformPoint(c);
                    if(any) without.Encapsulate(local);
                    else without = new Bounds(local, Vector3.zero);
                    any = true;
                }
            }
        } finally {
            root.gameObject.SetActive(was);
        }
        bool framed = root.GetComponentsInChildren<RectTransform>(true).Any(r => r.name == "PreviewFrame");
        check(tag, OverlayPreview.Target == null && framed && any && Vector3.Distance(with.min, without.min) < 0.01f && Vector3.Distance(with.max, without.max) < 0.01f,
            $"target={OverlayPreview.Target} frame={framed} bounds={with.size}@{with.center} without={without.size}@{without.center}");
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

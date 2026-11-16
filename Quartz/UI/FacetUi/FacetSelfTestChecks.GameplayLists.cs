#if DEBUG && !IL2CPP
using System.Collections;
using System.Reflection;
using Facet;
using Newtonsoft.Json.Linq;
using Quartz.Async;
using Quartz.Core;
using Quartz.Features;
using Quartz.Features.Interop;
using Quartz.Features.KeyLimiter;
using Quartz.Features.Practice;
using Quartz.Features.Restriction;
using Quartz.Game;
using Quartz.IO;
using Quartz.Overlay;
using Quartz.UI.FacetUi.GameplayPages;
using Quartz.UI.Utility;
using TMPro;
using UnityEngine;
using UnityEngine.UIElements;
namespace Quartz.UI.FacetUi;
/// <summary>
/// The Gameplay list pages in the new menu, each against its classic page: rows, search hits and Korean text, then each
/// page's own rows: the key capture ending on leave, Escape and sync, the restriction mode a page build must not rewrite,
/// undo after a list item went away, and the practice readout's preview.
/// </summary>
internal static partial class FacetSelfTestChecks {
    private static IEnumerator GameplayLists(Action<string, bool, string> check, Action<string> info) {
        yield return GameplayKeyLimiter(check);
        yield return GameplayRestriction(check);
        yield return GameplayPractice(check, info);
    }

    private static IEnumerator GameplayKeyLimiter(Action<string, bool, string> check) {
        const string page = "gameplay.keylimiter";
        KeyLimiter.EnsureConf();
        ChartKeyLimiter.EnsureConf();
        bool locked = KeyBindSync.IsSyncing;
        string[] keys = ["KEYVIEWER_SYNCLIMITER", "KL_PROFILE_LABEL", "KL_PROFILE_NAME", "KL_ADD_PROFILE", "KL_REMOVE_PROFILE", "KL_CAPTURE", "KL_CLEAR",
            "KL_ALLOWED_KEYS", "KL_CHART_SECTION", "CHARTKEYLIMITER_ENABLE", "KL_CHART_NOTE", .. locked ? ["KL_SYNC_LOCKED"] : Array.Empty<string>()];
        string[] hidden = ["KL_NO_ALLOWED_KEYS", .. locked ? Array.Empty<string>() : ["KL_SYNC_LOCKED"]];
        string[] searched = ["KEYVIEWER_SYNCLIMITER", "KL_PROFILE_LABEL", "KL_PROFILE_NAME", "KL_ADD_PROFILE", "KL_REMOVE_PROFILE", "KL_CAPTURE", "KL_CLEAR",
            "CHARTKEYLIMITER_ENABLE"];
        // At least one allowed key, so its row and the heading show.
        yield return FeatureFormPage(check, "keylimiter", page, KeyLimiter.ConfMgr, () => {
            if(KeyLimiter.Conf.AllowedKeys.Length == 0) KeyLimiter.Conf.AllowedKeys = [(int)KeyCode.F15];
        }, null, keys, searched, () => More(), hidden);
        IEnumerator More() {
            // A key's Set and Remove are absent while the key viewer owns the keys.
            string[] buttons = ["SET#1-input", "REMOVE#1"];
            yield return Parity(check, page, locked ? [] : buttons, locked ? buttons : null);
            // The captures need the keys unlocked. Where the key viewer owns them, its sync source reads off for these checks,
            // set by reflection: SetSyncing goes to the key viewer, which may rewrite the player's keys.
            FieldInfo source = SyncSource;
            var was = source?.GetValue(null) as Func<bool>;
            try {
                if(locked && source != null) {
                    source.SetValue(null, (Func<bool>)(() => false));
                    KeyBindSync.Raise();
                    yield return Frames(2);
                    yield return Parity(check, page, buttons);
                }
                yield return KlCaptureFace(check, page);
                yield return KlRowCaptureFace(check, page);
                yield return KlCaptureLeave(check, page);
                yield return KlCaptureEscape(check, page);
                yield return KlCaptureSync(check, page);
            } finally {
                source?.SetValue(null, was);
                KeyBindSync.Raise();
            }
            yield return ProfileNoUndo(check, page);
        }
    }

    private static Button KlCaptureButton() => PageContent().Q<Button>("KL_CAPTURE-input");

    // The capture button reads PRESS_A_KEY while capturing and its own label again once a second click cancels.
    private static IEnumerator KlCaptureFace(Action<string, bool, string> check, string page) {
        string idle = QuartzHost.Host.Translate("KL_CAPTURE"), pressing = QuartzHost.Host.Translate("PRESS_A_KEY");
        Button b = KlCaptureButton();
        string before = b?.text;
        if(b != null) Submit(b);
        yield return Frames(2);
        bool capturing = KeyLimiter.IsCapturing;
        string during = b?.text;
        if(b != null) Submit(b);
        yield return Frames(2);
        check($"{page} KL_CAPTURE: the button reads PRESS_A_KEY while capturing, and its own label once a second click cancels",
            b != null && capturing && !KeyLimiter.IsCapturing && before == idle && during == pressing && b.text == idle,
            $"button={b != null} capturing={capturing} after={KeyLimiter.IsCapturing} faces='{before}'/'{during}'/'{b?.text}'");
        KeyLimiter.CancelCapture();
    }

    // A key row's Set reads PRESS_A_KEY while its own capture runs; the capture button above keeps its label.
    private static IEnumerator KlRowCaptureFace(Action<string, bool, string> check, string page) {
        string idle = QuartzHost.Host.Translate("KL_CAPTURE"), set = QuartzHost.Host.Translate("SET"), pressing = QuartzHost.Host.Translate("PRESS_A_KEY");
        Button row = PageContent().Q<Button>("SET#1-input");
        if(row != null) Submit(row);
        yield return Frames(2);
        bool capturing = KeyLimiter.IsCapturing;
        string during = row?.text, top = KlCaptureButton()?.text;
        if(row != null) Submit(row);
        yield return Frames(2);
        check($"{page} SET#1: the row's Set reads PRESS_A_KEY while it captures, and KL_CAPTURE keeps its label",
            row != null && capturing && !KeyLimiter.IsCapturing && during == pressing && top == idle && row.text == set,
            $"button={row != null} capturing={capturing} after={KeyLimiter.IsCapturing} set='{during}'/'{row?.text}' capture='{top}'");
        KeyLimiter.CancelCapture();
    }

    private static IEnumerator KlCaptureLeave(Action<string, bool, string> check, string page) {
        Button b = KlCaptureButton();
        if(b != null) Submit(b);
        yield return Frames(2);
        bool capturing = KeyLimiter.IsCapturing;
        QuartzShell.Go("profiles.main");
        yield return Frames(2);
        check($"{page}: leaving the page during a capture ends it", b != null && capturing && !KeyLimiter.IsCapturing,
            $"button={b != null} capturing={capturing} after={KeyLimiter.IsCapturing}");
        KeyLimiter.CancelCapture();
        QuartzShell.Go(page);
        yield return Frames(2);
    }

    private static IEnumerator KlCaptureEscape(Action<string, bool, string> check, string page) {
        int[] keys = [.. KeyLimiter.Conf.AllowedKeys];
        Button b = KlCaptureButton();
        if(b != null) Submit(b);
        yield return Frames(2);
        bool capturing = KeyLimiter.IsCapturing;
        // Key Limiter's capture reads the physical keys, so Escape is held there; the menu sees the press too, unless the game
        // would (a window that does not block keys passes them to it).
        HoldPhysical(KeyCode.Escape, 2);
        if(FacetBridge.BlocksGameInput()) PressNextFrame(KeyCode.Escape);
        yield return Frames(3);
        bool ended = !KeyLimiter.IsCapturing;
        KeyLimiter.CancelCapture();
        check($"{page}: Escape during a capture ends it and adds no key", b != null && capturing && ended && keys.SequenceEqual(KeyLimiter.Conf.AllowedKeys),
            $"button={b != null} capturing={capturing} ended={ended} keys={keys.Length}->{KeyLimiter.Conf.AllowedKeys.Length} {Showing()}");
        if(!FacetBridge.NewMenuVisible() || QuartzShell.CurrentPage != page) {
            FacetBridge.OpenNewMenu(page);
            yield return Frames(3);
        }
    }

    // The sync source reads on for the check, by reflection. The classic page's own handler is held off, so only this page's
    // can end the capture.
    // A physical-key probe: HookInput.PhysicalKeyHeld(key) reads as held over the next frames only. Patched on first use.
    private static KeyCode physicalKey;
    private static int physicalFrom = -1, physicalTo = -1;
    private static HarmonyLib.Harmony physicalProbe;

    private static void HoldPhysical(KeyCode key, int frames) {
        if(physicalProbe == null) {
            physicalProbe = new HarmonyLib.Harmony("Quartz.FacetSelfTest.Physical");
            physicalProbe.Patch(typeof(HookInput).GetMethod(nameof(HookInput.PhysicalKeyHeld), [typeof(KeyCode)]),
                prefix: new HarmonyLib.HarmonyMethod(typeof(FacetSelfTestChecks).GetMethod(nameof(PhysicalProbe), Static)));
        }
        physicalKey = key;
        physicalFrom = Time.frameCount + 1;
        physicalTo = physicalFrom + frames - 1;
    }

    private static bool PhysicalProbe(KeyCode __0, ref bool __result) {
        if(__0 != physicalKey || Time.frameCount < physicalFrom || Time.frameCount > physicalTo) return true;
        __result = true;
        return false;
    }

    private static FieldInfo SyncSource => typeof(KeyBindSync).GetField("isSyncing", BindingFlags.Static | BindingFlags.NonPublic);

    private static IEnumerator KlCaptureSync(Action<string, bool, string> check, string page) {
        FieldInfo source = SyncSource;
        var classic = typeof(PageKeyLimiter).GetField("syncLockChangedHandler", BindingFlags.Static | BindingFlags.NonPublic)?.GetValue(null) as Action;
        var was = source?.GetValue(null) as Func<bool>;
        Button b = KlCaptureButton();
        if(b != null) Submit(b);
        yield return Frames(2);
        bool capturing = KeyLimiter.IsCapturing, ended = false;
        if(classic != null) KeyBindSync.Changed -= classic;
        try {
            if(source != null) {
                source.SetValue(null, (Func<bool>)(() => true));
                KeyBindSync.Raise();
                ended = !KeyLimiter.IsCapturing;
            }
        } finally {
            source?.SetValue(null, was);
            if(classic != null) KeyBindSync.Changed += classic;
            KeyLimiter.CancelCapture();
            KeyBindSync.Raise();
        }
        check($"{page}: sync turning on ends a running capture", source != null && b != null && capturing && ended,
            $"source={source != null} button={b != null} capturing={capturing} ended={ended}");
        yield return Frames(2);
    }

    private static IEnumerator ProfileNoUndo(Action<string, bool, string> check, string page) {
        global::Facet.History.Clear();
        string was = KeyLimiter.Profiles[KeyLimiter.ActiveProfileIndex].Name, want = was + " selftest";
        bool typed = TypeInto("KL_PROFILE_NAME-input", want);
        yield return Frames(2);
        string now = KeyLimiter.Profiles[KeyLimiter.ActiveProfileIndex].Name;
        bool canUndo = global::Facet.History.CanUndo;
        check($"{page}: profile rows record no undo step", typed && now == want && !canUndo, $"typed={typed} name='{now}' canUndo={canUndo}");
    }

    private static IEnumerator GameplayRestriction(Action<string, bool, string> check) {
        const string page = "gameplay.judgement";
        Restriction.EnsureConf();
        RestrictionSettings C() => Restriction.Conf;
        bool x = XPerfectBridge.Installed;
        string[] tiles = [.. RestrictionPage.Rows.Select(r => r.Key)];
        string[] keys = ["JR_MODE_LABEL", "JR_SECTIONS", "JR_SECTIONS_ON", "JR_SECTIONS_ADD", "JR_MESSAGE", "JR_MESSAGE_HINT"];
        string[] searched = ["JR_MODE_LABEL", "JR_ACC", .. tiles, "JR_SECTIONS_ON", "JR_SECTIONS_ADD", "JR_MESSAGE", "JRSEC_START", "JRSEC_END", "JRSEC_REMOVE"];
        yield return FeatureFormPage(check, "restriction", page, Restriction.ConfMgr, () => {
            C().JRestrictSectionsEnabled = true;
            C().JRestrictSections.Add(new JudgementSection());
            // Stored as X-Perfect only where XPerfect is absent: the page must show Pure Perfect and leave the 2 stored.
            C().JRestrictMode = x ? 1 : 2;
            Restriction.ConfMgr.Save();
            QuartzShell.Changed();   // the index follows the section, as it would one stored before the menu opened
        }, null, keys, searched, () => More(), ["JR_ACC", "JR_MODE_CUSTOM", "JR_NO_SECTIONS"]);
        IEnumerator More() {
            yield return StoredMode(check, page, x);
            yield return RestrictionDeath(check);
            int n = C().JRestrictSections.Count;
            yield return Parity(check, page, [$"JR_SECTION#{n}", $"JRSEC_START#{n}", $"JRSEC_END#{n}", $"JRSEC_REMOVE#{n}"]);
            Translated(check, page + " sections and tiles", ["JR_SECTION", "JRSEC_START", "JRSEC_END", "JRSEC_REMOVE", "JR_MODE_CUSTOM", .. tiles]);
            yield return PickItem("JR_MODE_LABEL", QuartzHost.Host.Translate("JR_MODE_CUSTOM"));
            check($"{page} JR_MODE_LABEL: Custom Judgements picked in the dropdown reaches the setting", C().JRestrictMode == 3, $"mode={C().JRestrictMode}");
            yield return Parity(check, page, ["JR_MODE_CUSTOM", .. tiles], ["JR_ACC"]);
            yield return MaskFlip(check, page, "JR_ALLOW_MISS", () => C().JRestrictAllowedMask, 8, [.. RestrictionPage.Rows.Select(r => r.Bit)]);
            yield return ListUndo(check, page, "JR_SECTIONS_ADD", "JRSEC_REMOVE", "JR_SECTION", C().JRestrictSections, s => (s.Start, s.End),
                s => (s.Start, s.End) = (20f, 80f), () => TypeInto("JRSEC_START#2-input", "40"), s => s.Start == 40f);
            yield return HeaderHotkeyCheck(check, page, "SECTION_JUDGEMENT_RESTRICTION", "restriction");
        }
    }

    // Mode 4 fills Too Early's segment with the death colour and Very Early's with its own; mode 1 turns Very Early to death too.
    // The bar rebuilds on a refresh pass, so each read waits up to a second for it. The mode is put back.
    private static IEnumerator RestrictionDeath(Action<string, bool, string> check) {
        static bool Near(Color a, Color b) => Mathf.Abs(a.r - b.r) <= 0.01f && Mathf.Abs(a.g - b.g) <= 0.01f && Mathf.Abs(a.b - b.b) <= 0.01f;
        static Color Fill(string key) =>
            PageContent().Q(name: key + "-input")?.parent?.Q(JudgementBar.FillName)?.resolvedStyle.backgroundColor ?? Color.clear;
        int was = Restriction.Conf.JRestrictMode;
        Color death = JudgementBar.Death, veryEarly = JudgementBar.SlotColour(1);
        Restriction.Conf.JRestrictMode = 4;
        for(float t0 = Time.realtimeSinceStartup; !(Near(Fill("JR_ALLOW_TOOEARLY"), death) && Near(Fill("JR_ALLOW_VERYEARLY"), veryEarly))
            && Time.realtimeSinceStartup - t0 < 1f;) yield return null;
        Color te4 = Fill("JR_ALLOW_TOOEARLY"), ve4 = Fill("JR_ALLOW_VERYEARLY");
        Restriction.Conf.JRestrictMode = 1;
        for(float t0 = Time.realtimeSinceStartup; !Near(Fill("JR_ALLOW_VERYEARLY"), death) && Time.realtimeSinceStartup - t0 < 1f;) yield return null;
        Color ve1 = Fill("JR_ALLOW_VERYEARLY");
        Restriction.Conf.JRestrictMode = was;
        yield return Frames(3);
        check("restriction: blocked judgements turn the death colour", Near(te4, death) && Near(ve4, veryEarly) && Near(ve1, death),
            $"mode4 tooEarly={te4} veryEarly={ve4} mode1 veryEarly={ve1} death={death} slot1={veryEarly}");
    }

    // A stored 2 without XPerfect: the classic page built for the search index and the moved page shown leave it 2 in memory,
    // and a section edit saves it as 2.
    private static IEnumerator StoredMode(Action<string, bool, string> check, string page, bool x) {
        const string tag = "gameplay.judgement: a stored X-Perfect-only mode without XPerfect shows Pure Perfect and stays 2 on disk";
        if(x) {
            formInfo(tag + ": not run, XPerfect is installed");
            yield break;
        }
        string path = Restriction.ConfMgr.Path;
        QuartzShell.Changed();
        Find("restriction");
        QuartzShell.Nav?.Filter("");
        yield return Frames(2);
        string shown = PageContent().Q<Button>("JR_MODE_LABEL-input")?.text;
        byte[] before = Bytes(path);
        bool typed = TypeInto("JRSEC_START#1-input", "12.5"), saved = false;
        for(float t0 = Time.realtimeSinceStartup; !saved && Time.realtimeSinceStartup - t0 < 2f;) {
            yield return null;
            saved = !SameBytes(Bytes(path), before);
        }
        int disk = saved ? (int?)JToken.Parse(File.ReadAllText(path))["JRestrictMode"] ?? -1 : -1;
        check(tag, shown == QuartzHost.Host.Translate("JR_MODE_PURE_PERFECT") && typed && saved && Restriction.Conf.JRestrictMode == 2 && disk == 2,
            $"shown='{shown}' typed={typed} saved={saved} memory={Restriction.Conf.JRestrictMode} disk={disk}");
    }

    /// <summary>Three items added through add, the last given values of its own (mark) and the second changed (change), then
    /// the first removed and the changed one after it: an undo leaves the one left as it was, skips the removed item's step (it
    /// keeps the change), and the box left is numbered 1.</summary>
    private static IEnumerator ListUndo<T, V>(Action<string, bool, string> check, string page, string addId, string removeId, string boxId, List<T> list,
        Func<T, V> values, Action<T> mark, Func<bool> change, Func<T, bool> changed) where T : class {
        global::Facet.History.Clear();
        list.Clear();
        QuartzShell.Surface?.Refresh();
        yield return Frames(2);
        for(int i = 0; i < 3; i++) {
            if(PageContent().Q<Button>(addId + "-input") is { } add) Submit(add);
            yield return Frames(2);
        }
        if(list.Count != 3) {
            check($"{page}: undo after a removal never writes into another item, and the titles renumber", false, $"added={list.Count}");
            yield break;
        }
        string word = MainCore.Tr.GetForLanguage(removeId, "en-US", removeId);
        Find(word);   // rebuilds a stale index; a common word passes the hit limit, so the index is asked again without one
        List<SearchIndex.Hit> hits = QuartzShell.Nav?.Index?.Find(word, int.MaxValue) ?? [];
        QuartzShell.Nav?.Filter("");
        yield return Frames(2);
        // A hit's Key is the row's Lang key, unnumbered: each of the three items gives one.
        check($"{page}: search finds an item added on the page", hits.Count(h => h.Key == removeId) == 3, Hits(hits));
        T second = list[1], last = list[2];
        mark(last);
        QuartzShell.Surface?.Refresh();
        yield return Frames(2);
        bool did = change();
        yield return Frames(2);
        bool wrote = changed(second);
        Button Remove() => PageContent().Q<Button>(removeId + "#1-input");
        if(Remove() is { } first) Submit(first);
        yield return Frames(2);
        if(Remove() is { } moved) Submit(moved);
        yield return Frames(2);
        V kept = values(last);
        global::Facet.History.Undo();
        yield return Frames(2);
        // The change's step is bound to the removed item, so the undo skips it: the removed item keeps the change too.
        bool skipped = changed(second);
        bool box = PageContent().Q(name: boxId + "#1") != null && PageContent().Q(name: boxId + "#2") == null;
        check($"{page}: undo after a removal never writes into another item, and the titles renumber",
            did && wrote && skipped && list.Count == 1 && list[0] == last && EqualityComparer<V>.Default.Equals(values(last), kept) && box,
            $"changed={did}/{wrote} skipped={skipped} left={list.Count} last={list.Count == 1 && list[0] == last} values={values(last)}/{kept} box={box}");
    }

    // A text field's value typed and committed with Enter.
    private static bool TypeInto(string id, string text) {
        TextField field = PageContent().Q<TextField>(id);
        if(field == null) return false;
        field.value = text;
        using KeyDownEvent ev = Pooled<KeyDownEvent>();
        typeof(KeyboardEventBase<KeyDownEvent>).GetProperty("keyCode").SetValue(ev, KeyCode.Return);
        ev.target = field;
        field.SendEvent(ev);
        return true;
    }

    private static IEnumerator GameplayPractice(Action<string, bool, string> check, Action<string> info) {
        const string page = "gameplay.practice";
        bool wasOn = FeatureService.IsEnabled("practice");
        string[] files = [Path.Combine(MainCore.Paths.RootPath, "Practice.json"), Path.Combine(ProfileManager.ProfilesPath, ProfileManager.Active, "Practice.json")];
        bool[] existed = [.. files.Select(File.Exists)];
        PracticeDifficulty.EnsureConf();
        PracticeSettings C() => PracticeDifficulty.Conf;
        string[] keys = ["PRACTICE_HINT", "FONT_OVERLAY", "PRACTICE_ADD", "PRACTICE_INDICATOR", "PRACTICE_SHOWINDICATOR", "PRACTICE_SHOWSPEED",
            "PRACTICE_INGAMEONLY", "PRACTICE_FONTSIZE", "PRACTICE_COLOR", "PRACTICE_RESETPOS"];
        string[] item = ["PRACTICE_KEY", "PRACTICE_DIFFICULTY_LABEL", "PRACTICE_SPEED", "PRACTICE_SETSDIFF", "PRACTICE_SETSSPEED", "PRACTICE_REMOVE"];
        string[] searched = ["PRACTICE_ADD", "PRACTICE_SHOWINDICATOR", "PRACTICE_SHOWSPEED", "PRACTICE_INGAMEONLY", "PRACTICE_FONTSIZE", "PRACTICE_COLOR",
            "PRACTICE_RESETPOS", .. item];
        try {
            yield return FeatureFormPage(check, "practice", page, PracticeDifficulty.ConfMgr, () => C().Bindings.Add(new PracticeBinding()), PracticeOverlay.Apply,
                keys, searched, () => More(), ["PRACTICE_NO_BINDINGS"]);
        } finally {
            // Practice turned on for the first time saves a file of its own: dropped again, with the settings it loaded.
            if(!wasOn && !existed[0]) MainThread.Enqueue(() => ForgetPractice(files, existed));
        }
        yield return Frames(3);
        string[] left = [.. files.Where((f, i) => !existed[i] && File.Exists(f))];
        check("the practice checks leave no Practice file they made", left.Length == 0, $"left={string.Join(",", left)} wasOn={wasOn}");
        IEnumerator More() {
            int n = C().Bindings.Count;
            yield return Parity(check, page, [$"PRACTICE_BINDING#{n}", .. item.Select(k => $"{k}#{n}")]);
            Translated(check, page + " bindings", ["PRACTICE_BINDING", "PRACTICE_UNBOUND", .. item]);
            yield return PracticeKeyHide(check, page, n);
            yield return Hotkey(check, page, "PRACTICE_SHOWSPEED", "practice_showspeed", "DESC_PRACTICE_SHOWSPEED", Bind.To(() => C().ShowSpeed, v => {
                C().ShowSpeed = v;
                PracticeDifficulty.Save();
            }));
            yield return ListUndo(check, page, "PRACTICE_ADD", "PRACTICE_REMOVE", "PRACTICE_BINDING", C().Bindings, b => b.Pitch, b => b.Pitch = 150,
                () => SlideTo("PRACTICE_SPEED#2", PracticeSettings.MinPitch, PracticeSettings.MaxPitch, 500f), b => b.Pitch == 500);
            // Through the row's own slider, read off the readout.
            float want = Mathf.Abs(C().FontSize - 60f) < 0.5f ? 70f : 60f;
            yield return PreviewChecks(check, info, page, PracticeOverlay.FontKey, () => SlideTo("PRACTICE_FONTSIZE", 8f, 120f, want),
                () => OverlayPreview.ShownOf(PracticeOverlay.FontKey)?.transform.Find("Readout")?.GetComponent<TextMeshProUGUI>() is { } text
                    && Mathf.Abs(text.fontSize - want) < 0.5f);
            FacetBridge.OpenNewMenu(page);
            yield return Frames(3);
            QuartzShell.Go("profiles.main");
            yield return Frames(2);
            GameObject root = OverlayPreview.ShownOf(PracticeOverlay.FontKey);
            check($"{page}: leaving the page puts the readout back to its own rule",
                root != null && root.activeSelf == PracticeOverlay.ShouldShow() && !OverlayPreview.Is(PracticeOverlay.FontKey),
                $"root={root != null} active={root?.activeSelf} rule={PracticeOverlay.ShouldShow()} target={OverlayPreview.Target}");
        }
    }

    // A capture on the binding's key row ends when the menu closes, and a key pressed after changes no binding.
    private static IEnumerator PracticeKeyHide(Action<string, bool, string> check, string page, int n) {
        string tag = $"{page} PRACTICE_KEY#{n}: closing the menu during a capture ends it, and a later key press changes no binding";
        PracticeBinding b = PracticeDifficulty.Conf.Bindings[n - 1];
        KeyCode was = b.Key;
        // A key none of the player's binds uses, so the press fires nothing of theirs.
        KeyCode press = new[] { KeyCode.F13, KeyCode.F14, KeyCode.F15 }.FirstOrDefault(k => (int)k != MainCore.Conf.ToggleKey
            && !MainCore.Conf.ToggleKeybinds.Any(t => t.Value.Key == (int)k) && PracticeDifficulty.Conf.Bindings.All(p => p.Key != k));
        Control<string> row = KeyRow(PageContent().Q(name: $"PRACTICE_KEY#{n}-input"));
        bool began = row != null && row.BeginCapture();
        yield return Frames(1);
        bool capturing = KeyCaptureService.IsCapturing;
        QuartzShell.Hide();
        yield return Frames(1);
        bool ended = !KeyCaptureService.IsCapturing;
        if(press != KeyCode.None) PressNextFrame(press);
        yield return Frames(2);
        check(tag, press != KeyCode.None && began && capturing && ended && b.Key == was,
            $"press={press} row={row != null} began={began} capturing={capturing} ended={ended} key={b.Key}/{was}");
        KeyCaptureService.Cancel();
        FacetBridge.OpenNewMenu(page);
        yield return Frames(3);
    }
}
#endif

#if DEBUG && !IL2CPP
using System.Collections;
using Facet;
using Quartz.Core;
using Quartz.Prism;
using UnityEngine;
using UnityEngine.UIElements;
namespace Quartz.UI.FacetUi;
/// <summary>
/// The menu hotkey pressed for real: it opens the shortcut menu, closes it, and the editor that menu's Edit layout opened
/// brings it back on Done or Escape once the session has ended. The hotkey is a free key of F13-F15; the hotkey and the
/// settings file are the player's and are put back.
/// </summary>
internal static partial class FacetSelfTestChecks {
    private static IEnumerator AltK(Action<string, bool, string> check, Action<string> info) {
        yield return ShortcutShots(info);
        IO.CoreSettings c = MainCore.Conf;
        string path = MainCore.ConfMgr.Path;
        KeyCode key = new[] { KeyCode.F13, KeyCode.F14, KeyCode.F15 }
            .FirstOrDefault(k => (int)k != c.ToggleKey && (int)k != c.KvSwitcherKey && !c.ToggleKeybinds.Any(b => b.Value.Key == (int)k));
        if(key == KeyCode.None) { check("alt+k: the menu hotkey's presses", false, "F13-F15 all bound by the player"); yield break; }
        QuartzShell.Hide();
        MainCore.ConfMgr.Save();   // pending saves land now: the bytes compared at the end are these
        byte[] file = File.ReadAllBytes(path);
        var was = (c.ToggleKey, c.ToggleModifier);
        bool AllShut() => !FacetBridge.NewMenuVisible() && !UICore.SharedLayoutOpen;
        string Seen() => $"shortcut={FacetBridge.ShortcutVisible()} editor={LayoutHotkey.EditorShown()} requested={LayoutHotkey.Trip.Requested} ours={LayoutHotkey.Ours} arranging={UICore.IsArranging} door={LayoutHotkey.Door} frame={Time.frameCount} {Showing()}";
        IEnumerator Press() {
            PressNextFrame(key);
            yield return Frames(5);
        }
        // Edit layout from the shortcut menu on screen, then up to 30 frames for the editor, which then stays up a few frames
        // as any real session does (a Done in the frame it opened would end a session the hotkey's tick never saw).
        IEnumerator EditLayout() {
            SubmitShortcut(1);
            for(int i = 0; i < 30 && !LayoutHotkey.EditorShown(); i++) yield return Frames(1);
            yield return Frames(3);
        }
        bool[] arranging = [false];
        // Up to 30 frames for the shortcut menu; the session's state on its first visible frame goes into arranging[0].
        IEnumerator BackToShortcut() {
            for(int i = 0; i < 30 && !FacetBridge.ShortcutVisible(); i++) yield return Frames(1);
            arranging[0] = UICore.IsArranging;
        }
        try {
            (c.ToggleModifier, c.ToggleKey) = ((int)Keybind.KeyModifier.None, (int)key);
            yield return Frames(3);
            if(!LayoutHotkey.Door) {
                info("alt+k: door shut, the menu checks run");
                yield return Press();
                check("alt+k: with the filter off the hotkey opens the menu", FacetBridge.NewMenuVisible(), Seen());
                yield return Press();
                check("alt+k: and again closes it", AllShut(), Seen());
            } else {
                yield return Press();
                check("alt+k: the hotkey opens the Quartz shortcut menu", FacetBridge.ShortcutVisible() && !FacetBridge.NewMenuVisible(), Seen());
                yield return Press();
                check("alt+k: the hotkey again closes the shortcut menu", !FacetBridge.ShortcutVisible() && AllShut(), Seen());

                yield return Press();
                yield return EditLayout();
                bool shown = LayoutHotkey.EditorShown();
                if(LayoutEditorProbe.ShownRoot()?.Q<Button>("layout-done") is Button done) Submit(done);
                yield return BackToShortcut();
                check("alt+k: Done in an editor the shortcut menu opened brings the shortcut menu back once the session has ended",
                    shown && FacetBridge.ShortcutVisible() && !arranging[0] && !LayoutHotkey.EditorShown() && !LayoutHotkey.Trip.Busy,
                    $"shown={shown} arranging={arranging[0]} {Seen()}");

                yield return EditLayout();
                shown = LayoutHotkey.EditorShown();
                FacetBridge.HandleEscape();
                yield return BackToShortcut();
                check("alt+k: Escape in that editor brings the shortcut menu back", shown && FacetBridge.ShortcutVisible(), $"shown={shown} {Seen()}");

                yield return EditLayout();
                shown = LayoutHotkey.EditorShown();
                PressNextFrame(key);
                yield return Frames(10);
                check("alt+k: the hotkey in that editor closes everything and nothing comes back",
                    shown && AllShut() && !LayoutHotkey.EditorShown() && !FacetBridge.ShortcutVisible() && !LayoutHotkey.Trip.Busy,
                    $"shown={shown} {Seen()}");

                yield return Press();
                yield return EditLayout();
                if(LayoutEditorProbe.ShownRoot()?.Q<Button>("layout-done") is Button again) Submit(again);
                yield return BackToShortcut();
                bool back = FacetBridge.ShortcutVisible();
                SubmitShortcut(2);
                yield return Frames(10);
                check("alt+k: Close after a round trip closes everything",
                    back && AllShut() && !FacetBridge.ShortcutVisible() && !LayoutHotkey.EditorShown() && !LayoutHotkey.Trip.Busy,
                    $"back={back} {Seen()}");

                FacetBridge.OpenNewMenu();
                yield return Frames(2);
                TextField box = QuartzShell.Window.Search;
                box.Focus();
                yield return Frames(2);
                yield return Press();
                check("alt+k: typing in the new menu's search box keeps the hotkey from acting",
                    FacetBridge.NewMenuVisible() && !FacetBridge.ShortcutVisible(), Seen());
                box.Blur();
                QuartzShell.Hide();
                yield return Frames(2);

                PrismBridge.OpenLayoutEditor();
                yield return Frames(5);
                yield return Press();
                check("alt+k: during a session the shortcut menu did not open, it does nothing",
                    LayoutHotkey.EditorShown() && !FacetBridge.ShortcutVisible(), Seen());
                FacetBridge.HandleEscape();
                for(int i = 0; i < 30 && UICore.IsArranging; i++) yield return Frames(1);
                yield return Frames(3);
                check("alt+k: and its end brings nothing back", !FacetBridge.ShortcutVisible(), Seen());

            }
        } finally {
            HoldKey(KeyCode.None);
            LayoutHotkey.Trip.Cancel();
            FacetBridge.HideShortcut();
            QuartzShell.Hide();
            if(LayoutHotkey.EditorShown()) PrismBridge.CloseLayoutEditor();   // once: a finally cannot wait for frames
            (c.ToggleKey, c.ToggleModifier) = was;
            MainCore.ConfMgr.CancelPendingSave();
        }
        for(int i = 0; i < 10 && (LayoutHotkey.EditorShown() || FacetBridge.ShortcutVisible()); i++) yield return Frames(1);
        check("alt+k: nothing is left open after the section", !LayoutHotkey.EditorShown() && !FacetBridge.ShortcutVisible(), Seen());
        // A save the reset asked for, already handed to a worker, can land after the finally: settle, then save what was put back.
        float until = Time.realtimeSinceStartup + 1f;
        while(Time.realtimeSinceStartup < until) yield return null;
        MainCore.ConfMgr.Save();
        check("alt+k: the player's hotkey and settings file are as before the presses", SameBytes(path, file),
            $"bytes={File.ReadAllBytes(path).Length}/{file.Length}");
        yield return Frames(3);
    }

    // Pictures for a look by eye, through Unity's own capture (reflection: Quartz does not reference its module): the
    // shortcut menu as built, then rebuilt on Facet's own look with its Korean labels. Logged with their paths.
    // UnityModManager's window, drawn over every panel, is hidden for them and shown again.
    private static IEnumerator ShortcutShots(Action<string> info) {
        System.Reflection.MethodInfo capture = Type.GetType("UnityEngine.ScreenCapture, UnityEngine.ScreenCaptureModule")
            ?.GetMethod("CaptureScreenshot", [typeof(string)]);
        if(capture == null) { info("shortcut shots: no ScreenCapture"); yield break; }
        string dir = Path.GetTempPath();
        Type umm = Type.GetType("UnityModManagerNet.UnityModManager+UI, UnityModManager");
        object ui = umm?.GetProperty("Instance")?.GetValue(null);
        bool ummOpen = ui != null && umm!.GetProperty("Opened")?.GetValue(ui) is true;
        void Umm(bool open) => umm!.GetMethod("ToggleWindow", [typeof(bool)])?.Invoke(ui, [open]);
        if(ummOpen) Umm(false);
        IEnumerator Shot(string name) {
            FacetBridge.OpenShortcut();
            for(float t0 = Time.realtimeSinceStartup; Time.realtimeSinceStartup - t0 < 0.5f;) yield return null;   // past the fade-in
            string file = Path.Combine(dir, $"quartz-shortcut-{name}.png");
            if(File.Exists(file)) File.Delete(file);
            capture.Invoke(null, [file]);
            yield return Frames(3);
            info($"shortcut shot ({name}): {file}");
        }
        yield return Shot("as-built");
        Surface built = QuartzShortcut.Surface;
        FacetBridge.HideShortcut();
        QuartzShortcut.Shutdown();
        try {
            using(Theme.Use(Theme.FromAccent(MainCore.Conf.GetAccentColor()))) FacetBridge.OpenShortcut();
            for(int i = 0; i < ShortcutButtons.Length; i++)
                if(QuartzShortcut.Surface?.Root.Q<Button>(ShortcutButtons[i]) is Button b) b.text = MainCore.Tr.GetForLanguage(PrismRules.ShortcutKeys[i], "ko-KR", b.text);
            yield return Shot("plain-ko");
        } finally {
            QuartzShortcut.Surface?.Dispose();
            built?.Dispose();
            QuartzShortcut.Shutdown();   // the next open builds it again on Quartz's theme
            if(ummOpen) Umm(true);
        }
    }
}
#endif

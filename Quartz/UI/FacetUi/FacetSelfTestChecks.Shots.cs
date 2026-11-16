#if DEBUG && !IL2CPP && !QUARTZ_KEYVIEWER
using System.Collections;
using System.Reflection;
using Quartz.Prism;
using Quartz.UI.Nav;
using UnityEngine;
using UnityEngine.UIElements;
namespace Quartz.UI.FacetUi;
/// <summary>
/// The run under the marker facet-shots: pictures of the screens the player's notes showed, through Unity's own capture
/// (reflection: Quartz does not reference its module), into the temp folder's quartz-shots/, each logged with its path.
/// For a look by eye; one PASS per picture written.
/// </summary>
internal static partial class FacetSelfTestChecks {
    internal static IEnumerator Shots(Action<string, bool, string> check, Action<string> info) {
        MethodInfo capture = Type.GetType("UnityEngine.ScreenCapture, UnityEngine.ScreenCaptureModule")
            ?.GetMethod("CaptureScreenshot", [typeof(string)]);
        if(capture == null) { check("shots: Unity's capture is there", false, null); yield break; }
        string dir = Path.Combine(Path.GetTempPath(), "quartz-shots");
        Directory.CreateDirectory(dir);
        Type umm = Type.GetType("UnityModManagerNet.UnityModManager+UI, UnityModManager");
        object ui = umm?.GetProperty("Instance")?.GetValue(null);
        bool ummOpen = ui != null && umm!.GetProperty("Opened")?.GetValue(ui) is true;
        void Umm(bool open) => umm!.GetMethod("ToggleWindow", [typeof(bool)])?.Invoke(ui, [open]);
        IEnumerator Shot(string name) {
            for(float t0 = Time.realtimeSinceStartup; Time.realtimeSinceStartup - t0 < 0.6f;) yield return null;   // past fades
            string file = Path.Combine(dir, name + ".png");
            if(File.Exists(file)) File.Delete(file);
            capture.Invoke(null, [file]);
            for(float t0 = Time.realtimeSinceStartup; !File.Exists(file) && Time.realtimeSinceStartup - t0 < 3f;) yield return null;
            check($"shots: {name}", File.Exists(file), file);
        }
        IEnumerator Page(string name, string key, string scrollTo = null) {
            QuartzShell.Open(key);
            yield return Frames(3);
            yield return Shot(name);
            ScrollView sv = ContentScroll;
            VisualElement target = scrollTo == null ? null : Body.Q(scrollTo);
            if(target != null) {
                sv.ScrollTo(target);
                yield return Shot(name + "-" + scrollTo);
            } else if(sv.contentContainer.layout.height > sv.contentViewport.layout.height + 1f) {
                sv.scrollOffset = new Vector2(0f, sv.contentContainer.layout.height);
                yield return Shot(name + "-end");
            }
        }
        if(ummOpen) Umm(false);
        try {
            yield return Page("01-restriction", "gameplay.judgement");
            yield return Page("02-panels", "overlay.panels");
            yield return Page("03-tuf-levels", "tuf.levels");
            yield return Page("04-tuf-settings", "tuf.settings");
            yield return Page("05-tuf-packs", "tuf.packs");
            yield return Page("06-effect-remover", "visuals.effectremover", "FXRM_PLANET_ALL");
            yield return Page("07-hide-judgements", "visuals.hidejudgements");
            // 12: a page tab after a pointer click (no focus ring).
            QuartzShell.Open("overlay.general");
            yield return Frames(3);
            NavPage judgement = NavRegistry.ByKey("overlay.judgement");
            string label = judgement == null ? null : QuartzHost.Host.Translate(QuartzShell.Label(judgement.LocaleKey, judgement.Title));
            Button tab = QuartzShell.TabRow?.Element?.Query<Button>().ToList().FirstOrDefault(b => b.text == label);
            if(tab != null) {
                Vector2 at = tab.worldBound.center;
                Pointer<PointerDownEvent>(tab, at);
                Pointer<PointerUpEvent>(tab, at);
            }
            yield return Shot("12-tab-after-click");
            QuartzShell.Hide();
            // 9-10: the shared layout editor's Undo/Redo and filter rail.
            PrismBridge.OpenLayoutEditor();
            yield return Frames(10);
            yield return Shot("09-10-layout-editor");
            PrismBridge.CloseLayoutEditor();
            yield return Frames(5);
        } finally {
            QuartzShell.Hide();
            if(ummOpen) Umm(true);
        }
        // 11: the hotkey's shortcut menu (the hub look), as built and on Facet's look in Korean.
        yield return ShortcutShots(info);
    }
}
#endif

#if DEBUG && !IL2CPP && !QUARTZ_KEYVIEWER
using System.Collections;
using System.Reflection;
using Quartz.Features;
using Quartz.Prism;
using Quartz.UI.FacetUi.KeyViewerEditor;
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
            yield return Showcase(check, Shot, Page);
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

    // 00: the menu's main screens, for a look at the new layout. A page whose feature is off is turned on for the run only;
    // every switch, the selection, the rail's fold and every popup are put back after.
    private static IEnumerator Showcase(Action<string, bool, string> check, Func<string, IEnumerator> shot,
        Func<string, string, string, IEnumerator> page) {
        var was = FeatureService.Features.ToDictionary(h => h.Id, h => h.Enabled);
        IEnumerator On(string key) {
            string id = FeaturePages.FeatureOf(NavRegistry.ByKey(key));
            if(id != null && !FeatureService.IsEnabled(id)) {
                FeatureService.SetEnabledForRun(id, true);
                yield return Frames(3);
            }
        }
        IEnumerator Shown(string name, string key) {
            yield return On(key);
            yield return page(name, key, null);
        }
        try {
            yield return Shown("00a-home", CorePages.HomePageKey);
            yield return Shown("00b-features", CorePages.FeaturesPageKey);
            yield return Shown("00c-settings", CorePages.SettingsPageKey);

            yield return On(KvEditorPage.PageKey);
            QuartzShell.Open(KvEditorPage.PageKey);
            yield return Frames(5);
            if(!KvEditorDoor.Usable || !KvEditorDoor.Live) {
                check("shots: the key viewer editor is on its page", false, $"usable={KvEditorDoor.Usable} live={KvEditorDoor.Live}");
            } else {
                ScrollView sv = ContentScroll;
                sv.scrollOffset = Vector2.zero;
                yield return shot("00d-keyviewer");
                try {
                    if(KvShowcaseSelect()) yield return shot("00e-keyviewer-key-selected");
                } finally { KvShowcaseClear(); }
                yield return Frames(3);
                try {
                    KvShowcaseMenu(1);   // Edit
                    yield return shot("00f-keyviewer-menu-bar");
                } finally { global::Facet.Popup.CloseAll(); }
                float end = sv.contentContainer.layout.height - sv.contentViewport.layout.height;
                if(end > 1f) {
                    sv.scrollOffset = new Vector2(0f, end / 2f);
                    yield return shot("00g-keyviewer-settings-mid");
                    sv.scrollOffset = new Vector2(0f, end);
                    yield return shot("00h-keyviewer-settings-end");
                    sv.scrollOffset = Vector2.zero;
                }
                QuartzShell.Hide();
                yield return Frames(3);
                try {
                    FacetBridge.OpenKvSwitcher();
                    yield return shot("00i-layouts-switcher");
                } finally { FacetBridge.HideKvSwitcher(); }
            }
            QuartzShell.Hide();
            yield return Frames(3);

            try {
                FacetBridge.OpenShortcut();
                yield return shot("00j-shortcut-menu");
            } finally { FacetBridge.HideShortcut(); }
            yield return Frames(3);

            // The rail folded to its icon strip, set on the Nav itself: the player's kept fold is never written.
            QuartzShell.Open(CorePages.HomePageKey);
            yield return Frames(3);
            global::Facet.Nav nav = QuartzShell.Nav;
            bool folded = nav?.Compact ?? true;
            try {
                if(nav != null) nav.Compact = true;
                yield return shot("00k-sidebar-folded");
            } finally { if(nav != null) nav.Compact = folded; }
            yield return Frames(3);

            yield return Shown("00l-overlay-general", "overlay.general");
            yield return Shown("00m-overlay-judgement", "overlay.judgement");
            yield return Shown("00n-overlay-progressbar", "overlay.progressbar");
            yield return Shown("00o-gameplay-practice", "gameplay.practice");
            yield return Shown("00p-visuals-planetcolors", "visuals.planetcolors");
        } finally {
            global::Facet.Popup.CloseAll();
            QuartzShell.Hide();
            foreach(FeatureService.Handle h in FeatureService.Features)   // a dependency turned on with a feature goes back too
                if(was.TryGetValue(h.Id, out bool on) && h.Enabled != on) FeatureService.SetEnabledForRun(h.Id, on);
        }
        yield return Frames(3);
    }

    // The editor's own selection, as the key viewer checks make it: the first element, so the inspector shows its settings.
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static bool KvShowcaseSelect() {
        var items = KvEditorPage.Model.Elements();
        if(items == null || items.Count == 0) return false;
        KvSelect(items[0]);
        return true;
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static void KvShowcaseClear() => KvEditorPage.Editor?.Canvas.Clear();

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static void KvShowcaseMenu(int menu) => KvEditorPage.Editor?.Menu.Open(menu);
}
#endif

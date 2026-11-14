#if DEBUG && !IL2CPP && !QUARTZ_KEYVIEWER
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using Quartz.Compat.Game;
using Quartz.Core;
using Quartz.Features.KeyViewer;
using Quartz.Features.KeyViewer.Layout;
using Quartz.Game.Stats;
using Quartz.IO;
using Quartz.Overlay;
using Quartz.UI.FacetUi.KeyViewerEditor;
using Quartz.UI.Nav;
using Quartz.UI.Utility;
using UnityEngine;
using UnityEngine.UIElements;
using KeyLayoutText = PrismLib.UI.Toolkit.KeyLayoutText;
using LayoutGroup = PrismLib.UI.Toolkit.LayoutGroup;
using Switcher = PrismLib.UI.Toolkit.KeyLayoutSwitcher<Quartz.Features.KeyViewer.Layout.KvElement>;
namespace Quartz.UI.FacetUi;
/// <summary>
/// The layout switcher's hotkey and its rows, a door forced off, Quartz's shutdown, and the hotkey inside a level and the editor.
/// </summary>
internal static partial class FacetSelfTestChecks {
    // ---- the hotkey and its rows

    private static IEnumerator KvSwitcherHotkeyChecks(Action<string, bool, string> check) {
        CoreSettings c = MainCore.Conf;
        FacetBridge.HideKvSwitcher();
        bool prism = !KvSwitcherHotkey.TrySet((int)Keybind.KeyModifier.Alt, (int)KeyCode.P);
        yield return KvSeconds(0.3f);
        VisualElement taken = Body.Q(name: "MENU_KVE_SWITCHER_KEY_TAKEN");
        bool said = taken != null && taken.resolvedStyle.display == DisplayStyle.Flex;
        bool menu = !KvSwitcherHotkey.TrySet(c.ToggleModifier, c.ToggleKey);
        // A bare key fires with any modifier held, so it may not share a key with the menu's or Prism's chords; Escape closes
        // the switcher, so it may not open it.
        bool bare = !KvSwitcherHotkey.TrySet(0, c.ToggleKey) && !KvSwitcherHotkey.TrySet(0, (int)KeyCode.P)
            && !KvSwitcherHotkey.TrySet(0, (int)KeyCode.Escape);
        check($"{KvSwitcherPage}: Prism's Alt+P, the menu's own chord, a bare menu or Prism key and Escape are refused, and the page says the key is taken",
            prism && menu && bare && said && c.KvSwitcherKey != (int)KeyCode.P && c.KvSwitcherKey != (int)KeyCode.Escape,
            $"prism={prism} menu={menu} bare={bare} said={said}");

        bool cleared = KvSwitcherHotkey.TrySet(0, 0);
        yield return KvSeconds(0.3f);
        VisualElement keyRow = Body.Q(name: "MENU_KVE_SWITCHER_KEY"), modRow = Body.Q(name: "MENU_KVE_SWITCHER_MODIFIER");
        VisualElement card = Body.Q(name: "SECTION_KEY_VIEWER-card") ?? keyRow?.parent;
        check($"{KvSwitcherPage}: the Key Viewer card has the switcher hotkey and modifier rows; unbound, the key is 0 and the taken line is gone",
            cleared && keyRow != null && modRow != null && keyRow.parent == modRow.parent && c.KvSwitcherKey == 0
            && taken?.resolvedStyle.display == DisplayStyle.None, $"key={keyRow != null} mod={modRow != null} value={c.KvSwitcherKey} card={card?.name}");

        bool set = KvSwitcherHotkey.TrySet(0, (int)KeyCode.F9);
        Func<KeyCode, bool> f9 = k => k == KeyCode.F9;
        KvSwitcherHotkey.Tick(f9, _ => true, false);
        bool opened = KvSwShows;
        yield return Frames(1);
        KvSwitcherHotkey.Tick(f9, _ => true, false);
        bool closed = !KvSwShows;
        check($"{KvSwitcherPage}: F9 is accepted and saved; the hotkey opens the switcher, then closes it",
            set && c.KvSwitcherKey == (int)KeyCode.F9 && c.KvSwitcherModifier == 0 && opened && closed, $"set={set} opened={opened} closed={closed}");

        // The key row's reset (a middle click) unbinds it; the row first reads the F9 set above.
        QuartzShell.Changed();
        yield return KvSeconds(0.3f);
        // The page rebuilt since the rows were found: find the key row again.
        VisualElement row = Body.Q(name: "MENU_KVE_SWITCHER_KEY");
        if(row != null) Press(row, 2);
        yield return Frames(1);
        bool reset = c.KvSwitcherKey == 0;
        check($"{KvSwitcherPage}: resetting the key row unbinds the hotkey", reset, $"row={row?.panel != null} key={c.KvSwitcherKey}");
        KvSwitcherHotkey.TrySet(0, (int)KeyCode.F9);

        KvSwitcherHotkey.Tick(f9, _ => true, true);
        bool playing = !KvSwShows;
        KeyCaptureService.Start(_ => { }, null);
        bool capturing = KeyCaptureService.IsCapturing;
        KvSwitcherHotkey.Tick(f9, _ => true, false);
        bool duringCapture = !KvSwShows;
        KeyCaptureService.Cancel();
        yield return Frames(1);
        check($"{KvSwitcherPage}: the hotkey does nothing while a level plays or a key row captures",
            playing && capturing && duringCapture, $"playing={playing} capturing={capturing} duringCapture={duringCapture}");
        FacetBridge.HideKvSwitcher();

        // The other way round: the menu hotkey moved onto the switcher's key clears the switcher's chord, and so does a loaded
        // chord the switcher's row would refuse.
        (int menuMod, int menuKey) = (c.ToggleModifier, c.ToggleKey);
        bool moved;
        try {
            UICore.TrySetMenuHotkey(menuMod, (int)KeyCode.F9);
            moved = c.ToggleKey == (int)KeyCode.F9 && c.KvSwitcherKey == 0;
        } finally {
            // Written back directly: TrySetMenuHotkey would refuse a hand-edited chord and leave the player on F9.
            (c.ToggleModifier, c.ToggleKey) = (menuMod, menuKey);
            MainCore.ConfMgr.RequestSave();
            Quartz.Prism.PrismBridge.RebindMenuKey();
        }
        (c.KvSwitcherModifier, c.KvSwitcherKey) = (0, (int)KeyCode.Escape);
        bool loaded = c.ClearRefusedSwitcherKey() && c.KvSwitcherKey == 0;
        KvSwitcherHotkey.TrySet(0, (int)KeyCode.F9);
        bool kept = !c.ClearRefusedSwitcherKey() && c.KvSwitcherKey == (int)KeyCode.F9;
        check($"{KvSwitcherPage}: a menu hotkey set onto the switcher's key clears the switcher's chord, a refused chord from the file is cleared, an accepted one stays",
            moved && loaded && kept && c.ToggleModifier == menuMod && c.ToggleKey == menuKey, $"moved={moved} loaded={loaded} kept={kept} menu={c.ToggleModifier}+{c.ToggleKey}");
    }

    // ---- a door forced off: no switcher, no hub button, no rows

    private static IEnumerator KvSwitcherForcedOff(Action<string, bool, string> check) {
        FacetBridge.HideKvSwitcher();
        yield return Frames(1);
        KvEditorDoor.ForceGone = true;
        try {
            KvSwitcherHotkey.Tick(k => k == KeyCode.F9, _ => true, false);
            bool hotkey = !KvSwShows;
            FacetBridge.OpenKvSwitcher();
            bool open = !KvSwShows;
            yield return KvSeconds(0.3f);
            VisualElement keyRow = Body.Q(name: "MENU_KVE_SWITCHER_KEY"), modRow = Body.Q(name: "MENU_KVE_SWITCHER_MODIFIER");
            bool rows = keyRow?.resolvedStyle.display == DisplayStyle.None && modRow?.resolvedStyle.display == DisplayStyle.None;
            QuartzShell.Hide();
            UICore.OpenToHub();
            yield return Frames(2);
            Vector2 size = ((RectTransform)KvHub.transform).sizeDelta;
            bool button = KvHub.GetComponentsInChildren<TMPro.TextMeshProUGUI>(true)
                .Any(t => t.text == MainCore.Tr.Get("MENU_KVE_LAYOUTS", "Layouts") && t.gameObject.activeInHierarchy);
            check($"{KvSwitcherPage}: with the door forced off, the hotkey and OpenKvSwitcher open nothing, the hub has no Layouts button and is 260 x 300, and the hotkey rows hide",
                hotkey && open && !button && size == new Vector2(260f, 300f) && rows,
                $"hotkey={hotkey} open={open} button={button} size={size} rows={rows}");
            UICore.ExitReorganize();
        } finally {
            KvEditorDoor.ForceGone = false;
        }
        yield return KvBackOnPage();
    }

    // ---- Quartz unloading leaves no switcher

    private static IEnumerator KvSwitcherShutdown(Action<string, bool, string> check) {
        FacetBridge.OpenKvSwitcher();
        yield return Frames(1);
        bool up = KvSwShows;
        KvEditorDoor.Shutdown();
        yield return Frames(1);
        check($"{KvSwitcherPage}: Quartz's shutdown disposes the switcher", up && KvSw == null && !FacetBridge.KvSwitcherVisible(),
            $"up={up} switcher={KvSw != null}");
        // The page's editor went with it: build it again for whatever runs next.
        QuartzShell.Go(CorePages.HomePageKey);
        yield return Frames(2);
        QuartzShell.Go(KeyViewerPage);
        yield return Frames(3);
    }

    // ---- inside a level: playing, the hotkey does nothing; paused, it opens and a card click switches with counts kept

    internal static IEnumerator KvSwitcherInLevel(Action<string, bool, string> check) {
        if(!KvEditorDoor.Usable) {
            check("level: the key viewer switcher's door is usable", false, "usable=False");
            yield break;
        }
        KvDocument doc = KvStore.Current;
        string from = doc.SelectedTab, other = doc.NewTabId();
        doc.EnsureTab(other, "Self Test Level Hands");
        KvMigration.GenerateStockTab(doc, other, 4);
        global::Facet.History.Clear();
        KvSwitcherHotkey.TrySet(0, (int)KeyCode.F9);
        Func<KeyCode, bool> f9 = k => k == KeyCode.F9;
        scrController ctl = scrController.instance;
        bool playing = GameStats.InGame;
        KvSwitcherHotkey.Tick(f9, _ => true, GameStats.InGame);
        check("level: while playing, the switcher hotkey opens nothing", playing && !KvSwShows, $"inGame={playing} shows={KvSwShows}");
        ctl.paused = true;
        try {
            yield return null;   // GameStats.InGame is read once a frame
            KvSwitcherHotkey.Tick(f9, _ => true, GameStats.InGame);
            yield return Frames(2);
            check("level: paused, the switcher hotkey opens the switcher", KvSwShows, $"inGame={GameStats.InGame} shows={KvSwShows}");
            List<int> counts = [.. doc.AllElements(from).Select(e => e.Count)];
            KvSwClick(KvSwCard(true, other));
            bool switched = KvStore.Current.SelectedTab == other;
            // The overlay rebuilds onto the clicked layout while paused.
            int want = doc.AllElements(other).Count, boxes = -1;
            float until = Time.realtimeSinceStartup + 0.5f;
            while(Time.realtimeSinceStartup < until && (boxes = KvOverlayBoxesOf(other)) != want) yield return null;
            global::Facet.History.Undo();
            yield return Frames(2);
            List<int> after = [.. KvStore.Current.AllElements(from).Select(e => e.Count)];
            check("level: paused, a card click switches the key viewer's layout, and undo switches back with every count unchanged",
                switched && boxes == want && KvStore.Current.SelectedTab == from && after.SequenceEqual(counts),
                $"switched={switched} boxes={boxes}/{want} shown={KvStore.Current.SelectedTab} counts={string.Join(",", counts)} after={string.Join(",", after)}");
        } finally {
            ctl.paused = false;
        }
        yield return null;
        KvSwitcherHotkey.Tick(f9, _ => false, GameStats.InGame);
        check("level: when play resumes, the switcher hides", !KvSwShows, $"inGame={GameStats.InGame} shows={KvSwShows}");
        FacetBridge.HideKvSwitcher();
    }

    // ---- the level editor, editing: not play, so the hotkey opens the switcher; then back to the level select

    internal static IEnumerator KvSwitcherInEditor(Action<string, bool, string> check) {
        scnEditor stale = scnEditor.instance;
        GCS.sceneToLoad = "scnEditor";
        GCS.worldEntrance = null;
        scnEditor.levelToOpenOnLoad = null;
        UnityEngine.SceneManagement.SceneManager.LoadScene("scnEditor");
        float until = Time.realtimeSinceStartup + 30f;
        scnEditor ed = null;
        while(Time.realtimeSinceStartup < until && ((ed = scnEditor.instance) == null || ReferenceEquals(ed, stale) || !ed.initialized))
            yield return null;
        yield return Frames(30);
        bool editing = ed != null && ed.initialized && !ed.playMode
            && UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "scnEditor";
        try {
            KvSwitcherHotkey.TrySet(0, (int)KeyCode.F9);
            Func<KeyCode, bool> f9 = k => k == KeyCode.F9;
            bool inGame = GameStats.InGame;
            KvSwitcherHotkey.Tick(f9, _ => true, GameStats.InGame);
            yield return Frames(2);
            bool opened = KvSwShows;
            KvSwitcherHotkey.Tick(f9, _ => true, GameStats.InGame);
            yield return Frames(1);
            check("editor: editing a level, the switcher hotkey opens the switcher and closes it again",
                editing && !inGame && opened && !KvSwShows, $"editing={editing} strict={ed != null && ed.inStrictlyEditingMode} inGame={inGame} opened={opened} shows={KvSwShows}");
        } finally {
            FacetBridge.HideKvSwitcher();
            GCS.sceneToLoad = GameApi.SceneLevelSelect;
            UnityEngine.SceneManagement.SceneManager.LoadScene(GameApi.SceneLevelSelect);
        }
        yield return Frames(60);
    }
}
#endif

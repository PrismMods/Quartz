#if !QUARTZ_CLASSIC_UI
using Quartz.Async;
using Quartz.Core;
using Quartz.UI.FacetUi;
using Quartz.UI.Objects;
using Quartz.UI.Utility;
using UnityEngine;
namespace Quartz.UI;
/// <summary>
/// The menu core of a build without the classic uGUI menu (every Mono build but the KeyViewer flavour): the new menu is the
/// only menu. It keeps what overlays and hotkeys need from the classic UICore: the arranging state, whether a menu shows,
/// the menu hotkey and Escape. Without Facet or PrismLib.UI's suite shell there is no menu at all, said once in the log;
/// overlays and their hotkeys still work.
/// </summary>
public static partial class UICore {
    /// <summary>Reorganize mode is the classic hub's: never on here.</summary>
    public static bool IsReorganizing => false;
    public static readonly Vector2 ReferenceResolution = new(1920, 1080);
    public const float OUTLINE_WIDTH = 3f;
    /// <summary>PrismLib's shared layout editor is open (inferred from its box reads, see PrismBridge). Overlays show while it is.</summary>
    public static bool SharedLayoutOpen { get; private set; }
    /// <summary>The shared layout editor is moving Quartz's overlays.</summary>
    public static bool IsArranging => SharedLayoutOpen;
    public static event Action<bool> OnArrangingChanged;
    /// <summary>The new menu is up.</summary>
    internal static bool MenuShowing => FacetBridge.NewMenuVisible();
    /// <summary>The new menu can exist (FacetBridge.MenuAvailable): the hotkey's shortcut menu leads to it.</summary>
    internal static bool NewMenuWanted => FacetBridge.MenuAvailable();
    private static bool started, reported, dismissed;
    public static void Initialize() {
        UIColors.ApplyAccent(MainCore.Conf.GetAccentColor());   // overlays and the Facet and Prism windows take the saved accent
        UpdateToast.Initialize();
        if(started) return;
        started = true;
        // The core categories and pages: the classic menu's factories registered them, and the new menu and its search read them.
        Quartz.UI.Nav.CorePages.EnsureRegistered();
        // A pump later, once the runtime is up: the probes below need PrismLib registered.
        MainThread.Enqueue(() => {
            if(!FacetBridge.MenuAvailable()) {
                ReportNoMenu();
                return;
            }
            if(MainCore.Conf.ShowOnStartup) FacetBridge.OpenNewMenu();
        });
    }
    private static void ReportNoMenu() {
        if(reported) return;
        reported = true;
        MainCore.Log.Wrn("No Quartz menu this session: it needs Facet and PrismLib.UI's suite shell (the line above says which is missing). Overlays and their hotkeys still work.");
    }
    public static void Dispose() => UpdateToast.Dispose();
    public static void HandleUpdate() {
        // Polled with no window open too: a Facet window of another mod can be up.
        if(Input.GetKeyDown(KeyCode.Escape) && !Keybind.Capturing) FacetBridge.HandleEscape();
        // A runtime UI Toolkit panel here does not get WheelEvent; Facet scrolls its window from this, and pans a canvas on both axes.
        Vector2 wheel = Input.mouseScrollDelta;
        if(wheel != Vector2.zero) FacetBridge.TickWheel(Input.mousePosition, wheel.y);
        FacetBridge.TickNewMenu();
        FacetBridge.TickKvEditor();
        KvSwitcherHotkey.Tick();
        LayoutHotkey.Tick();
        Keybind.KeyModifier mod = (Keybind.KeyModifier)MainCore.Conf.ToggleModifier;
        KeyCode key = (KeyCode)MainCore.Conf.ToggleKey;
        if(ToggleHotkeyAllowed(Keybind.ModifierHeld(mod), Keybind.KeyDown(key), LayoutHotkey.Ours)) LayoutHotkey.Press(LayoutHotkey.Route());
        ToggleBinds.HandleUpdate();
    }
    // The menu hotkey fires only when nobody is typing (uGUI or Facet) or capturing a key, nor in the frame a capture took it,
    // nor during a layout session unless sessionOk (the session the shortcut menu opened, which the hotkey closes).
    internal static bool ToggleHotkeyAllowed(bool modHeld, bool down, bool sessionOk = false) =>
        modHeld && down && !UIInputBlocker.IsEditing && !Keybind.Capturing && !KeyCaptureCoordinator.ReleasedThisFrame && (sessionOk || !SharedLayoutOpen);
    /// <summary>The hotkey with the shortcut menu's door shut: the new menu shows or hides.</summary>
    public static void Toggle() {
        if(FacetBridge.HideNewMenu()) return;
        if(!FacetBridge.MenuAvailable()) {
            ReportNoMenu();
            return;
        }
        FacetBridge.OpenNewMenu();
    }
    /// <summary>Hides the menu to get it out of the way (a level launch, an overlay); Reopen brings it back.</summary>
    internal static void Dismiss() {
        // With nothing showing (a second Dismiss after the quiz overlay's), the first one's menu stays the one to reopen.
        if(FacetBridge.HideNewMenu()) dismissed = true;
    }
    /// <summary>After a failed launch: back to the menu Dismiss hid, on its page. Nothing while it shows.</summary>
    internal static void Reopen() {
        bool was = dismissed;
        dismissed = false;
        if(was && !MenuShowing) FacetBridge.OpenNewMenu();
    }
    // The flag goes up before the menu hides, so IsArranging stays true across the hand-over.
    internal static void SetSharedLayout(bool open) {
        if(SharedLayoutOpen == open) return;
        SharedLayoutOpen = open;
        if(open) Dismiss();
        try {
            OnArrangingChanged?.Invoke(open);
        } catch(Exception e) {
            MainCore.Log.Err($"[UI] arranging listener threw: {e.Message}");
        }
    }
    /// <summary>
    /// The menu hotkey from a row that can be handed anything (Prism · Settings, the new menu): one Quartz won't take is refused
    /// with one log line, else it is saved and PrismLib's key registry follows.
    /// </summary>
    internal static void TrySetMenuHotkey(int modifier, int key) {
        if(modifier < 0 || modifier > (int)Keybind.KeyModifier.Cmd || !Enum.IsDefined(typeof(KeyCode), key)
            || Quartz.Prism.PrismRules.HotkeyRefused(modifier, key)) {
            MainCore.Log.Msg($"Menu hotkey refused: {Keybind.Format((Keybind.KeyModifier)modifier, (KeyCode)key)}");
            return;
        }
        MainCore.Conf.ToggleModifier = modifier;
        MainCore.Conf.ToggleKey = key;
        MainCore.Conf.ClearRefusedSwitcherKey();   // the switcher may not keep a chord the menu hotkey now fires on
        MainCore.ConfMgr.RequestSave();
        Quartz.Prism.PrismBridge.RebindMenuKey();
    }
}
#endif

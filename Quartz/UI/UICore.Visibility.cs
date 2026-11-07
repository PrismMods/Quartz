using Quartz.Async;
using Quartz.Core;
using Quartz.Localization;
using Quartz.Resource;
using Quartz.UI.FacetUi;
using Quartz.UI.Generator;
using Quartz.UI.Objects;
using Quartz.UI.Panes;
using Quartz.UI.Utility;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Random = UnityEngine.Random;
using GTweens.Tweens;
using GTweens.Builders;
using GTweens.Extensions;
using Quartz.Tween;
using GTweens.Easings;
using GTweenExtensions = GTweens.Extensions.GTweenExtensions;
using TMPro;
namespace Quartz.UI;
public static partial class UICore {
    private static float holdStartTime = 0f;
    private static bool holdingToggle = false;
    private static GTween panelTweener;
    private static GTween resetSequence;
    private static bool isOpen = false;
    public static bool IsOpen => isOpen;
    /// <summary>A Quartz menu is up: the classic uGUI menu or the new one.</summary>
    internal static bool MenuShowing => isOpen || FacetBridge.NewMenuVisible();
    /// <summary>The menu hotkey opens the new menu: the experimental switch is on and Facet can run it.</summary>
    internal static bool NewMenuWanted => MainCore.Conf.ExperimentalNewMenu && FacetBridge.Available();
    public static Vector2 LastPanelPosition;
    public static Vector2 LastPanelSize;
    public static Vector2 DefaultPanelSize {
        get {
            float scale = MainCore.Conf.UIScale <= 0f ? 1f : MainCore.Conf.UIScale;
            return Vector2.Min(new Vector2(1280f / scale, 720f / scale), CanvasSize);
        }
    }
    private static Vector2 MinPanelSize {
        get {
            float scale = MainCore.Conf.UIScale <= 0f ? 1f : MainCore.Conf.UIScale;
            return Vector2.Min(
                new Vector2(ResizeHandle.MIN_WIDTH / scale, ResizeHandle.MIN_HEIGHT / scale),
                CanvasSize
            );
        }
    }
    private static Vector2 ClampToCanvas(Vector2 size) {
        Vector2 canvas = CanvasSize;
        Vector2 min = MinPanelSize;
        return new Vector2(
            Mathf.Clamp(size.x, min.x, Mathf.Max(min.x, canvas.x)),
            Mathf.Clamp(size.y, min.y, Mathf.Max(min.y, canvas.y))
        );
    }
    private static Vector2 LoadSavedPanelSize() {
        Vector2 canvas = CanvasSize;
        float fw = MainCore.Conf.PanelWidthFrac;
        float fh = MainCore.Conf.PanelHeightFrac;
        if(fw <= 0f || fh <= 0f) {
            float legacyW = MainCore.Conf.PanelWidth;
            float legacyH = MainCore.Conf.PanelHeight;
            if(legacyW <= 0f || legacyH <= 0f) return DefaultPanelSize;
            fw = legacyW / canvas.x;
            fh = legacyH / canvas.y;
        }
        return ClampToCanvas(new Vector2(fw * canvas.x, fh * canvas.y));
    }
    public static void SavePanelSize() {
        if(Panel == null) return;
        Vector2 canvas = CanvasSize;
        LastPanelSize = Panel.sizeDelta;
        MainCore.Conf.PanelWidthFrac = Mathf.Clamp01(Panel.sizeDelta.x / canvas.x);
        MainCore.Conf.PanelHeightFrac = Mathf.Clamp01(Panel.sizeDelta.y / canvas.y);
        MainCore.Conf.PanelWidth = 0f;
        MainCore.Conf.PanelHeight = 0f;
        MainCore.ConfMgr.RequestSave();
        float clamped = ClampBand(bandHeight);
        if(!Mathf.Approximately(clamped, bandHeight)) {
            bandHeight = clamped;
            RefreshBand(false);
        }
    }
    private static bool canvasWasVisible;
    private static int lastScreenWidth;
    private static int lastScreenHeight;
    // Same rule as dragging the top bar: on screen vertically, PANEL_KEEP_VISIBLE of it horizontally.
    public static Vector2 ClampPanelPosition(Vector2 position, Vector2 size) {
        Vector2 canvas = CanvasSize;
        float limitX = Mathf.Max((canvas.x + size.x) * 0.5f - PANEL_KEEP_VISIBLE, 0f);
        float maxY = (canvas.y - size.y) * 0.5f;
        float minY = Mathf.Min(-(canvas.y + size.y) * 0.5f + TOP_BAR_HEIGHT, maxY);
        return new Vector2(Mathf.Clamp(position.x, -limitX, limitX), Mathf.Clamp(position.y, minY, maxY));
    }
    private static void HandleResolutionChange() {
        if(Screen.width == lastScreenWidth && Screen.height == lastScreenHeight) return;
        if(Panel == null || !TryGetCanvasSize(out _)) return;
        lastScreenWidth = Screen.width;
        lastScreenHeight = Screen.height;
        LastPanelSize = LoadSavedPanelSize();
        LastPanelPosition = ClampPanelPosition(LastPanelPosition, LastPanelSize);
        Panel.sizeDelta = LastPanelSize;
        Panel.anchoredPosition = LastPanelPosition;
        float clamped = ClampBand(bandHeight);
        if(!Mathf.Approximately(clamped, bandHeight)) {
            bandHeight = clamped;
            RefreshBand(false);
        }
    }
    public static void HandleUpdate() {
        // Polled even with the menu closed: a Facet window can be open on its own.
        if(Input.GetKeyDown(KeyCode.Escape)) EscapeDown();
        // A runtime UI Toolkit panel here does not get WheelEvent; Facet scrolls its window from this.
        float wheel = Input.mouseScrollDelta.y;
        if(wheel != 0f) FacetBridge.TickWheel(Input.mousePosition, wheel);
        FacetBridge.TickNewMenu();
        if(canvasObj == null) return;
        HandleResolutionChange();
        Keybind.KeyModifier mod = (Keybind.KeyModifier)MainCore.Conf.ToggleModifier;
        KeyCode key = (KeyCode)MainCore.Conf.ToggleKey;
        bool modHeld = Keybind.ModifierHeld(mod);
        bool editing = UIInputBlocker.IsEditing;
        bool down = Keybind.KeyDown(key);
        bool held = Keybind.KeyHeld(key);
        bool pressed = !editing && modHeld && held;
        if(ToggleHotkeyAllowed(modHeld, down)) {
            Toggle();
            holdStartTime = Time.unscaledTime;
            holdingToggle = true;
        }
        if(holdingToggle && pressed && Time.unscaledTime - holdStartTime >= 0.4f) {
            // With the switch on the hotkey toggles the new menu: a long press must not reset the classic panel behind it.
            if(isOpen || !NewMenuWanted) ResetScalePosition(!isOpen);
            holdingToggle = false;
        }
        if(!held) holdingToggle = false;
        if(isOpen && !editing && !Keybind.Capturing) HandleShortcuts();
        ToggleBinds.HandleUpdate();
        Quartz.UI.Search.SettingsSearch.Tick();
        if(!canvasObj.activeSelf) {
            if(canvasWasVisible) {
                canvasWasVisible = false;
                UIObject.NotifyHidden();
            }
            return;
        }
        canvasWasVisible = true;
        UIObject.TickAll();
        Tooltip.Tick();
    }
    // The menu hotkey fires only when nobody is typing (uGUI or Facet) or capturing a key, nor in the frame a capture took it.
    internal static bool ToggleHotkeyAllowed(bool modHeld, bool down) =>
        modHeld && down && !UIInputBlocker.IsEditing && !Keybind.Capturing && !KeyCaptureCoordinator.ReleasedThisFrame && !SharedLayoutOpen;
    // The one place an Escape press is decided, in the frame it goes down. A Facet window takes it
    // first and then nothing else happens; otherwise the open menu closes (deferred, see below).
    internal static void EscapeDown() {
        // Synchronously and once: Facet answers one Escape per frame for every loaded mod, so a
        // deferred retry would get false and the menu would close along with the Facet window.
        if(!Keybind.Capturing && FacetBridge.HandleEscape()) return;
        if(isOpen && !UIInputBlocker.IsEditing && !Keybind.Capturing) QueueMenuEscape();
    }
    private static void QueueMenuEscape() {
        int frame = Time.frameCount;
        // Next pump, so a widget's Update later this frame can still claim the key.
        MainThread.Enqueue(() => {
            if(escConsumedFrame == frame || !isOpen) return;
            if(IsReorganizing && !panelOverHub && Reorganizer.Selected != null) Reorganizer.Deselect();
            else if(IsReorganizing && !panelOverHub) Close();
            else ClosePanel();
        });
    }
    private static int escConsumedFrame = -1;
    // For widgets that use Esc themselves (KeyViewer editor selection, key capture): this frame's Esc is theirs.
    internal static void ConsumeEscape() => escConsumedFrame = Time.frameCount;
    private static readonly List<RaycastResult> clickHits = [];
    // A click that lands on none of our UI (handles, hub, panel) means empty screen.
    private static bool PointerOverQuartz() {
        EventSystem es = EventSystem.current;
        if(es == null) return false;
        clickHits.Clear();
        es.RaycastAll(new PointerEventData(es) { position = Input.mousePosition }, clickHits);
        Transform root = MainCore.Root.transform;
        foreach(RaycastResult hit in clickHits)
            if(hit.gameObject != null && hit.gameObject.transform.IsChildOf(root)) return true;
        return false;
    }
    private static void HandleShortcuts() {
        if(IsReorganizing && Reorganizer.Selected != null && Input.GetMouseButtonDown(0) && !PointerOverQuartz()) Reorganizer.Deselect();
        bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)
            || Input.GetKey(KeyCode.LeftCommand) || Input.GetKey(KeyCode.RightCommand);
        if(!ctrl || !MainCore.IsModEnabled) return;
        bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        if(Input.GetKeyDown(KeyCode.Z)) {
            if(shift) UndoTimeline.Redo();
            else UndoTimeline.Undo();
        } else if(Input.GetKeyDown(KeyCode.Y)) UndoTimeline.Redo();
    }
    private static Vector2 GetRandomOffscreenPosition() {
        float halfW = Screen.width * 0.5f;
        float halfH = Screen.height * 0.5f;
        int side = Random.Range(0, 4);
        return side switch {
            0 => new(
                -halfW - Panel.sizeDelta.x,
                Random.Range(-halfH, halfH)
            ),
            1 => new(
                halfW + Panel.sizeDelta.x,
                Random.Range(-halfH, halfH)
            ),
            2 => new(
                Random.Range(-halfW, halfW),
                halfH + Panel.sizeDelta.y
            ),
            _ => new(
                Random.Range(-halfW, halfW),
                -halfH - Panel.sizeDelta.y
            )
        };
    }
    public static void Open(bool noAnimate = false) {
        if(isOpen || Panel == null) return;
        FreshenClassic();   // every classic opener comes through here: pages the new menu left stale rebuild once first
        if(isOpen || Panel == null) return;   // that rebuild's show-on-startup open got there first
        // Never over the new menu: it hides. Opened from here, its Hidden brings this menu back (calling in here
        // again, with it hidden); otherwise this menu opens and a user's close goes back to the new one.
        bool handedBack = MenuHandOff.ReturnTo == MenuHandOff.Menu.Classic;
        if(FacetBridge.HideNewMenu()) {
            if(handedBack) return;
            MenuHandOff.ReturnTo = MenuHandOff.Menu.New;
        }
        isOpen = true;
        Panel.gameObject.SetActive(true);
        if(panelCanvasGroup != null) {
            panelCanvasGroup.alpha = Mathf.Clamp01(MainCore.Conf.PanelOpacity);
            panelCanvasGroup.interactable = true;
            panelCanvasGroup.blocksRaycasts = true;
        }
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        panelTweener.CompleteAndKill();
        resetSequence.CompleteAndKill();
        // Before the noAnimate return: opening the menu completes the tutorial, and ShowOnStartup and Rebuild() pass noAnimate.
        if(firstRunHelperActivated) {
            firstRunHelperActivated = false;
            EndFirstRunHelper();
        }
        if(noAnimate) {
            Panel.anchoredPosition = LastPanelPosition;
            Panel.sizeDelta = LastPanelSize;
            canvasObj.SetActive(true);
            RefreshAllText();
            return;
        }
        Vector2 startPos = GetRandomOffscreenPosition();
        Panel.anchoredPosition = startPos;
        Panel.sizeDelta = LastPanelSize;
        canvasObj.SetActive(true);
        RefreshAllText();
        panelTweener = Panel.GTAnchorPos(LastPanelPosition, 0.25f)
            .SetEasing(Easing.OutExpo);
        MainCore.TC.Play(panelTweener);
    }
    private static int refreshedFontId = -1;
    private static int refreshedCharacterCount = -1;
    private static int refreshedAtlasCount = -1;
    private static void RefreshAllText() {
        if(canvasObj == null) return;
        TMP_FontAsset font = FontManager.MenuFontAsset;
        int fontId = font != null ? font.GetInstanceID() : 0;
        int characterCount = font?.characterTable?.Count ?? 0;
        int atlasCount = font?.atlasTextures?.Length ?? 0;
        if(fontId == refreshedFontId
            && characterCount == refreshedCharacterCount
            && atlasCount == refreshedAtlasCount) {
            return;
        }
        TMP_Text[] texts = canvasObj.GetComponentsInChildren<TMP_Text>(true);
        for(int i = 0; i < texts.Length; i++) {
            if(texts[i] != null) texts[i].ForceMeshUpdate(false, true);
        }
        if(Panel != null) LayoutRebuilder.ForceRebuildLayoutImmediate(Panel);
        refreshedFontId = fontId;
        refreshedCharacterCount = font?.characterTable?.Count ?? 0;
        refreshedAtlasCount = font?.atlasTextures?.Length ?? 0;
    }
    public static void Close(bool noAnimate = false) {
        if(!isOpen) return;
        // A classic menu opened from the new one goes back there; a rebuild's close is no user's.
        bool handBack = !rebuilding && MenuHandOff.ReturnTo == MenuHandOff.Menu.New;
        if(handBack) {
            MenuHandOff.ReturnTo = MenuHandOff.Menu.None;
            noAnimate = true;   // the new menu opens below; a sliding panel would draw over it and take its clicks
        }
        // From the hub the panel is invisible; sliding it out would flash it back in.
        if(IsReorganizing) noAnimate = true;
        ExitReorganize();
        isOpen = false;
        LastPanelPosition = Panel.anchoredPosition;
        LastPanelSize = Panel.sizeDelta;
        CloseImage.color = new Color(
            CloseImage.color.r,
            CloseImage.color.g,
            CloseImage.color.b,
            0f
        );
        panelTweener.CompleteAndKill();
        resetSequence.CompleteAndKill();
        if(noAnimate) canvasObj.SetActive(false);
        else {
            Vector2 targetPos = GetRandomOffscreenPosition();
            panelTweener = Panel
                .GTAnchorPos(targetPos, 0.25f)
                .SetEasing(Easing.OutExpo)
                .OnComplete(() => canvasObj.SetActive(false));
            MainCore.TC.Play(panelTweener);
        }
        if(handBack) FacetBridge.OpenNewMenu();
    }
    /// <summary>Closes the classic menu to get it out of the way (a level launch, an overlay): no hand-back to the new menu, which stays shut too.</summary>
    internal static void Dismiss() {
        MenuHandOff.ReturnTo = MenuHandOff.Menu.None;
        FacetBridge.HideNewMenu();
        Close(true);
    }
    /// <summary>The classic menu on a page, opened from the new menu: a user's close goes back there.</summary>
    internal static void OpenAt(int state) {
        Open();
        if(state >= 0) Factory.MenuFactory.SetState(state);
        if(isOpen) MenuHandOff.ReturnTo = MenuHandOff.Menu.New;
    }
    // The preview opens from the settings panel, often the one over the hub; it comes back to the same place.
    private static bool previewFromHub, previewOverHub;
    internal static void HandToPreview() {
        previewFromHub = IsReorganizing;
        previewOverHub = panelOverHub;
        Close(true);   // the menu's canvas draws above every UI Toolkit panel
    }
    internal static void BackFromPreview() {
        if(!previewFromHub) { Open(); return; }
        Open(true);
        RestoreHub(previewOverHub);
    }
    // The panel's X: settings opened from the hub closes back to the hub, not out of the UI.
    private static void ClosePanel() {
        if(IsReorganizing && panelOverHub) HidePanelToHub();
        else Close();
    }
    public static void Toggle(bool noAnimate = false) {
        // The new menu hides (its Hidden brings back a classic menu it came from), never with the classic one over it.
        if(FacetBridge.HideNewMenu()) return;
        if(isOpen) {
            Close(noAnimate);   // a classic menu opened from the new one goes back there
            return;
        }
        if(NewMenuWanted) {
            FacetBridge.OpenNewMenu();
            if(FacetBridge.NewMenuVisible()) return;   // one that failed to build turned Facet off: the classic menu opens instead
        }
        OpenToHub();
    }
    public static void ResetScalePosition(bool noAnimate = false) {
        Vector2 targetSize = DefaultPanelSize;
        LastPanelPosition = Vector2.zero;
        LastPanelSize = targetSize;
        MainCore.Conf.PanelWidth = 0f;
        MainCore.Conf.PanelHeight = 0f;
        MainCore.Conf.PanelWidthFrac = 0f;
        MainCore.Conf.PanelHeightFrac = 0f;
        MainCore.ConfMgr.RequestSave();
        panelTweener?.Kill();
        resetSequence?.Kill();
        if(noAnimate) {
            Panel.anchoredPosition = LastPanelPosition;
            Panel.sizeDelta = LastPanelSize;
            return;
        }
        resetSequence = GTweenSequenceBuilder.New()
            .Append(Panel.GTAnchorPos(LastPanelPosition, 0.26f).SetEasing(Easing.OutExpo))
            .Join(Panel.GTSizeDelta(LastPanelSize, 0.26f).SetEasing(Easing.OutExpo))
            .Build();
        MainCore.TC.Play(resetSequence);
    }
    private static bool isMenuOpen = false;
    /// <summary>Single-page shell for the KeyViewer flavour: no rail, no submenu.</summary>
    internal static bool ShellNavHidden => Info.KeyViewerOnly;
    private static Vector2 MenuOpenPosition => new(0f, -TOP_BAR_HEIGHT * 0.5f);
    private static Vector2 MenuClosedPosition => new(-MENU_WIDTH, -TOP_BAR_HEIGHT * 0.5f);
    public static void OpenMenu() {
        if(ShellNavHidden) return;
        isMenuOpen = true;
        Menu.anchoredPosition = MenuClosedPosition;
        menuCanvasGroup.interactable = true;
        menuCanvasGroup.blocksRaycasts = true;
        ApplyShellLayout(true, 0.6f);
    }
    public static void CloseMenu() {
        SetRailExpanded(false);
        menuCanvasGroup.interactable = false;
        menuCanvasGroup.blocksRaycasts = false;
        isMenuOpen = false;
        ApplyShellLayout(true, 0.4f);
    }
    public static void ToggleMenu() {
        if(ShellNavHidden) return;
        if(isMenuOpen) CloseMenu();
        else OpenMenu();
    }
}

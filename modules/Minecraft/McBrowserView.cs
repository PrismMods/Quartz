#nullable enable
using Quartz.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using VoltstroStudios.UnityWebBrowser.Shared;
using VoltstroStudios.UnityWebBrowser.Shared.Events;
namespace Quartz.Features.Minecraft;
public sealed class McBrowserView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler {
    public const string ClassicUrl = "https://classic.minecraft.net/";
    private McEngine? engine;
    private RawImage? surface;
    private Texture2D? texture;
    private RectTransform? rect;
    private bool pointerInside;
    private Vector2Int lastMouse = new(int.MinValue, int.MinValue);
    private readonly List<WindowsKey> keysDown = [];
    private readonly List<WindowsKey> keysUp = [];
    private bool lookActive;
    private Vector2 virtualPointer;
    private float injectTimer;
    private int injectsLeft = 8;
    public float LookSensitivity { get; set; } = 12f;
    private int lastWidth;
    private int lastHeight;
    public string DataRoot { get; set; } = string.Empty;
    public int FrameRate { get; set; } = 60;
    private void Awake() {
        surface = GetComponent<RawImage>();
        rect = GetComponent<RectTransform>();
    }
    // Pages are built once then toggled with SetActive, and closing the menu
    // deactivates the whole Quartz canvas, so OnDisable is the single signal for
    // both "tab switched away" and "menu closed". Killing the engine here is what
    // keeps gameplay free of any browser cost.
    private void OnEnable() {
        faulted = false;
        if(surface == null) surface = GetComponent<RawImage>();
        if(rect == null) rect = GetComponent<RectTransform>();
        if(string.IsNullOrEmpty(DataRoot)) {
            MainCore.Log.Err("[Minecraft] view enabled with no data root; browser cannot start.");
            return;
        }
        if(!McPaths.IsInstalled(DataRoot)) return;
        engine ??= new McEngine(DataRoot);
        Vector2 size = Size();
        lastWidth = (int)size.x;
        lastHeight = (int)size.y;
        if(!engine.Start(ClassicUrl, lastWidth, lastHeight, FrameRate))
            MainCore.Log.Wrn("[Minecraft] browser failed to start.");
    }
    private void OnDisable() => Teardown();
    private void OnDestroy() {
        engine?.Stop(true);
        engine?.Dispose();
        engine = null;
        if(texture != null) {
            Destroy(texture);
            texture = null;
        }
    }
    private void OnApplicationQuit() => engine?.Stop(true);
    private void Teardown() {
        ReleaseLook();
        injectsLeft = 8;
        injectTimer = 0f;
        engine?.Stop();
        lastMouse = new Vector2Int(int.MinValue, int.MinValue);
    }
    // Whole frames cross the IPC socket uncompressed, and the transport tops out
    // around 60-80 MB/s, so frame size sets the frame rate: 1280x720 (3.6 MB/frame)
    // measured a stable 17.6 fps, while 854x480 (1.6 MB/frame) reached 42-58. Cap the
    // engine's resolution and let the RawImage scale it back up — Minecraft Classic is
    // chunky enough that bilinear upscaling costs little. Raise this for sharpness at
    // the cost of frame rate.
    public int MaxRenderHeight { get; set; } = 480;
    private Vector2 Size() {
        Vector2 size = rect == null ? new Vector2(1280f, 720f) : rect.rect.size;
        float width = Mathf.Clamp(size.x, 320f, 1920f);
        float height = Mathf.Clamp(size.y, 180f, 1080f);
        if(height > MaxRenderHeight) {
            width *= MaxRenderHeight / height;
            height = MaxRenderHeight;
        }
        return new Vector2(Mathf.Max(320f, width), height);
    }
    private bool faulted;
    private void Update() {
        if(faulted) return;
        try { Tick(); } catch(Exception e) {
            // A per-frame throw would otherwise spam the log with no context and no way
            // to tell which stage broke. Report once, with the stack, then stand down.
            faulted = true;
            MainCore.Log.Err("[Minecraft] view update failed, browser disabled: " + e);
        }
    }
    private void Tick() {
        if(engine == null) return;
        if(!engine.Running) {
            // Start is refused while a previous teardown unwinds; pick it up here
            // instead of leaving the tab permanently blank.
            if(!engine.Starting && !engine.Stopping && McPaths.IsInstalled(DataRoot))
                engine.Start(ClassicUrl, lastWidth, lastHeight, FrameRate);
            return;
        }
        Vector2 size = Size();
        int width = (int)size.x;
        int height = (int)size.y;
        if(width != lastWidth || height != lastHeight) {
            lastWidth = width;
            lastHeight = height;
            engine.Resize(width, height);
        }
        PumpShim();
        PumpInput();
        int expected = engine.Width * engine.Height * 4;
        if(engine.FrameLength != expected) return;
        if(texture == null || texture.width != engine.Width || texture.height != engine.Height) {
            if(texture != null) Destroy(texture);
            texture = new Texture2D(engine.Width, engine.Height, TextureFormat.BGRA32, false) { filterMode = FilterMode.Bilinear };
            if(surface != null) {
                surface.texture = texture;
                // CEF hands over its framebuffer top-down; Unity samples bottom-up, so
                // the page renders upside down without inverting V.
                surface.uvRect = new Rect(0f, 1f, 1f, -1f);
            }
        }
        Texture2D target = texture;
        try {
            engine.TryApplyFrame(data => {
                target.LoadRawTextureData(data);
                target.Apply(false);
            });
        } catch(Exception e) { Diag.Ignore(e); }
    }
    // The page installs its own handlers as it boots, and Quartz has no LoadFinish
    // callback (the client-callback service is internal to UnityWebBrowser), so nudge
    // the shim in a few times early. It self-guards against running twice.
    private void PumpShim() {
        if(engine == null || injectsLeft <= 0) return;
        injectTimer -= Time.unscaledDeltaTime;
        if(injectTimer > 0f) return;
        injectTimer = 2f;
        injectsLeft--;
        engine.ExecuteJs(McPointerLock.Script);
    }
    private void ReleaseLook() {
        if(!lookActive) return;
        lookActive = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
    private void PumpInput() {
        if(engine == null || rect == null) return;
        if(Input.GetKeyDown(KeyCode.Escape)) ReleaseLook();
        if(!lookActive && !pointerInside) return;
        if(McKeyboard.Collect(keysDown, keysUp) || Input.inputString.Length > 0)
            engine.SendKeyboard([.. keysDown], [.. keysUp], Input.inputString);
        int x;
        int y;
        if(lookActive) {
            // The OS cursor is pinned, so position comes from deltas. Wrapping back to
            // the middle keeps the look infinite; the shim drops the jump.
            virtualPointer.x += Input.GetAxisRaw("Mouse X") * LookSensitivity;
            virtualPointer.y -= Input.GetAxisRaw("Mouse Y") * LookSensitivity;
            float marginX = engine.Width * 0.2f;
            float marginY = engine.Height * 0.2f;
            if(virtualPointer.x < marginX || virtualPointer.x > engine.Width - marginX
                || virtualPointer.y < marginY || virtualPointer.y > engine.Height - marginY)
                virtualPointer = new Vector2(engine.Width * 0.5f, engine.Height * 0.5f);
            x = (int)virtualPointer.x;
            y = (int)virtualPointer.y;
        } else {
            if(!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, Input.mousePosition, null, out Vector2 local)) return;
            Rect bounds = rect.rect;
            x = (int)((local.x - bounds.x) / Mathf.Max(1f, bounds.width) * engine.Width);
            y = (int)((1f - (local.y - bounds.y) / Mathf.Max(1f, bounds.height)) * engine.Height);
        }
        // One RPC per frame regardless of movement queued behind the pump's multi-megabyte
        // GetPixels on the shared lock. Only speak when something actually moved.
        if(x != lastMouse.x || y != lastMouse.y) {
            lastMouse = new Vector2Int(x, y);
            engine.SendMouseMove(x, y);
        }
        if(pointerInside && !lookActive && Input.GetMouseButtonDown(0)) {
            lookActive = true;
            virtualPointer = new Vector2(engine.Width * 0.5f, engine.Height * 0.5f);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        PumpButton(x, y, 0, MouseClickType.Left);
        PumpButton(x, y, 1, MouseClickType.Right);
        PumpButton(x, y, 2, MouseClickType.Middle);
        float scroll = Input.mouseScrollDelta.y;
        if(Mathf.Abs(scroll) > 0.01f) engine.SendMouseScroll(x, y, (int)(scroll * 100f));
    }
    private void PumpButton(int x, int y, int button, MouseClickType type) {
        if(engine == null) return;
        if(Input.GetMouseButtonDown(button)) engine.SendMouseClick(x, y, 1, type, MouseEventType.Down);
        else if(Input.GetMouseButtonUp(button)) engine.SendMouseClick(x, y, 1, type, MouseEventType.Up);
    }
    public void OnPointerEnter(PointerEventData eventData) => pointerInside = true;
    public void OnPointerExit(PointerEventData eventData) {
        pointerInside = false;
        lastMouse = new Vector2Int(int.MinValue, int.MinValue);
        engine?.SendMouseMove(-1, -1);
    }
}

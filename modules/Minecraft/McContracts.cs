#nullable enable
using System.Numerics;
using VoltRpc.Communication;
using VoltstroStudios.UnityWebBrowser.Shared.Events;
namespace VoltstroStudios.UnityWebBrowser.Shared.Core;
// UnityWebBrowser marks its RPC control interfaces internal, with InternalsVisibleTo
// for only its own Runtime, its tests, and the engine — so Quartz cannot reference
// them. VoltRpc keys methods by the plain "Namespace.Type.Method" string and derives
// that from the Type handed to AddService, so re-declaring the contract here with a
// byte-identical namespace, name, order and signature set produces exactly the same
// wire keys. VoltRpc verifies the contract when it connects and throws
// SyncServiceMissMatchException on any drift, so a mistake here fails loudly at
// startup rather than corrupting the stream. The event DTOs stay upstream's: those
// are public. Keep this in upstream's declaration order.
public interface IEngineControls {
    PixelsEvent GetPixels();
    void Shutdown();
    void SendKeyboardEvent(KeyboardEvent keyboardEvent);
    void SendMouseMoveEvent(MouseMoveEvent mouseMoveEvent);
    void SendMouseClickEvent(MouseClickEvent mouseClickEvent);
    void SendMouseScrollEvent(MouseScrollEvent mouseScrollEvent);
    Vector2 GetScrollPosition();
    void GoForward();
    void GoBack();
    void Refresh();
    void LoadUrl(string url);
    void LoadHtml(string html);
    void ExecuteJs(string js);
    void SetZoomLevel(double zoomLevel);
    double GetZoomLevel();
    void OpenDevTools();
    void Resize(Resolution resolution);
    void AudioMute(bool muted);
}
internal sealed class McEngineProxy(Client client) : IEngineControls {
    private const string Prefix = "VoltstroStudios.UnityWebBrowser.Shared.Core.IEngineControls.";
    private readonly Client client = client;
    private readonly object gate = new();
    private readonly object[] oneParameter = new object[1];
    // VoltRpc writes a request and reads its response over ONE socket, so it is not
    // thread safe. Quartz calls in from two threads — the pixel pump polling
    // GetPixels and Unity's main thread forwarding input — and interleaving them
    // desynchronises the stream, which surfaces as bogus "method does not exist" and
    // decode errors on whichever call reads someone else's reply. Every call funnels
    // through here, so the lock cannot be forgotten at a call site.
    private object[] Call(string method) {
        lock(gate) return client.InvokeMethod(Prefix + method, Array.Empty<object>()) ?? [];
    }
    private object[] Call<T>(string method, T parameter) {
        lock(gate) {
            oneParameter[0] = parameter!;
            try { return client.InvokeMethod(Prefix + method, oneParameter) ?? []; }
            finally { oneParameter[0] = null!; }
        }
    }
    public PixelsEvent GetPixels() {
        object[] result = Call(nameof(GetPixels));
        return result.Length > 0 && result[0] is PixelsEvent pixels ? pixels : default;
    }
    public void Shutdown() => Call(nameof(Shutdown));
    public void SendKeyboardEvent(KeyboardEvent keyboardEvent) => Call(nameof(SendKeyboardEvent), keyboardEvent);
    public void SendMouseMoveEvent(MouseMoveEvent mouseMoveEvent) => Call(nameof(SendMouseMoveEvent), mouseMoveEvent);
    public void SendMouseClickEvent(MouseClickEvent mouseClickEvent) => Call(nameof(SendMouseClickEvent), mouseClickEvent);
    public void SendMouseScrollEvent(MouseScrollEvent mouseScrollEvent) => Call(nameof(SendMouseScrollEvent), mouseScrollEvent);
    public Vector2 GetScrollPosition() {
        object[] result = Call(nameof(GetScrollPosition));
        return result.Length > 0 && result[0] is Vector2 position ? position : default;
    }
    public void GoForward() => Call(nameof(GoForward));
    public void GoBack() => Call(nameof(GoBack));
    public void Refresh() => Call(nameof(Refresh));
    public void LoadUrl(string url) => Call(nameof(LoadUrl), url);
    public void LoadHtml(string html) => Call(nameof(LoadHtml), html);
    public void ExecuteJs(string js) => Call(nameof(ExecuteJs), js);
    public void SetZoomLevel(double zoomLevel) => Call(nameof(SetZoomLevel), zoomLevel);
    public double GetZoomLevel() {
        object[] result = Call(nameof(GetZoomLevel));
        return result.Length > 0 && result[0] is double zoom ? zoom : 0d;
    }
    public void OpenDevTools() => Call(nameof(OpenDevTools));
    public void Resize(Resolution resolution) => Call(nameof(Resize), resolution);
    public void AudioMute(bool muted) => Call(nameof(AudioMute), muted);
}

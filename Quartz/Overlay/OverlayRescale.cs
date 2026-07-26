using Quartz.Core;
namespace Quartz.Overlay;
public static class OverlayRescale {
    private static readonly List<(string Id, Action<float, float> Rescale)> targets = [];
    public static IReadOnlyList<string> RegisteredIds => targets.ConvertAll(t => t.Id);
    public static void Register(string id, Action<float, float> rescale) {
        if(string.IsNullOrWhiteSpace(id) || rescale == null)
            throw new ArgumentException("a rescalable overlay needs an id and a delegate");
        Unregister(id);
        targets.Add((id, rescale));
    }
    public static void Unregister(string id) {
        if(string.IsNullOrEmpty(id)) return;
        targets.RemoveAll(t => t.Id == id);
    }
    public static void ApplyAll(float fx, float fy) {
        foreach((string id, Action<float, float> rescale) in targets.ToArray()) {
            try {
                rescale(fx, fy);
            } catch(Exception e) {
                MainCore.Log.Err($"[Overlay] rescaling '{id}' failed: {e}");
            }
        }
    }
}

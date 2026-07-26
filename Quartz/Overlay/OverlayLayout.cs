using Quartz.Core;
namespace Quartz.Overlay;
public static class OverlayLayout {
    private static readonly Dictionary<string, Func<float>> bottomEdges = new(StringComparer.Ordinal);
    public static void Reserve(string id, Func<float> bottomEdge) {
        if(string.IsNullOrWhiteSpace(id) || bottomEdge == null)
            throw new ArgumentException("a reserved overlay band needs an id and a delegate");
        bottomEdges[id] = bottomEdge;
    }
    public static void Release(string id) {
        if(!string.IsNullOrEmpty(id)) bottomEdges.Remove(id);
    }
    public static float BottomEdge(string id, float fallback) {
        if(id == null || !bottomEdges.TryGetValue(id, out Func<float> provider)) return fallback;
        try {
            return provider();
        } catch(Exception e) {
            MainCore.Log.Err($"[Overlay] '{id}' band query failed: {e}");
            return fallback;
        }
    }
}

using UnityEngine;
namespace Quartz.Resource;
public static class ProceduralTexture {
    public delegate float CoverageFn(float x, float y);
    public static Texture2D Circle(int radius) {
        float c = radius - 0.5f;
        return Generate(radius * 2, (x, y) => CircleCoverage(x - c, y - c, radius));
    }
    public static Texture2D CircleHalfTop(int radius) {
        float c = radius - 0.5f;
        return Generate(radius * 2, (x, y) => y <= c ? 1f : CircleCoverage(x - c, y - c, radius));
    }
    public static Texture2D CircleOutline(int radius, int stroke) {
        float c = radius - 0.5f;
        return Generate(radius * 2, (x, y) => {
            float d = Mathf.Sqrt(((x - c) * (x - c)) + ((y - c) * (y - c)));
            float outer = Mathf.Clamp01(radius - d + 0.5f);
            float inner = Mathf.Clamp01(radius - stroke - d + 0.5f);
            return outer - inner;
        }, mip: false);
    }
    private static float CircleCoverage(float dx, float dy, float radius) {
        float d = Mathf.Sqrt((dx * dx) + (dy * dy));
        return Mathf.Clamp01(radius - d + 0.5f);
    }
    private static Texture2D Generate(int size, CoverageFn coverage, bool mip = true) {
        Texture2D tex = new(size, size, TextureFormat.RGBA32, mip, true);
        for(int y = 0; y < size; y++) {
            for(int x = 0; x < size; x++) {
                float a = Mathf.Clamp01(coverage(x, y));
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply(mip, true);
        tex.filterMode = mip ? FilterMode.Trilinear : FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        return tex;
    }
}

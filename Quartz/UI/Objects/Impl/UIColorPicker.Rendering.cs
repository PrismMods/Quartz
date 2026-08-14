using UnityEngine;
namespace Quartz.UI.Objects.Impl;
public sealed partial class UIColorPicker {
    private void UpdateTexture() {
        if(renderedHue >= 0f && Mathf.Abs(Mathf.DeltaAngle(renderedHue * 360f, hue * 360f)) < 0.5f) return;
        renderedHue = hue;
        Color hueColor = Color.HSVToRGB(hue, 1f, 1f);
        Color32[] pixels = new Color32[TextureSize * TextureSize];
        Vector2 huePoint = Direction(hue) * TriangleRadius;
        Vector2 whitePoint = Direction(hue + (1f / 3f)) * TriangleRadius;
        Vector2 blackPoint = Direction(hue - (1f / 3f)) * TriangleRadius;
        for(int y = 0; y < TextureSize; y++) {
            for(int x = 0; x < TextureSize; x++) {
                Vector2 point = new(
                    ((x + 0.5f) / TextureSize) - 0.5f,
                    ((y + 0.5f) / TextureSize) - 0.5f
                );
                float distance = point.magnitude;
                Color color = Color.clear;
                if(distance is >= RingInner and <= RingOuter) {
                    float angle = Mathf.Repeat(Mathf.Atan2(point.y, point.x) / (Mathf.PI * 2f), 1f);
                    color = Color.HSVToRGB(angle, 1f, 1f);
                } else if(Barycentric(point, huePoint, whitePoint, blackPoint, out Vector3 weights)) {
                    color = (hueColor * weights.x) + (Color.white * weights.y) + (Color.black * weights.z);
                    color.a = 1f;
                }
                pixels[(y * TextureSize) + x] = color;
            }
        }
        texture.SetPixels32(pixels);
        texture.Apply(false, false);
    }
    private static Vector2 Direction(float turns) {
        float radians = turns * Mathf.PI * 2f;
        return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
    }
    private static bool Barycentric(Vector2 point, Vector2 a, Vector2 b, Vector2 c, out Vector3 weights) {
        Vector2 v0 = b - a;
        Vector2 v1 = c - a;
        Vector2 v2 = point - a;
        float denominator = (v0.x * v1.y) - (v1.x * v0.y);
        if(Mathf.Abs(denominator) < 0.00001f) {
            weights = default;
            return false;
        }
        float y = ((v2.x * v1.y) - (v1.x * v2.y)) / denominator;
        float z = ((v0.x * v2.y) - (v2.x * v0.y)) / denominator;
        float x = 1f - y - z;
        weights = new Vector3(x, y, z);
        return x >= 0f && y >= 0f && z >= 0f;
    }
    private static Vector2 ClosestPointOnTriangle(Vector2 point, Vector2 a, Vector2 b, Vector2 c) {
        if(Barycentric(point, a, b, c, out _)) return point;
        Vector2 ab = ClosestPointOnSegment(point, a, b);
        Vector2 bc = ClosestPointOnSegment(point, b, c);
        Vector2 ca = ClosestPointOnSegment(point, c, a);
        float abDistance = (point - ab).sqrMagnitude;
        float bcDistance = (point - bc).sqrMagnitude;
        float caDistance = (point - ca).sqrMagnitude;
        if(abDistance <= bcDistance && abDistance <= caDistance) return ab;
        return bcDistance <= caDistance ? bc : ca;
    }
    private static Vector2 ClosestPointOnSegment(Vector2 point, Vector2 a, Vector2 b) {
        Vector2 segment = b - a;
        float length = segment.sqrMagnitude;
        if(length <= 0.00001f) return a;
        return a + (segment * Mathf.Clamp01(Vector2.Dot(point - a, segment) / length));
    }
}

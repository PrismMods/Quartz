using UnityEngine;
namespace Quartz.Features.Tuf;
// The difficulty scale's colours, names and gradient, which the new menu's TUF filters draw too; the classic slider is the rest.
internal sealed partial class TufDifficultyRangeBar {
    internal static readonly Color[] QuantumStops = {
        new Color32(255, 255, 255, 255), new Color32(241, 161, 5, 255), new Color32(235, 123, 41, 255),
        new Color32(227, 85, 74, 255), new Color32(192, 52, 94, 255), new Color32(214, 16, 136, 255),
        new Color32(113, 73, 164, 255), new Color32(63, 32, 103, 255), new Color32(47, 43, 54, 255),
        new Color32(126, 0, 0, 255), new Color32(255, 254, 254, 255)
    };
    internal static readonly Color[] PguStops = {
        new Color32(0, 153, 255, 255), new Color32(0, 255, 136, 255), new Color32(242, 167, 0, 255),
        new Color32(225, 79, 79, 255), new Color32(210, 0, 151, 255), new Color32(45, 29, 65, 255),
        new Color32(0, 0, 0, 255)
    };
    internal static string[] CompactNames(IReadOnlyList<string> names) {
        string[] compact = new string[names.Count];
        for(int i = 0; i < names.Count; i++) {
            int space = names[i].IndexOf(' ');
            compact[i] = space < 0 ? names[i] : names[i][..space];
        }
        return compact;
    }
    internal static Texture2D BuildGradientTexture(int width, Color[] stops) {
        Texture2D texture = new(width, 1, TextureFormat.RGBA32, false) {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.HideAndDontSave
        };
        Color32[] pixels = new Color32[width];
        float span = 1f / (stops.Length - 1);
        for(int x = 0; x < width; x++) {
            float t = (float)x / (width - 1);
            int index = Mathf.Min(Mathf.FloorToInt(t / span), stops.Length - 2);
            float local = (t - index * span) / span;
            pixels[x] = Color.Lerp(stops[index], stops[index + 1], local);
        }
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        return texture;
    }
}

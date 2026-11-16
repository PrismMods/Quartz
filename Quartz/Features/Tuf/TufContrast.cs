namespace Quartz.Features.Tuf;
/// <summary>
/// A TUF difficulty colour made readable on a card, worked out once when the card is built: the hue is kept, a dark colour is
/// lightened toward white just enough to reach MinContrast against the dark backdrop and gets a light glow, a light colour keeps
/// a dark shadow. Plain floats (sRGB 0-1) so the offline tests can pin it for every colour.
/// </summary>
public static class TufContrast {
    public const float MinContrast = 3f;
    /// <summary>The dark backdrop the card and a dark thumbnail come to (#1C1C20) and a light thumbnail's (#F0F0F0).</summary>
    public static readonly (float R, float G, float B) Dark = (0x1C / 255f, 0x1C / 255f, 0x20 / 255f), Light = (0xF0 / 255f, 0xF0 / 255f, 0xF0 / 255f);
    /// <summary>Below this luminance a colour takes the light glow; at or above it, the dark shadow.</summary>
    public const float GlowBelow = 0.2f;
    public const float GlowAlpha = 0.55f, ShadowAlpha = 0.75f;

    /// <summary>The text (and rail) colour to draw, and whether its shadow is the light glow (else the dark shadow).</summary>
    public static ((float R, float G, float B) Colour, bool Glow) Readable(float r, float g, float b) {
        bool glow = Luminance(r, g, b) < GlowBelow;
        (float R, float G, float B) c = (r, g, b);
        // ponytail: 5% steps toward white, at most 20; a closed-form tint is not worth it for one colour per card.
        for(int i = 1; i <= 20 && Contrast(c, Dark) < MinContrast; i++) {
            float t = i * 0.05f;
            c = (r + (1f - r) * t, g + (1f - g) * t, b + (1f - b) * t);
        }
        return (c, glow);
    }

    /// <summary>The shadow's colour once laid over bg: white or black at its alpha.</summary>
    public static (float R, float G, float B) ShadowOver(bool glow, (float R, float G, float B) bg) {
        float v = glow ? 1f : 0f, a = glow ? GlowAlpha : ShadowAlpha;
        return (bg.R + (v - bg.R) * a, bg.G + (v - bg.G) * a, bg.B + (v - bg.B) * a);
    }

    /// <summary>WCAG relative luminance of an sRGB colour.</summary>
    public static float Luminance(float r, float g, float b) => 0.2126f * Lin(r) + 0.7152f * Lin(g) + 0.0722f * Lin(b);

    public static float Contrast((float R, float G, float B) x, (float R, float G, float B) y) {
        float a = Luminance(x.R, x.G, x.B), b = Luminance(y.R, y.G, y.B);
        return (System.Math.Max(a, b) + 0.05f) / (System.Math.Min(a, b) + 0.05f);
    }

    private static float Lin(float c) => c <= 0.04045f ? c / 12.92f : (float)System.Math.Pow((c + 0.055f) / 1.055f, 2.4f);
}

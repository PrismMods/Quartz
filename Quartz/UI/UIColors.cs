using UnityEngine;
namespace Quartz.UI;
public static class UIColors {
    public struct Palette {
        public Color PanelBG;
        public Color TopBar;
        public Color MenuBG;
        public Color MenuNormal;
        public Color MenuHover;
        public Color MenuSelected;
        public Color MenuHighlight;
        public Color ObjectBG;
        public Color ObjectActive;
        public Color ObjectInactive;
        public Color ObjectActiveBright;
        public Color ObjectActiveLightBright;
        public Color ObjectButton;
        public Color SoftRed;
        public Color TextSecondary;
    }
    // Quartz design system (Modlist, red variation). Greys are tinted toward the accent;
    // the defaults below are the system's values for its coral ol-accent #ff8a8f.
    private static readonly Color BaseSoftRed = new(1f, 0.478f, 0.271f, 1f); // danger #ff7a45
    public static readonly Color Border = new(1f, 1f, 1f, 0.09f); // border-strong
    public static Color PanelBG { get; private set; } = new(0.094f, 0.067f, 0.071f, 1f); // bg #181112
    public static Color TopBar { get; private set; } = new(0.157f, 0.110f, 0.118f, 1f); // surface #281c1e
    public static Color MenuBG { get; private set; } = new(0.157f, 0.110f, 0.118f, 1f);
    public static Color MenuNormal { get; private set; } = new(0.294f, 0.227f, 0.243f, 0f);
    public static Color MenuHover { get; private set; } = new(0.294f, 0.227f, 0.243f, 1f); // ol-control #4b3a3e
    public static Color MenuSelected { get; private set; } = new(0.294f, 0.227f, 0.243f, 1f); // surface-3, the active nav link
    public static Color MenuHighlight { get; private set; } = new(0.350f, 0.289f, 0.304f, 1f);
    public static Color ObjectBG { get; private set; } = new(0.294f, 0.227f, 0.243f, 1f);
    public static Color ObjectActive { get; private set; } = new(1f, 0.541f, 0.561f, 1f); // ol-accent #ff8a8f
    public static Color ObjectInactive { get; private set; } = new(0.710f, 0.502f, 0.541f, 1f); // ol-muted #b5808a
    public static Color ObjectActiveBright { get; private set; } = new(1f, 0.827f, 0.820f, 1f); // quartz-tint #ffd3d1
    public static Color ObjectActiveLightBright { get; private set; } = new(1f, 0.624f, 0.640f, 1f);
    public static Color ObjectButton { get; private set; } = new(0.788f, 0.231f, 0.255f, 1f);
    // quartz-red, the logo's face; the logo's shade and facet reuse ObjectButton and ObjectActiveBright.
    public static Color LogoFace { get; private set; } = new(0.898f, 0.282f, 0.302f, 1f);
    public static Color TextSecondary { get; private set; } = new(0.788f, 0.710f, 0.722f, 1f); // text-secondary #c9b5b8
    public static readonly Color ObjectActiveMathOk = new(0.588f, 1f, 0.569f, 1f);
    public static readonly Color ObjectActiveMathWarn = new(1f, 0.898f, 0.569f, 1f);
    public static readonly Color ObjectActiveMathErr = new(1f, 0.569f, 0.569f, 1f);
    public static readonly Color ChannelR = new(1f, 0.478f, 0.478f, 1f);
    public static readonly Color ChannelG = new(0.427f, 0.867f, 0.478f, 1f);
    public static readonly Color ChannelB = new(0.478f, 0.647f, 1f, 1f);
    public static Color SoftRed { get; private set; } = BaseSoftRed;
    public static Palette Current => new() {
        PanelBG = PanelBG,
        TopBar = TopBar,
        MenuBG = MenuBG,
        MenuNormal = MenuNormal,
        MenuHover = MenuHover,
        MenuSelected = MenuSelected,
        MenuHighlight = MenuHighlight,
        ObjectBG = ObjectBG,
        ObjectActive = ObjectActive,
        ObjectInactive = ObjectInactive,
        ObjectActiveBright = ObjectActiveBright,
        ObjectActiveLightBright = ObjectActiveLightBright,
        ObjectButton = ObjectButton,
        SoftRed = SoftRed,
        TextSecondary = TextSecondary
    };
    public static void ApplyAccent(Color accent) {
        accent.r = Mathf.Clamp01(accent.r);
        accent.g = Mathf.Clamp01(accent.g);
        accent.b = Mathf.Clamp01(accent.b);
        accent.a = 1f;
        PanelBG = Tint(0.037f, accent, 0.059f);
        TopBar = Tint(0.061f, accent, 0.102f);
        MenuBG = TopBar;
        Color control = Tint(0.173f, accent, 0.146f);
        MenuNormal = WithAlpha(control, 0f);
        MenuHover = control;
        ObjectButton = Shade(accent, 1.54f, 0.788f);
        MenuSelected = control;
        MenuHighlight = Color.Lerp(control, Color.white, 0.08f);
        ObjectBG = control;
        ObjectActive = accent;
        ObjectInactive = Shade(accent, 0.64f, 0.71f);
        ObjectActiveBright = Color.Lerp(accent, Color.white, 0.6f);
        ObjectActiveLightBright = Color.Lerp(accent, Color.white, 0.18f);
        TextSecondary = Tint(0.745f, accent, 0.17f);
        LogoFace = Shade(accent, 1.5f, 0.898f);
        SoftRed = BaseSoftRed;
    }
    // A neutral grey leaning toward the accent: the system's warm red-tinted surfaces.
    private static Color Tint(float grey, Color accent, float amount)
        => Color.Lerp(new Color(grey, grey, grey, 1f), accent, amount);
    // Same hue as the accent with scaled saturation and a fixed value: ol-button, ol-muted.
    private static Color Shade(Color accent, float saturation, float value) {
        Color.RGBToHSV(accent, out float h, out float s, out _);
        return Color.HSVToRGB(h, Mathf.Clamp01(s * saturation), value);
    }
    private static Color WithAlpha(Color color, float alpha) {
        color.a = alpha;
        return color;
    }
}

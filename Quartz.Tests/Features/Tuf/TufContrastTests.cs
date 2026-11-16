using Quartz.Features.Tuf;
using static Asserts;
static class TufContrastTests {
    // TUF's difficulty gradient stops (TufDifficultyRangeBar Quantum and PGU), the dark U range included.
    static readonly (int R, int G, int B)[] Stops = [
        (255, 255, 255), (241, 161, 5), (235, 123, 41), (227, 85, 74), (192, 52, 94), (214, 16, 136), (113, 73, 164),
        (63, 32, 103), (47, 43, 54), (126, 0, 0), (255, 254, 254),
        (0, 153, 255), (0, 255, 136), (242, 167, 0), (225, 79, 79), (210, 0, 151), (45, 29, 65), (0, 0, 0),
        (0x1F, 0x2A, 0x5C),   // the U16 navy of the player's screenshot, about
    ];

    /// <summary>For every stop and every colour of a coarse RGB grid, against the dark and the light backdrop, the text or its
    /// shadow laid over that backdrop stands 3:1 from it; on the dark one the text alone does. The hue is kept.</summary>
    public static void TestEveryColourReadable() {
        List<(float, float, float)> colours = [.. Stops.Select(s => (s.R / 255f, s.G / 255f, s.B / 255f))];
        for(int r = 0; r <= 8; r++) for(int g = 0; g <= 8; g++) for(int b = 0; b <= 8; b++) colours.Add((r / 8f, g / 8f, b / 8f));
        List<string> bad = [];
        foreach((float r, float g, float b) in colours) {
            ((float R, float G, float B) c, bool glow) = TufContrast.Readable(r, g, b);
            if(TufContrast.Contrast(c, TufContrast.Dark) < TufContrast.MinContrast) bad.Add($"dark {r:0.00},{g:0.00},{b:0.00}");
            var light = TufContrast.Light;
            if(TufContrast.Contrast(c, light) < TufContrast.MinContrast && TufContrast.Contrast(c, TufContrast.ShadowOver(glow, light)) < TufContrast.MinContrast)
                bad.Add($"light {r:0.00},{g:0.00},{b:0.00}");
            // The hue is kept: lightening toward white never reorders the channels.
            if((r < g) != (c.R < c.G) && r != g || (g < b) != (c.G < c.B) && g != b) bad.Add($"hue {r:0.00},{g:0.00},{b:0.00}");
        }
        Assert(bad.Count == 0, "unreadable: " + string.Join("; ", bad.Take(20)) + $" ({bad.Count})");
        var (navy, navyGlow) = TufContrast.Readable(0x1F / 255f, 0x2A / 255f, 0x5C / 255f);
        Assert(navyGlow && navy.B > 0x5C / 255f, "a dark navy is lightened and glows");
        var (yellow, yellowGlow) = TufContrast.Readable(1f, 0.85f, 0f);
        Assert(!yellowGlow && yellow == (1f, 0.85f, 0f), "a light yellow keeps its colour and its dark shadow");
    }
}

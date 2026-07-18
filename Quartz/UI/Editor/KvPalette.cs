using UnityEngine;
namespace Quartz.UI.Editor;
/// <summary>
/// DM Note's own design tokens, transcribed from its source rather than approximated: the colours
/// from <c>src/renderer/styles/colors.ts</c> and <c>global.css</c>, the metrics from the Tailwind
/// classes on its ToolBar, Grid and PropertiesPanel.
///
/// Deliberately not <see cref="UIColors"/>. That palette is re-derived from the user's accent by
/// <c>UIColors.ApplyAccent</c>, so every surface in the mod shifts with it; these are fixed, because
/// the ask for this region is that it read as DM Note rather than as the rest of the Quartz menu.
/// The trade is real and one-way: the editor no longer answers the accent picker.
///
/// Hex rather than float triples so each constant can be diffed against the source it came from.
/// Parsed once at static init — <see cref="ColorUtility"/> is Unity-side but allocation-free here.
/// </summary>
internal static class KvPalette {
    private static Color Hex(string hex)
        => ColorUtility.TryParseHtmlString(hex, out Color c) ? c : Color.magenta;
    // colors.ts
    /// <summary>The bar and page behind the editor. colors.primary.</summary>
    internal static readonly Color Primary = Hex("#1A191E");
    /// <summary>Grouping pills and the buttons inside them. colors.button.primary.</summary>
    internal static readonly Color ButtonPrimary = Hex("#000000");
    internal static readonly Color ButtonHover = Hex("#1F1F23");
    internal static readonly Color ButtonActive = Hex("#2A2A30");
    internal static readonly Color TextWhite = Hex("#FFFFFF");
    /// <summary>Icon and label tint on the bar. colors.text.white2.</summary>
    internal static readonly Color TextDim = Hex("#DBDEE8");
    internal static readonly Color Border = Hex("#3A3943");
    internal static readonly Color Surface = Hex("#2A2A30");
    internal static readonly Color SurfaceHover = Hex("#303036");
    internal static readonly Color SurfaceActive = Hex("#393941");
    internal static readonly Color Focus = Hex("#459BF8");
    internal static readonly Color TextDisabled = Hex("#6B6D75");
    internal static readonly Color DangerBg = Hex("#3C1E1E");
    internal static readonly Color DangerHover = Hex("#442222");
    internal static readonly Color DangerText = Hex("#E6DBDB");
    // Panel + tabs, from PropertiesPanel.tsx / PropertyInputs.tsx.
    internal static readonly Color PanelBg = Hex("#1F1F24");
    internal static readonly Color TabTrack = Hex("#26262C");
    internal static readonly Color TabActive = Hex("#3A3943");
    internal static readonly Color TabIdleText = Hex("#9395A1");
    internal static readonly Color InputText = Hex("#D7D8DB");
    /// <summary>The canvas grid line. GridBackground's lineColor, an rgb() literal in the source.</summary>
    internal static readonly Color GridLine = Hex("#19191C");
    /// <summary>
    /// DM Note's CSS pixels are laid out for a ~1000px Electron window; this menu's canvas is
    /// scaled to <c>UICore.ReferenceResolution</c> (1920x1080), so its units are the larger of the
    /// two. Everything below is a DM Note px multiplied by this, which is why each member carries
    /// the source value it was derived from.
    ///
    /// 1.5 rather than a measured ratio because the editor's existing metrics already sit near it
    /// (the 220px panel is 320 here, DM Note's 13px label is 19) and a round factor keeps the
    /// derived values from landing on fractions the layout would then round apart.
    ///
    /// Multiplied by the user's UI Scale setting: the menu canvas grows its virtual area as the
    /// setting shrinks, so a fixed 1.5 read as "the editor ignores my UI Scale". A property, not
    /// a const — it is only read while building editor chrome, and the setting can change
    /// between builds.
    /// </summary>
    internal static float Scale => 1.5f * Mathf.Clamp(Quartz.Core.MainCore.Conf?.UIScale ?? 1f, 0.8f, 1.6f);
    /// <summary>60px bar.</summary>
    internal static float BarHeight => 60f * Scale;
    /// <summary>10px bar padding.</summary>
    internal static float BarPad => 10f * Scale;
    /// <summary>40px grouping pill.</summary>
    internal static float PillHeight => 40f * Scale;
    /// <summary>5px pill padding, and the gap between buttons inside one.</summary>
    internal static float PillPad => 5f * Scale;
    /// <summary>10px gap between pills.</summary>
    internal static float GroupGap => 10f * Scale;
    /// <summary>30x30 icon button.</summary>
    internal static float IconButton => 30f * Scale;
    /// <summary>
    /// The icon itself. 18 is the reference box every sprite in the set was baked against, so
    /// drawing them all at this one size reproduces each at its true DM Note size — the 14px move
    /// glyph at 14, the 11x2 minus at 11x2 — without a per-icon number.
    /// </summary>
    internal static float IconSize => 18f * Scale;
    /// <summary>7px corner, the radius on almost everything DM Note draws.</summary>
    internal static float Radius => 7f * Scale;
    /// <summary>4px corner, used only by the panel's 24px header buttons.</summary>
    internal static float RadiusSmall => 4f * Scale;
    /// <summary>220px properties panel.</summary>
    internal static float PanelWidth => 220f * Scale;
    /// <summary>12px panel padding, and the gap between its rows.</summary>
    internal static float PanelPad => 12f * Scale;
    // NB there is no grid-pitch constant here on purpose. Everything above is chrome, which is why
    // it scales; the grid is drawn in layout space, where a unit is DM Note's dx/dy and maps 1:1
    // onto this canvas (elements are placed straight from el.X/el.W). Its pitch is
    // KvSnap.GridSnap — the same 5 the drag snaps to, read from there so the two cannot drift.
}

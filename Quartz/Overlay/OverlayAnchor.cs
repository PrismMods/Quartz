using Quartz.UI.Generator;
using TMPro;
using UnityEngine;
namespace Quartz.Overlay;
public enum OverlayAnchor {
    TopLeft,
    TopCenter,
    TopRight,
    MiddleLeft,
    MiddleCenter,
    MiddleRight,
    BottomLeft,
    BottomCenter,
    BottomRight,
}
public static class OverlayAnchors {
    public static readonly OverlayAnchor[] All = (OverlayAnchor[])System.Enum.GetValues(typeof(OverlayAnchor));
    public static OverlayAnchor Parse(int value) =>
        value >= 0 && value < All.Length ? (OverlayAnchor)value : OverlayAnchor.TopLeft;
    public static Vector2 Vector(OverlayAnchor anchor) => new(
        (int)anchor % 3 * 0.5f,
        1f - (int)anchor / 3 * 0.5f
    );
    public static bool IsTop(OverlayAnchor anchor) => (int)anchor < 3;
    public static Vector2 DefaultOffset(OverlayAnchor anchor, float margin = 24f) {
        Vector2 a = Vector(anchor);
        return new Vector2(
            a.x == 0f ? margin : a.x == 1f ? -margin : 0f,
            a.y == 0f ? margin : a.y == 1f ? -margin : 0f
        );
    }
    public static void Pin(RectTransform rect, OverlayAnchor anchor) {
        if(rect == null) return;
        Vector2 a = Vector(anchor);
        rect.anchorMin = a;
        rect.anchorMax = a;
        rect.pivot = a;
    }
    public static TextAlignmentOptions TextAlign(OverlayAnchor anchor) => anchor switch {
        OverlayAnchor.TopLeft => TextAlignmentOptions.TopLeft,
        OverlayAnchor.TopCenter => TextAlignmentOptions.Top,
        OverlayAnchor.TopRight => TextAlignmentOptions.TopRight,
        OverlayAnchor.MiddleLeft => TextAlignmentOptions.Left,
        OverlayAnchor.MiddleRight => TextAlignmentOptions.Right,
        OverlayAnchor.BottomLeft => TextAlignmentOptions.BottomLeft,
        OverlayAnchor.BottomCenter => TextAlignmentOptions.Bottom,
        OverlayAnchor.BottomRight => TextAlignmentOptions.BottomRight,
        _ => TextAlignmentOptions.Center,
    };
    public static TextAlignmentOptions HorizontalAlign(OverlayAnchor anchor) => ((int)anchor % 3) switch {
        0 => TextAlignmentOptions.Left,
        2 => TextAlignmentOptions.Right,
        _ => TextAlignmentOptions.Center,
    };
    public static TextAnchor LayoutAlign(OverlayAnchor anchor) => (TextAnchor)(int)anchor;
    public static string Name(OverlayAnchor anchor) => anchor switch {
        OverlayAnchor.TopLeft => GenerateUI.Tr("ANCHOR_TOP_LEFT", "Top Left"),
        OverlayAnchor.TopCenter => GenerateUI.Tr("ANCHOR_TOP_CENTER", "Top Center"),
        OverlayAnchor.TopRight => GenerateUI.Tr("ANCHOR_TOP_RIGHT", "Top Right"),
        OverlayAnchor.MiddleLeft => GenerateUI.Tr("ANCHOR_MIDDLE_LEFT", "Middle Left"),
        OverlayAnchor.MiddleCenter => GenerateUI.Tr("ANCHOR_MIDDLE_CENTER", "Middle Center"),
        OverlayAnchor.MiddleRight => GenerateUI.Tr("ANCHOR_MIDDLE_RIGHT", "Middle Right"),
        OverlayAnchor.BottomLeft => GenerateUI.Tr("ANCHOR_BOTTOM_LEFT", "Bottom Left"),
        OverlayAnchor.BottomCenter => GenerateUI.Tr("ANCHOR_BOTTOM_CENTER", "Bottom Center"),
        OverlayAnchor.BottomRight => GenerateUI.Tr("ANCHOR_BOTTOM_RIGHT", "Bottom Right"),
        _ => anchor.ToString(),
    };
    public static void Dropdown(Transform parent, OverlayAnchor def, OverlayAnchor current, System.Action<OverlayAnchor> onChange, string id) =>
        GenerateUI.DropDown(GenerateUI.Row(parent), def, current, All, Name, onChange, id, 260f, "Anchor");
}

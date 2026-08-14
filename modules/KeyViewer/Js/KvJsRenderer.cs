using Quartz.Core;
using Quartz.Resource;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Quartz.Features.KeyViewer.Js;

internal static class KvJsRenderer {
    private const float DefaultFontSize = 16f;
    private const float DefaultWidth = 220f;
    private const float DefaultHeight = 140f;

    private sealed class Box {
        internal KvJsVNode Node;
        internal KvJsStyle Style;
        internal readonly List<Box> Children = [];
        internal Vector2 Size;
        internal Vector2 At;
        internal float FontSize;
        internal bool Inline;
        internal bool Skip;
    }

    internal readonly struct Result {
        internal readonly GameObject Root;
        internal readonly Vector2 Size;
        internal Result(GameObject root, Vector2 size) { Root = root; Size = size; }
    }

    internal static Result Render(
        RectTransform parent, KvJsVNode node, string name, float x, float y,
        float estimatedWidth = DefaultWidth, float estimatedHeight = DefaultHeight
    ) {
        if(parent == null || node == null) return new Result(null, Vector2.zero);
        float basisW = Mathf.Max(20f, estimatedWidth);
        float basisH = Mathf.Max(20f, estimatedHeight);
        Box layout = Measure(node, basisW, basisH, DefaultFontSize);
        if(layout.Size.x < 1f || layout.Size.y < 1f) layout.Size = new Vector2(basisW, basisH);
        GameObject rootObj = new("JsPlugin_" + SafeName(name));
        rootObj.transform.SetParent(parent, false);
        RectTransform root = rootObj.AddComponent<RectTransform>();
        SetRect(root, x, y, layout.Size.x, layout.Size.y);
        Draw(layout, root, 0f, 0f, Color.white, DefaultFontSize, isRoot: true);
        return new Result(rootObj, layout.Size);
    }

    private static Box Measure(KvJsVNode node, float basisW, float basisH, float inheritedFont) {
        if(node.IsText) {
            float textWidth = Mathf.Max(0f, (node.Text?.Length ?? 0) * inheritedFont * 0.54f);
            return new Box {
                Node = node,
                FontSize = inheritedFont,
                Inline = true,
                Size = new Vector2(textWidth, inheritedFont * 1.3f),
            };
        }
        KvJsStyle style = KvJsStyle.Parse(node.Attr("style"));
        float font = style.FontSize > 0f ? style.FontSize : inheritedFont;
        Box box = new() {
            Node = node,
            Style = style,
            FontSize = font,
            Inline = IsInline(node.Tag),
            Skip = style.Display == KvJsStyle.DispNone || IsSkipped(node.Tag),
        };
        if(box.Skip) return box;
        bool svg = node.Tag.Equals("svg", StringComparison.OrdinalIgnoreCase);
        float explicitW = style.Width.Has ? style.Width.Resolve(basisW) : AttrLength(node, "width", 0f, basisW);
        float explicitH = style.Height.Has ? style.Height.Resolve(basisH) : AttrLength(node, "height", 0f, basisH);
        float innerBasisW = Mathf.Max(1f, (explicitW > 0f ? explicitW : basisW) - style.PadL - style.PadR);
        float innerBasisH = Mathf.Max(1f, (explicitH > 0f ? explicitH : basisH) - style.PadT - style.PadB);
        if(svg) {
            box.Size = new Vector2(
                Mathf.Max(1f, explicitW > 0f ? explicitW : basisW),
                Mathf.Max(1f, explicitH > 0f ? explicitH : basisH));
            return box;
        }
        if(node.Children != null) {
            foreach(KvJsVNode child in node.Children) {
                Box measured = Measure(child, innerBasisW, innerBasisH, font);
                if(!measured.Skip) box.Children.Add(measured);
            }
        }
        bool grid = style.Display == KvJsStyle.DispGrid;
        bool row = style.Display == KvJsStyle.DispFlex ? style.Row : box.Inline || HasInlineRun(box.Children);
        Vector2 content = grid
            ? MeasureGrid(box, Math.Max(1, style.GridColumns))
            : MeasureFlow(box, row);
        float naturalW = content.x + style.PadL + style.PadR;
        float naturalH = content.y + style.PadT + style.PadB;
        float width = explicitW > 0f ? explicitW : naturalW;
        float height = explicitH > 0f ? explicitH : naturalH;
        if(style.MinWidth.Has) width = Mathf.Max(width, style.MinWidth.Resolve(basisW));
        if(style.MinHeight.Has) height = Mathf.Max(height, style.MinHeight.Resolve(basisH));
        box.Size = new Vector2(Mathf.Max(1f, width), Mathf.Max(1f, height));
        LayoutChildren(box, row, grid, Math.Max(1, style.GridColumns));
        return box;
    }

    private static Vector2 MeasureFlow(Box box, bool row) {
        float main = 0f, cross = 0f;
        int normal = 0;
        foreach(Box child in box.Children) {
            if(child.Style is { Absolute: true }) continue;
            float w = child.Size.x + MarginX(child);
            float h = child.Size.y + MarginY(child);
            main += row ? w : h;
            cross = Mathf.Max(cross, row ? h : w);
            normal++;
        }
        float gap = row ? box.Style.ColGap : box.Style.RowGap;
        if(normal > 1) main += gap * (normal - 1);
        return row ? new Vector2(main, cross) : new Vector2(cross, main);
    }

    private static Vector2 MeasureGrid(Box box, int columns) {
        List<Box> normal = box.Children.Where(static child => child.Style is not { Absolute: true }).ToList();
        if(normal.Count == 0) return Vector2.zero;
        int rows = (normal.Count + columns - 1) / columns;
        float[] colW = new float[columns];
        float[] rowH = new float[rows];
        for(int i = 0; i < normal.Count; i++) {
            Box child = normal[i];
            colW[i % columns] = Mathf.Max(colW[i % columns], child.Size.x + MarginX(child));
            rowH[i / columns] = Mathf.Max(rowH[i / columns], child.Size.y + MarginY(child));
        }
        return new Vector2(colW.Sum() + box.Style.ColGap * Mathf.Max(0, columns - 1),
            rowH.Sum() + box.Style.RowGap * Mathf.Max(0, rows - 1));
    }

    private static void LayoutChildren(Box box, bool row, bool grid, int columns) {
        float innerW = Mathf.Max(0f, box.Size.x - box.Style.PadL - box.Style.PadR);
        float innerH = Mathf.Max(0f, box.Size.y - box.Style.PadT - box.Style.PadB);
        if(grid) {
            LayoutGrid(box, columns);
            LayoutAbsolute(box, innerW, innerH);
            return;
        }
        List<Box> normal = box.Children.Where(static child => child.Style is not { Absolute: true }).ToList();
        float used = 0f;
        float grow = 0f;
        foreach(Box child in normal) {
            used += (row ? child.Size.x + MarginX(child) : child.Size.y + MarginY(child));
            grow += Mathf.Max(0f, child.Style?.FlexGrow ?? 0f);
        }
        float gap = row ? box.Style.ColGap : box.Style.RowGap;
        if(normal.Count > 1) used += gap * (normal.Count - 1);
        float available = row ? innerW : innerH;
        float extra = Mathf.Max(0f, available - used);
        float cursor = box.Style.JustifyContent switch {
            KvJsStyle.JustifyCenter => extra * 0.5f,
            KvJsStyle.JustifyEnd => extra,
            _ => 0f,
        };
        float between = box.Style.JustifyContent == KvJsStyle.JustifyBetween && normal.Count > 1
            ? extra / (normal.Count - 1) : 0f;
        foreach(Box child in normal) {
            KvJsStyle childStyle = child.Style ?? KvJsStyle.Empty;
            if(grow > 0f && childStyle.FlexGrow > 0f) {
                float add = extra * childStyle.FlexGrow / grow;
                child.Size = row
                    ? new Vector2(child.Size.x + add, child.Size.y)
                    : new Vector2(child.Size.x, child.Size.y + add);
            }
            float crossAvailable = row ? innerH - MarginY(child) : innerW - MarginX(child);
            int align = childStyle.AlignItems != KvJsStyle.AlignStretch
                ? childStyle.AlignItems : box.Style.AlignItems;
            float crossSize = row ? child.Size.y : child.Size.x;
            if(align == KvJsStyle.AlignStretch) {
                if(row && !childStyle.Height.Has) child.Size.y = Mathf.Max(child.Size.y, crossAvailable);
                if(!row && !childStyle.Width.Has) child.Size.x = Mathf.Max(child.Size.x, crossAvailable);
                crossSize = row ? child.Size.y : child.Size.x;
            }
            float crossExtra = Mathf.Max(0f, crossAvailable - crossSize);
            float cross = align switch {
                KvJsStyle.AlignCenter => crossExtra * 0.5f,
                KvJsStyle.AlignEnd => crossExtra,
                _ => 0f,
            };
            if(row) {
                child.At = new Vector2(box.Style.PadL + cursor + childStyle.MarL,
                    box.Style.PadT + cross + childStyle.MarT);
                cursor += child.Size.x + MarginX(child) + gap + between;
            } else {
                child.At = new Vector2(box.Style.PadL + cross + childStyle.MarL,
                    box.Style.PadT + cursor + childStyle.MarT);
                cursor += child.Size.y + MarginY(child) + gap + between;
            }
        }
        LayoutAbsolute(box, innerW, innerH);
    }

    private static void LayoutGrid(Box box, int columns) {
        List<Box> normal = box.Children.Where(static child => child.Style is not { Absolute: true }).ToList();
        if(normal.Count == 0) return;
        int rows = (normal.Count + columns - 1) / columns;
        float innerW = Mathf.Max(0f, box.Size.x - box.Style.PadL - box.Style.PadR);
        float colWidth = Mathf.Max(0f, (innerW - box.Style.ColGap * Mathf.Max(0, columns - 1)) / columns);
        float[] rowH = new float[rows];
        for(int i = 0; i < normal.Count; i++)
            rowH[i / columns] = Mathf.Max(rowH[i / columns], normal[i].Size.y + MarginY(normal[i]));
        float y = box.Style.PadT;
        for(int row = 0; row < rows; row++) {
            for(int col = 0; col < columns; col++) {
                int index = row * columns + col;
                if(index >= normal.Count) break;
                Box child = normal[index];
                KvJsStyle childStyle = child.Style ?? KvJsStyle.Empty;
                if(!childStyle.Width.Has) child.Size.x = Mathf.Max(child.Size.x, colWidth - MarginX(child));
                child.At = new Vector2(box.Style.PadL + col * (colWidth + box.Style.ColGap) + childStyle.MarL,
                    y + childStyle.MarT);
            }
            y += rowH[row] + box.Style.RowGap;
        }
    }

    private static void LayoutAbsolute(Box box, float innerW, float innerH) {
        foreach(Box child in box.Children) {
            if(child.Style is not { Absolute: true } s) continue;
            float x = s.Left.Has ? s.Left.Resolve(innerW)
                : s.Right.Has ? innerW - s.Right.Resolve(innerW) - child.Size.x : 0f;
            float y = s.Top.Has ? s.Top.Resolve(innerH)
                : s.Bottom.Has ? innerH - s.Bottom.Resolve(innerH) - child.Size.y : 0f;
            child.At = new Vector2(box.Style.PadL + x + s.MarL, box.Style.PadT + y + s.MarT);
        }
    }

    private static void Draw(Box box, RectTransform parent, float x, float y, Color inheritedColor, float inheritedFont, bool isRoot = false) {
        if(box.Skip) return;
        if(box.Node.IsText) {
            TextMeshProUGUI text = KeyViewerOverlay.NewText(parent, "Text", box.Node.Text ?? "", box.FontSize);
            SetRect(text.rectTransform, x, y, box.Size.x, box.Size.y);
            text.color = inheritedColor;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            return;
        }
        KvJsStyle style = box.Style;
        float alpha = Mathf.Clamp01(inheritedColor.a * style.Opacity);
        Color textColor = style.TextColor ?? inheritedColor;
        textColor.a *= style.Opacity;
        bool contents = style.Display == KvJsStyle.DispContents;
        RectTransform rect = parent;
        if(!contents) {
            GameObject obj = new(string.IsNullOrEmpty(box.Node.Tag) ? "Element" : box.Node.Tag);
            obj.transform.SetParent(parent, false);
            rect = obj.AddComponent<RectTransform>();
            SetRect(rect, isRoot ? 0f : x, isRoot ? 0f : y, box.Size.x, box.Size.y);
            DrawBackground(rect, style, alpha);
            if(box.Node.Tag.Equals("svg", StringComparison.OrdinalIgnoreCase)) {
                KvJsSvgGraphic svg = obj.AddComponent<KvJsSvgGraphic>();
                svg.raycastTarget = false;
                KvJsSvg.Build(svg, box.Node, alpha);
                return;
            }
        }
        foreach(Box child in box.Children)
            Draw(child, rect, child.At.x, child.At.y, textColor, box.FontSize);
    }

    private static void DrawBackground(RectTransform rect, KvJsStyle style, float alpha) {
        if(style.Bg is Color bg) {
            Image fill = rect.gameObject.AddComponent<Image>();
            fill.sprite = MainCore.Spr.GetFilled(Mathf.Max(0f, style.Radius));
            fill.type = Image.Type.Sliced;
            bg.a *= alpha;
            fill.color = bg;
            fill.raycastTarget = false;
        }
        if(style.BorderWidth <= 0f || style.BorderColor is not Color borderColor) return;
        GameObject borderObj = new("Border");
        borderObj.transform.SetParent(rect, false);
        RectTransform border = borderObj.AddComponent<RectTransform>();
        border.anchorMin = Vector2.zero;
        border.anchorMax = Vector2.one;
        border.offsetMin = Vector2.zero;
        border.offsetMax = Vector2.zero;
        Image image = borderObj.AddComponent<Image>();
        image.sprite = MainCore.Spr.GetRing(Mathf.Max(0.5f, style.Radius), Mathf.Max(0.1f, style.BorderWidth));
        image.type = Image.Type.Sliced;
        borderColor.a *= alpha;
        image.color = borderColor;
        image.raycastTarget = false;
    }

    private static void SetRect(RectTransform rect, float x, float y, float width, float height) {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(Mathf.Max(1f, width), Mathf.Max(1f, height));
    }

    private static float MarginX(Box box) => (box.Style?.MarL ?? 0f) + (box.Style?.MarR ?? 0f);
    private static float MarginY(Box box) => (box.Style?.MarT ?? 0f) + (box.Style?.MarB ?? 0f);
    private static bool HasInlineRun(List<Box> children) => children.Count > 0 && children.All(static child => child.Inline);
    private static bool IsInline(string tag) => tag?.ToLowerInvariant() is "span" or "strong" or "b" or "i" or "em" or "small" or "label";
    private static bool IsSkipped(string tag) => tag?.ToLowerInvariant() is "style" or "link" or "script" or "defs" or "meta";
    private static string SafeName(string value) {
        if(string.IsNullOrEmpty(value)) return "Panel";
        char[] chars = value.Select(static c => char.IsLetterOrDigit(c) ? c : '_').ToArray();
        return new string(chars);
    }
    private static float AttrLength(KvJsVNode node, string name, float fallback, float basis) {
        string raw = node.Attr(name);
        if(string.IsNullOrWhiteSpace(raw)) return fallback;
        KvJsUnit unit = KvJsStyle.Unit(raw);
        return unit.Has ? unit.Resolve(basis) : fallback;
    }
}

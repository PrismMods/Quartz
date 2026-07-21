using Quartz.Core;
using Quartz.Resource;
using Quartz.UI;
using Quartz.UI.Utility;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Quartz.Compat.Game;
namespace Quartz.Features.KeyViewer;
public static partial class KeyViewerOverlay {
    private static void AddReorganizeHandle(RectTransform rainLayer) {
        rainLayerRef = rainLayer;
        dragObj = BuildReorganizeHandle(root, "Drag", "KEYVIEWER_TITLE", "Key Viewer");
        dragRect = (RectTransform)dragObj.transform;
        if(dragObj.GetComponent<ReorganizeHandle>() is { } handle) handle.Bounds = dragRect;
        RefreshDragBounds();
    }
    internal static void RefreshDragBounds() {
        if(root == null || dragRect == null) return;
        if(!TryContentBounds(out Vector2 min, out Vector2 max)) return;
        Rect rootRect = root.rect;
        dragRect.offsetMin = min - rootRect.min;
        dragRect.offsetMax = max - rootRect.max;
    }
    private static bool TryContentBounds(out Vector2 min, out Vector2 max) {
        min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        bool any = false;
        ExtendContentBounds(root, ref min, ref max, ref any);
        return any && max.x > min.x && max.y > min.y;
    }
    private static readonly Vector3[] boundsCorners = new Vector3[4];
    private static void ExtendContentBounds(Transform parent, ref Vector2 min, ref Vector2 max, ref bool any) {
        for(int i = 0; i < parent.childCount; i++) {
            Transform child = parent.GetChild(i);
            if(!child.gameObject.activeSelf) continue;
            if(rainLayerRef != null && child == rainLayerRef.transform) continue;
            if(dragRect != null && child == dragRect.transform) continue;
            if(child is RectTransform rt && IsRenderedGraphic(rt)) {
                rt.GetWorldCorners(boundsCorners);
                for(int c = 0; c < 4; c++) {
                    Vector2 local = root.InverseTransformPoint(boundsCorners[c]);
                    min = Vector2.Min(min, local);
                    max = Vector2.Max(max, local);
                }
                any = true;
            }
            ExtendContentBounds(child, ref min, ref max, ref any);
        }
    }
    private static bool IsRenderedGraphic(RectTransform rt) {
        if(rt.GetComponent<Graphic>() is not { enabled: true } g) return false;
        if(g is EmptyGraphic) return false;
        if(g.color.a <= 0.01f) return false;
        return g is not TMP_Text { text: null or "" };
    }
    private static GameObject BuildReorganizeHandle(RectTransform target, string name,
        string titleKey, string titleFallback) {
        GameObject drag = new(name);
        drag.transform.SetParent(target, false);
        RectTransform dragRect = drag.AddComponent<RectTransform>();
        dragRect.anchorMin = Vector2.zero;
        dragRect.anchorMax = Vector2.one;
        dragRect.offsetMin = Vector2.zero;
        dragRect.offsetMax = Vector2.zero;
        drag.AddComponent<EmptyGraphic>().raycastTarget = true;
        ReorganizeHandle handle = drag.AddComponent<ReorganizeHandle>();
        handle.Target = target;
        handle.GetName = () => MainCore.Tr.Get(titleKey, titleFallback);
        handle.OnMoved = Save;
        drag.SetActive(false);
        return drag;
    }
    internal static (Image fill, Image border) NewBoxVisual(
        string name, Transform parent, float x, float y, float w, float h,
        float radius = KeyRadius, float borderWidth = BorderWidth
    ) {
        GameObject obj = new(name);
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(w, h);
        Image fill = obj.AddComponent<Image>();
        fill.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P1024);
        fill.type = Image.Type.Sliced;
        fill.pixelsPerUnitMultiplier = 8f / Mathf.Max(0.5f, radius);
        fill.raycastTarget = false;
        GameObject borderObj = new("Border");
        borderObj.transform.SetParent(obj.transform, false);
        RectTransform borderRect = borderObj.AddComponent<RectTransform>();
        borderRect.anchorMin = Vector2.zero;
        borderRect.anchorMax = Vector2.one;
        borderRect.offsetMin = Vector2.zero;
        borderRect.offsetMax = Vector2.zero;
        Image border = borderObj.AddComponent<Image>();
        border.sprite = MainCore.Spr.GetRing(Mathf.Max(0.5f, radius), Mathf.Max(0.1f, borderWidth));
        border.type = Image.Type.Sliced;
        border.raycastTarget = false;
        return (fill, border);
    }
    private static Box NewBox(string name, float x, float y, float w, float h) {
        (Image fill, Image border) = NewBoxVisual(name, root, x, y, w, h);
        return new Box { Border = border, Fill = fill };
    }
    internal static string LabelFor(int style, int slot) {
        if(slot >= KeyViewerSettings.FootSlotBase) {
            int fi = slot - KeyViewerSettings.FootSlotBase;
            string[] footOverrides = Conf.FootLabelsForStyle(Conf.FootStyle);
            if(fi >= 0 && fi < footOverrides.Length && !string.IsNullOrEmpty(footOverrides[fi])) return footOverrides[fi];
            int[] footKeys = Conf.FootKeysForStyle(Conf.FootStyle);
            return fi >= 0 && fi < footKeys.Length ? KeyCodeShortLabel((KeyCode)footKeys[fi]) : "";
        }
        string[] overrides = Conf.LabelsForStyle(style);
        if(slot >= 0 && slot < overrides.Length && !string.IsNullOrEmpty(overrides[slot])) return overrides[slot];
        int[] keys = Conf.KeysForStyle(style);
        return slot >= 0 && slot < keys.Length ? KeyCodeShortLabel((KeyCode)keys[slot]) : "";
    }
    internal static TextMeshProUGUI NewText(Transform parent, string name, string text, float fontSize) {
        GameObject obj = new(name);
        obj.transform.SetParent(parent, false);
        obj.AddComponent<RectTransform>();
        TextMeshProUGUI tmp = obj.AddComponent<TextMeshProUGUI>();
        tmp.font = FontManager.Current;
        tmp.fontSize = fontSize;
        tmp.alignment = TextAlignmentOptions.Center;
        TextCompat.NoWrap(tmp);
        tmp.raycastTarget = false;
        tmp.text = text;
        return tmp;
    }
    internal static string KeyCodeShortLabel(KeyCode kc) {
        switch(kc) {
            case KeyCode.UpArrow: return "↑";
            case KeyCode.DownArrow: return "↓";
            case KeyCode.LeftArrow: return "←";
            case KeyCode.RightArrow: return "→";
        }
        string s = kc.ToString();
        if(s.StartsWith("Alpha")) s = s[5..];
        if(s.StartsWith("Keypad")) {
            string rest = s[6..];
            return "N" + rest switch {
                "Enter" => "↵",
                "Plus" => "+",
                "Minus" => "-",
                "Multiply" => "*",
                "Divide" => "/",
                "Period" => ".",
                "Equals" => "=",
                _ => rest,
            };
        }
        if(s.StartsWith("Left")) s = "L" + s[4..];
        if(s.StartsWith("Right")) s = "R" + s[5..];
        if(s.EndsWith("Shift")) s = s[..^5] + "⇧";
        if(s.EndsWith("Control")) s = s[..^7] + "Ctrl";
        if(s.EndsWith("Windows")) s = s[..^7] + "Win";
        return s switch {
            "PageUp" => "PgUp",
            "PageDown" => "PgDn",
            "Insert" => "Ins",
            "Delete" => "Del",
            "Numlock" => "NmLk",
            "ScrollLock" => "ScLk",
            "Print" or "SysReq" => "PrtSc",
            "Break" => "Brk",
            "Escape" => "Esc",
            "Plus" => "+",
            "Minus" => "-",
            "Multiply" => "*",
            "Divide" => "/",
            "Enter" or "Return" => "↵",
            "Equals" => "=",
            "Period" => ".",
            "Comma" => ",",
            "Tab" => "⇥",
            "Space" => "␣",
            "Backslash" => "\\",
            "Slash" => "/",
            "Semicolon" => ";",
            "Quote" => "'",
            "BackQuote" => "`",
            "CapsLock" => "⇪",
            "Backspace" => "Back",
            "LBracket" or "LeftBracket" => "[",
            "RBracket" or "RightBracket" => "]",
            "None" => "",
            _ => s,
        };
    }
}

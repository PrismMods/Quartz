using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Object = UnityEngine.Object;
namespace Quartz.UI.Utility;
public static class TMPTextShadow {
    private const string RootName = "__QuartzTextShadow";
    public static bool UseMaterialUnderlay;
    public static float UnderlayOffsetScale = 6f;
    public static void Apply(
        TextMeshProUGUI text,
        bool enabled,
        float offsetX,
        float offsetY,
        float softness,
        Color color,
        bool isolateCanvas = false
    ) {
        if(text == null) return;
        ShadowRoot root = GetOrCreateRoot(text);
        if(root == null) return;
        bool on = enabled && text.gameObject.activeSelf && color.a > 0.001f;
        if(UseMaterialUnderlay && !isolateCanvas && softness <= 0.001f) {
            root.Rect.gameObject.SetActive(false);
            ApplyUnderlay(text, root, on, offsetX, offsetY, color);
            return;
        }
        DisableUnderlay(text, root);
        RectTransform shadowRoot = root.Rect;
        if(isolateCanvas && shadowRoot.GetComponent<Canvas>() == null)
            shadowRoot.gameObject.AddComponent<Canvas>().overrideSorting = false;
        // Hide via CanvasGroup alpha rather than SetActive: deactivating the root
        // re-registers up to 9 TMP layers with the canvas on every toggle, which is
        // a per-press cost when only one CSS state has a text shadow.
        if(root.Group == null) {
            root.Group = shadowRoot.gameObject.GetComponent<CanvasGroup>()
                ?? shadowRoot.gameObject.AddComponent<CanvasGroup>();
            root.Group.interactable = false;
            root.Group.blocksRaycasts = false;
        }
        if(!shadowRoot.gameObject.activeSelf) shadowRoot.gameObject.SetActive(true);
        root.Group.alpha = on ? 1f : 0f;
        if(!on) return;
        SyncRootRect(text.rectTransform, shadowRoot);
        KeepRootBehindTarget(text, shadowRoot);
        float soft = Mathf.Clamp(softness, 0f, 50f);
        int layerCount = soft > 0.001f ? 9 : 1;
        EnsureLayerCount(root, layerCount);
        Vector2 baseOffset = new(offsetX, offsetY);
        float spread = soft * 0.25f;
        List<TextMeshProUGUI> layers = root.Layers;
        for(int i = 0; i < layers.Count; i++) {
            TextMeshProUGUI layer = layers[i];
            bool active = i < layerCount;
            layer.gameObject.SetActive(active);
            if(!active) continue;
            Color layerColor = color;
            Vector2 layerOffset = baseOffset;
            if(i > 0) {
                layerOffset += SoftnessOffset(i - 1, spread);
                layerColor.a *= 0.28f;
            }
            SyncLayer(text, layer, layerColor, layerOffset);
        }
    }
    public static void Remove(TextMeshProUGUI text) {
        if(text == null) return;
        ShadowLink link = text.GetComponent<ShadowLink>();
        if(link == null) return;
        if(link.Root != null) Object.Destroy(link.Root.gameObject);
        Object.Destroy(link);
    }
    private static ShadowRoot GetOrCreateRoot(TextMeshProUGUI text) {
        ShadowLink link = text.GetComponent<ShadowLink>();
        if(link != null && link.Root != null && link.Root.Rect != null) return link.Root;
        Transform parent = text.transform.parent;
        if(parent == null) return null;
        ShadowRoot root = null;
        foreach(Transform child in parent) {
            ShadowRoot marker = child.GetComponent<ShadowRoot>();
            if(marker != null && marker.Target == text) {
                root = marker;
                root.Rect = (RectTransform)child;
                RebuildLayerCache(root);
                break;
            }
        }
        if(root == null) {
            GameObject obj = new(RootName);
            obj.transform.SetParent(parent, false);
            RectTransform rect = obj.AddComponent<RectTransform>();
            LayoutElement le = obj.AddComponent<LayoutElement>();
            le.ignoreLayout = true;
            root = obj.AddComponent<ShadowRoot>();
            root.Target = text;
            root.Rect = rect;
            KeepRootBehindTarget(text, rect);
        }
        if(link == null) link = text.gameObject.AddComponent<ShadowLink>();
        link.Root = root;
        return root;
    }
    private static void RebuildLayerCache(ShadowRoot root) {
        root.Layers.Clear();
        for(int i = 0; i < root.Rect.childCount; i++) {
            TextMeshProUGUI tmp = root.Rect.GetChild(i).GetComponent<TextMeshProUGUI>();
            if(tmp != null) root.Layers.Add(tmp);
        }
    }
    private static void EnsureLayerCount(ShadowRoot root, int count) {
        while(root.Layers.Count < count) {
            GameObject obj = new("Layer");
            obj.transform.SetParent(root.Rect, false);
            RectTransform rect = obj.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            TextMeshProUGUI tmp = obj.AddComponent<TextMeshProUGUI>();
            tmp.raycastTarget = false;
            // Layers are hidden via CanvasGroup alpha 0; make sure the fully
            // transparent meshes are culled instead of drawn.
            tmp.canvasRenderer.cullTransparentMesh = true;
            root.Layers.Add(tmp);
        }
    }
    private static void KeepRootBehindTarget(TextMeshProUGUI text, RectTransform root) {
        int rootIndex = root.GetSiblingIndex();
        int textIndex = text.transform.GetSiblingIndex();
        if(rootIndex > textIndex) root.SetSiblingIndex(textIndex);
        else if(rootIndex < textIndex - 1) root.SetSiblingIndex(textIndex - 1);
    }
    private static void SyncRootRect(RectTransform source, RectTransform root) {
        root.anchorMin = source.anchorMin;
        root.anchorMax = source.anchorMax;
        root.pivot = source.pivot;
        root.localScale = source.localScale;
        root.localRotation = source.localRotation;
        root.offsetMin = source.offsetMin;
        root.offsetMax = source.offsetMax;
    }
    private static void SyncLayer(
        TextMeshProUGUI source,
        TextMeshProUGUI layer,
        Color color,
        Vector2 offset
    ) {
        RectTransform rect = layer.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = source.rectTransform.pivot;
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
        if(rect.offsetMin != offset) rect.offsetMin = offset;
        if(rect.offsetMax != offset) rect.offsetMax = offset;
        layer.font = source.font;
        layer.text = StripColorKeepAlpha(source.text);
        layer.fontSize = source.fontSize;
        layer.fontStyle = source.fontStyle;
        layer.alignment = source.alignment;
        layer.color = color;
        layer.lineSpacing = source.lineSpacing;
        layer.characterSpacing = source.characterSpacing;
        layer.wordSpacing = source.wordSpacing;
        layer.paragraphSpacing = source.paragraphSpacing;
        layer.richText = source.richText;
        layer.textWrappingMode = source.textWrappingMode;
        layer.overflowMode = source.overflowMode;
        layer.enableAutoSizing = source.enableAutoSizing;
        layer.fontSizeMin = source.fontSizeMin;
        layer.fontSizeMax = source.fontSizeMax;
        layer.margin = source.margin;
        layer.raycastTarget = false;
    }
    private static readonly Regex ColorTagRegex =
        new(@"</?color[^>]*>", RegexOptions.IgnoreCase);
    private static string StripColorKeepAlpha(string s) =>
        string.IsNullOrEmpty(s) || s.IndexOf("color", System.StringComparison.OrdinalIgnoreCase) < 0
            ? s
            : ColorTagRegex.Replace(s, m => ColorTagToAlpha(m.Value));
    private static string ColorTagToAlpha(string tag) {
        int eq = tag.IndexOf('=');
        if(eq < 0) return "<alpha=#FF>"; 
        string val = tag.Substring(eq + 1, tag.Length - eq - 2).Trim().Trim('"', '\'');
        if(val.Length == 0 || val[0] != '#') return "<alpha=#FF>"; // named colour (red, etc.) → opaque silhouette
        string hex = val.Substring(1);
        string aa = hex.Length switch {
            >= 8 => hex.Substring(6, 2),        // RRGGBBAA
            4 or 5 => $"{hex[3]}{hex[3]}",       // RGBA short form (alpha nibble)
            _ => "FF",                           // RRGGBB / RGB / malformed → opaque
        };
        return $"<alpha=#{aa}>";
    }
    private static Vector2 SoftnessOffset(int index, float spread) {
        if(spread <= 0.001f) return Vector2.zero;
        return index switch {
            0 => new Vector2(spread, 0f),
            1 => new Vector2(-spread, 0f),
            2 => new Vector2(0f, spread),
            3 => new Vector2(0f, -spread),
            4 => new Vector2(spread, spread),
            5 => new Vector2(spread, -spread),
            6 => new Vector2(-spread, spread),
            _ => new Vector2(-spread, -spread),
        };
    }
    // Drive the drop shadow through the font material's GPU underlay instead of a
    // sibling TMP. One mesh/draw per label. Offset is font-relative (approximate).
    private static void ApplyUnderlay(
        TextMeshProUGUI text,
        ShadowRoot root,
        bool on,
        float offsetX,
        float offsetY,
        Color color
    ) {
        Material mat = text.fontMaterial;
        if(mat == null) return;
        if(!on) {
            mat.DisableKeyword("UNDERLAY_ON");
            // Invalidate the cache so a later sibling-mode Apply re-runs its
            // idempotent disable writes against this material.
            root.UnderlayDisabledMat = null;
            return;
        }
        float fs = text.fontSize <= 0f ? 1f : text.fontSize;
        mat.EnableKeyword("UNDERLAY_ON");
        mat.DisableKeyword("UNDERLAY_INNER");
        mat.SetColor("_UnderlayColor", color);
        mat.SetFloat("_UnderlayOffsetX", Mathf.Clamp(offsetX / fs * UnderlayOffsetScale, -1f, 1f));
        mat.SetFloat("_UnderlayOffsetY", Mathf.Clamp(offsetY / fs * UnderlayOffsetScale, -1f, 1f));
        mat.SetFloat("_UnderlaySoftness", 0f);
        mat.SetFloat("_UnderlayDilate", 0f);
        // Underlay is ON now — a switch back to sibling mode must actually re-run
        // DisableUnderlay, so clear its "already disabled" memo.
        root.UnderlayDisabledMat = null;
    }
    private static void DisableUnderlay(TextMeshProUGUI text, ShadowRoot root) {
        Material mat = text.fontMaterial;
        if(mat == null || ReferenceEquals(mat, root.UnderlayDisabledMat)) {
            // Underlay is permanently off and never re-enabled, so the 6 idempotent
            // material writes only need to run once per material instance (the
            // instance changes on a font swap, which re-triggers this).
            return;
        }
        mat.DisableKeyword("UNDERLAY_ON");
        mat.DisableKeyword("UNDERLAY_INNER");
        mat.SetFloat("_UnderlayOffsetX", 0f);
        mat.SetFloat("_UnderlayOffsetY", 0f);
        mat.SetFloat("_UnderlaySoftness", 0f);
        mat.SetFloat("_UnderlayDilate", 0f);
        root.UnderlayDisabledMat = mat;
    }
    private sealed class ShadowRoot : MonoBehaviour {
        public TextMeshProUGUI Target;
        public RectTransform Rect;
        public Material UnderlayDisabledMat;
        public CanvasGroup Group;
        public readonly List<TextMeshProUGUI> Layers = new();
    }
    // Cached pointer from a text to its ShadowRoot, attached to the text's own
    // GameObject so lookup is one GetComponent and the cache can't outlive it.
    private sealed class ShadowLink : MonoBehaviour {
        public ShadowRoot Root;
    }
}

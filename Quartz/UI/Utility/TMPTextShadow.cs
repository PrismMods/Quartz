using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Object = UnityEngine.Object;
using Quartz.Compat.Game;
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
        if(root.Group == null) {
            root.Group = shadowRoot.gameObject.GetComponent<CanvasGroup>()
                ?? shadowRoot.gameObject.AddComponent<CanvasGroup>();
            root.Group.interactable = false;
            root.Group.blocksRaycasts = false;
        }
        if(!shadowRoot.gameObject.activeSelf) shadowRoot.gameObject.SetActive(true);
        float wantAlpha = on ? 1f : 0f;
        if(root.Group.alpha != wantAlpha) root.Group.alpha = wantAlpha;
        if(!on) return;
        SyncRootRect(text.rectTransform, shadowRoot);
        KeepRootBehindTarget(text, shadowRoot);
        float soft = Mathf.Clamp(softness, 0f, 50f);
        int layerCount = soft > 0.001f ? 9 : 1;
        EnsureLayerCount(root, layerCount);
        string srcText = text.text;
        if(!ReferenceEquals(srcText, root.LastSourceText)) {
            root.LastSourceText = srcText;
            root.StrippedText = StripColorKeepAlpha(srcText);
        }
        Vector2 baseOffset = new(offsetX, offsetY);
        float spread = soft * 0.25f;
        List<TextMeshProUGUI> layers = root.Layers;
        bool sourceWrap = TextCompat.GetWrap(text);
        for(int i = 0; i < layers.Count; i++) {
            TextMeshProUGUI layer = layers[i];
            bool active = i < layerCount;
            if(layer.gameObject.activeSelf != active) layer.gameObject.SetActive(active);
            if(!active) continue;
            Color layerColor = color;
            Vector2 layerOffset = baseOffset;
            if(i > 0) {
                layerOffset += SoftnessOffset(i - 1, spread);
                layerColor.a *= 0.28f;
            }
            SyncLayer(text, layer, layerColor, layerOffset, root.StrippedText, sourceWrap);
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
        Vector2 v = source.anchorMin;
        if(root.anchorMin != v) root.anchorMin = v;
        v = source.anchorMax;
        if(root.anchorMax != v) root.anchorMax = v;
        v = source.pivot;
        if(root.pivot != v) root.pivot = v;
        Vector3 scale = source.localScale;
        if(root.localScale != scale) root.localScale = scale;
        Quaternion rot = source.localRotation;
        if(!root.localRotation.Equals(rot)) root.localRotation = rot;
        v = source.offsetMin;
        if(root.offsetMin != v) root.offsetMin = v;
        v = source.offsetMax;
        if(root.offsetMax != v) root.offsetMax = v;
    }
    private static void SyncLayer(
        TextMeshProUGUI source,
        TextMeshProUGUI layer,
        Color color,
        Vector2 offset,
        string strippedText,
        bool sourceWrap
    ) {
        RectTransform rect = layer.rectTransform;
        if(rect.anchorMin != Vector2.zero) rect.anchorMin = Vector2.zero;
        if(rect.anchorMax != Vector2.one) rect.anchorMax = Vector2.one;
        Vector2 pivot = source.rectTransform.pivot;
        if(rect.pivot != pivot) rect.pivot = pivot;
        if(rect.localScale != Vector3.one) rect.localScale = Vector3.one;
        if(!rect.localRotation.Equals(Quaternion.identity)) rect.localRotation = Quaternion.identity;
        if(rect.offsetMin != offset) rect.offsetMin = offset;
        if(rect.offsetMax != offset) rect.offsetMax = offset;
        if(layer.font != source.font) layer.font = source.font;
        if(!ReferenceEquals(layer.text, strippedText)) layer.text = strippedText;
        if(layer.fontSize != source.fontSize) layer.fontSize = source.fontSize;
        if(layer.fontStyle != source.fontStyle) layer.fontStyle = source.fontStyle;
        if(layer.alignment != source.alignment) layer.alignment = source.alignment;
        if(layer.color != color) layer.color = color;
        if(layer.lineSpacing != source.lineSpacing) layer.lineSpacing = source.lineSpacing;
        if(layer.characterSpacing != source.characterSpacing) layer.characterSpacing = source.characterSpacing;
        if(layer.wordSpacing != source.wordSpacing) layer.wordSpacing = source.wordSpacing;
        if(layer.paragraphSpacing != source.paragraphSpacing) layer.paragraphSpacing = source.paragraphSpacing;
        if(layer.richText != source.richText) layer.richText = source.richText;
        if(TextCompat.GetWrap(layer) != sourceWrap) TextCompat.SetWrap(layer, sourceWrap);
        if(layer.overflowMode != source.overflowMode) layer.overflowMode = source.overflowMode;
        if(layer.enableAutoSizing != source.enableAutoSizing) layer.enableAutoSizing = source.enableAutoSizing;
        if(layer.fontSizeMin != source.fontSizeMin) layer.fontSizeMin = source.fontSizeMin;
        if(layer.fontSizeMax != source.fontSizeMax) layer.fontSizeMax = source.fontSizeMax;
        if(layer.margin != source.margin) layer.margin = source.margin;
        if(layer.raycastTarget) layer.raycastTarget = false;
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
        if(val.Length == 0 || val[0] != '#') return "<alpha=#FF>";
        string hex = val.Substring(1);
        string aa = hex.Length switch {
            >= 8 => hex.Substring(6, 2),
            4 or 5 => $"{hex[3]}{hex[3]}",
            _ => "FF",
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
    private const float UnderlayPaddingEpsilon = 0.01f;
    private static Material ResolveMaterial(TextMeshProUGUI text, ShadowRoot root) {
        Material cached = root.MatInstance;
        if(cached != null && ReferenceEquals(text.fontSharedMaterial, cached)) return cached;
        Material mat = text.fontMaterial;
        root.MatInstance = mat;
        return mat;
    }
    private static void ApplyUnderlay(
        TextMeshProUGUI text,
        ShadowRoot root,
        bool on,
        float offsetX,
        float offsetY,
        Color color
    ) {
        Material shared = text.fontSharedMaterial;
        if(shared == null) return;
        if(!on) {
            if(!ReferenceEquals(shared, root.UnderlayAppliedMat)) return;
            Material off = ResolveMaterial(text, root);
            if(off != null) off.DisableKeyword("UNDERLAY_ON");
            root.UnderlayAppliedMat = null;
            root.UnderlayDisabledMat = null;
            root.UnderlayKeywordMat = null;
            return;
        }
        float fs = text.fontSize <= 0f ? 1f : text.fontSize;
        float ux = Mathf.Clamp(offsetX / fs * UnderlayOffsetScale, -1f, 1f);
        float uy = Mathf.Clamp(offsetY / fs * UnderlayOffsetScale, -1f, 1f);
        bool sameMat = ReferenceEquals(shared, root.UnderlayAppliedMat);
        if(sameMat
           && ux == root.UnderlayAppliedX && uy == root.UnderlayAppliedY
           && color == root.UnderlayAppliedColor) return;
        Material mat = ResolveMaterial(text, root);
        if(mat == null) return;
        if(!ReferenceEquals(mat, root.UnderlayKeywordMat)) {
            mat.EnableKeyword("UNDERLAY_ON");
            mat.DisableKeyword("UNDERLAY_INNER");
            root.UnderlayKeywordMat = mat;
        }
        if(!sameMat || color != root.UnderlayAppliedColor) mat.SetColor("_UnderlayColor", color);
        mat.SetFloat("_UnderlayOffsetX", ux);
        mat.SetFloat("_UnderlayOffsetY", uy);
        mat.SetFloat("_UnderlaySoftness", 0f);
        mat.SetFloat("_UnderlayDilate", 0f);
        if(!sameMat
           || Mathf.Abs(ux - root.UnderlayPaddedX) > UnderlayPaddingEpsilon
           || Mathf.Abs(uy - root.UnderlayPaddedY) > UnderlayPaddingEpsilon) {
            text.UpdateMeshPadding();
            root.UnderlayPaddedX = ux;
            root.UnderlayPaddedY = uy;
        }
        root.UnderlayAppliedMat = text.fontSharedMaterial;
        root.UnderlayAppliedX = ux;
        root.UnderlayAppliedY = uy;
        root.UnderlayAppliedColor = color;
        root.UnderlayDisabledMat = null;
    }
    private static void DisableUnderlay(TextMeshProUGUI text, ShadowRoot root) {
        Material shared = text.fontSharedMaterial;
        if(shared == null || ReferenceEquals(shared, root.UnderlayDisabledMat)) return;
        Material mat = ResolveMaterial(text, root);
        if(mat == null) return;
        mat.DisableKeyword("UNDERLAY_ON");
        mat.DisableKeyword("UNDERLAY_INNER");
        mat.SetFloat("_UnderlayOffsetX", 0f);
        mat.SetFloat("_UnderlayOffsetY", 0f);
        mat.SetFloat("_UnderlaySoftness", 0f);
        mat.SetFloat("_UnderlayDilate", 0f);
        root.UnderlayDisabledMat = text.fontSharedMaterial;
        root.UnderlayAppliedMat = null;
        root.UnderlayKeywordMat = null;
    }
    private sealed class ShadowRoot : MonoBehaviour {
        public TextMeshProUGUI Target;
        public RectTransform Rect;
        public Material UnderlayDisabledMat;
        public Material UnderlayAppliedMat;
        public Material UnderlayKeywordMat;
        public Material MatInstance;
        public float UnderlayAppliedX;
        public float UnderlayAppliedY;
        public float UnderlayPaddedX;
        public float UnderlayPaddedY;
        public Color UnderlayAppliedColor;
        public CanvasGroup Group;
        public string LastSourceText;
        public string StrippedText;
        public readonly List<TextMeshProUGUI> Layers = new();
    }
    private sealed class ShadowLink : MonoBehaviour {
        public ShadowRoot Root;
    }
}

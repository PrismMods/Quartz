using Quartz.Compat.Game;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace Quartz.Features.Judgement;
internal static class XPerfectBorder {
    private const float OutlineWidth = 0.405f;
    private const float GradientOpacity = 0.4f;
    private const int GradientHeight = 64;
    private static readonly Color[] GradientStops = [
        new Color32(0xF7, 0x4F, 0x4F, 0xFF),
        new Color32(0xFF, 0x89, 0xC7, 0xFF),
        new Color32(0xB9, 0x94, 0xFF, 0xFF),
        new Color32(0x8B, 0xAD, 0xFF, 0xFF),
        new Color32(0x00, 0x84, 0xFF, 0xFF),
    ];
    private static Texture2D gradient;
    private static Material material;
    internal static TextMeshProUGUI Create(TextMeshProUGUI target) {
        GameObject obj = new("XPerfectBorder");
        obj.transform.SetParent(target.transform.parent, false);
        obj.AddComponent<RectTransform>();
        obj.AddComponent<LayoutElement>().ignoreLayout = true;
        TextMeshProUGUI border = obj.AddComponent<TextMeshProUGUI>();
        border.raycastTarget = false;
        border.richText = true;
        border.color = Color.white;
        border.horizontalMapping = TextureMappingOptions.Character;
        border.verticalMapping = TextureMappingOptions.Character;
        border.canvasRenderer.cullTransparentMesh = true;
        TextCompat.NoWrap(border);
        return border;
    }
    internal static void Sync(TextMeshProUGUI target, TextMeshProUGUI border, string text) {
        if(border.transform.GetSiblingIndex() != target.transform.GetSiblingIndex() + 1)
            border.transform.SetSiblingIndex(target.transform.GetSiblingIndex() + 1);
        RectTransform src = target.rectTransform;
        RectTransform dst = border.rectTransform;
        dst.anchorMin = src.anchorMin;
        dst.anchorMax = src.anchorMax;
        dst.pivot = src.pivot;
        dst.offsetMin = src.offsetMin;
        dst.offsetMax = src.offsetMax;
        if(border.font != target.font) {
            border.font = target.font;
            border.fontSharedMaterial = BuildMaterial(GameApi.FontMaterial(target.font) ?? target.fontSharedMaterial);
        }
        if(border.fontSize != target.fontSize) border.fontSize = target.fontSize;
        if(border.alignment != target.alignment) border.alignment = target.alignment;
        if(!ReferenceEquals(border.text, text)) border.text = text;
    }
    private static Material BuildMaterial(Material source) {
        if(material != null) UnityEngine.Object.Destroy(material);
        material = new Material(source) { name = "Quartz XPerfect Border", hideFlags = HideFlags.HideAndDontSave };
        Shader faceTextured = Shader.Find("TextMeshPro/Distance Field") ?? Shader.Find("TextMeshPro/Distance Field Overlay");
        if(faceTextured != null) material.shader = faceTextured;
        material.SetTexture(ShaderUtilities.ID_OutlineTex, Gradient());
        material.SetColor(ShaderUtilities.ID_FaceColor, Color.white);
        material.SetFloat(ShaderUtilities.ID_OutlineWidth, OutlineWidth);
        material.SetColor(ShaderUtilities.ID_OutlineColor, Color.white);
        material.EnableKeyword(ShaderUtilities.Keyword_Outline);
        ShaderUtilities.UpdateShaderRatios(material);
        return material;
    }
    private static Texture2D Gradient() {
        if(gradient != null) return gradient;
        gradient = new Texture2D(1, GradientHeight, TextureFormat.RGBA32, false) {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.HideAndDontSave,
        };
        Color32[] pixels = new Color32[GradientHeight];
        int last = GradientStops.Length - 1;
        for(int y = 0; y < GradientHeight; y++) {
            float t = (1f - y / (float)(GradientHeight - 1)) * last;
            int i = Mathf.Min((int)t, last - 1);
            Color stop = Color.Lerp(GradientStops[i], GradientStops[i + 1], t - i);
            pixels[y] = Color.Lerp(Color.white, stop, GradientOpacity);
        }
        gradient.SetPixels32(pixels);
        gradient.Apply(false, true);
        return gradient;
    }
}

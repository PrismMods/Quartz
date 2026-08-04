using HarmonyLib;
using Quartz.Compat.Game;
using Quartz.Core;
using Quartz.IO;
using Quartz.Resource;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;
namespace Quartz.Features.OttoIcon;
public static class OttoIcon {
    public static SettingsFile<OttoIconSettings> ConfMgr { get; private set; }
    public static OttoIconSettings Conf => ConfMgr?.Data;
    public static void EnsureConf() => ConfMgr ??= SettingsFile<OttoIconSettings>.Loaded("OttoIcon.json");
    public static void Save() => ConfMgr?.RequestSave();
    private static bool ShouldChange {
        get {
            EnsureConf();
            return MainCore.IsModEnabled && Conf.Enabled;
        }
    }
    private const float Scale = 0.85f;
    private const float IdleDimFactor = 0.343f;
    private static bool IsHighBpm => scnGame.instance != null && scnGame.instance.highestBPM >= 300f;
    private static Color ActiveColor {
        get {
            if(customSprite != null && !Conf.TintImage) return Color.white;
            return Conf.UseHighBpmColor && IsHighBpm ? Conf.GetHighBpmColor() : Conf.GetColor();
        }
    }
    private static Color IdleColor {
        get {
            Color c = ActiveColor;
            return new Color(c.r * IdleDimFactor, c.g * IdleDimFactor, c.b * IdleDimFactor, c.a);
        }
    }
    private static Sprite originalSprite;
    private static Sprite[] originalAutoSprites;
    private static Sprite[] trackedAutoSprites;
    private static SpriteState originalSpriteState;
    private static bool hasOriginalSpriteState;
    private static Button spriteStateButton;
    private static Image spriteStateImage;
    private static bool hasOriginalTransform;
    private static Vector2 originalAnchoredPosition;
    private static Vector3 originalLocalScale;
    private static Color originalColor;
    private static bool originalPreserveAspect;
    private static Image trackedTransformImage;
    private static Sprite resolvedReplacement;
    private static Sprite customSprite;
    private static Texture2D customTexture;
    private static string customPath = "";
    private static bool applyStateValid;
    private static scnEditor cachedEditor;
    private static Image cachedImage;
    private static Sprite cachedReplacement;
    private static bool cachedAutoState;
    private static Color cachedTargetColor;
    private static Vector2 cachedPosition;
    private static Vector3 cachedScale;
    private static bool cachedPreserveAspect;
    private static void InvalidateApplyState() {
        applyStateValid = false;
        cachedEditor = null;
        cachedImage = null;
        cachedReplacement = null;
    }
    public static void Refresh() {
        InvalidateApplyState();
        Apply();
    }
    internal static void Apply() {
        if(!ShouldChange) return;
        scnEditor editor = scnEditor.instance;
        if(editor == null) return;
        Image autoImage = editor.autoImage;
        if(autoImage == null) return;
        Sprite replacement = ResolveReplacement();
        if(replacement == null) return;
        bool autoState;
        try { autoState = RDC.auto; } catch(Exception e) { Diag.Ignore(e); autoState = false; }
        Color targetColor = autoState ? ActiveColor : IdleColor;
        bool preserveAspect = customSprite != null;
        RectTransform rt = autoImage.rectTransform;
        Vector2 targetPosition = new(Conf.OffsetX, Conf.OffsetY);
        Vector3 targetScale = Vector3.one * Scale;
        if(!hasOriginalTransform || trackedTransformImage != autoImage) {
            originalAnchoredPosition = rt.anchoredPosition;
            originalLocalScale = rt.localScale;
            originalColor = autoImage.color;
            originalPreserveAspect = autoImage.preserveAspect;
            trackedTransformImage = autoImage;
            hasOriginalTransform = true;
        }
        if(ApplyStateMatches(
            editor, autoImage, replacement, autoState, targetColor, targetPosition, targetScale, preserveAspect
        )) return;
        if(autoImage.sprite != replacement) originalSprite = autoImage.sprite;
        OverrideAutoSpriteArray(editor, replacement);
        if(autoImage.sprite != replacement) autoImage.sprite = replacement;
        OverrideAutoButtonSpriteState(autoImage, replacement);
        if(autoImage.color != targetColor) autoImage.color = targetColor;
        if(autoImage.preserveAspect != preserveAspect) autoImage.preserveAspect = preserveAspect;
        if(rt.anchoredPosition != targetPosition) rt.anchoredPosition = targetPosition;
        if(rt.localScale != targetScale) rt.localScale = targetScale;
        applyStateValid = true;
        cachedEditor = editor;
        cachedImage = autoImage;
        cachedReplacement = replacement;
        cachedAutoState = autoState;
        cachedTargetColor = targetColor;
        cachedPosition = targetPosition;
        cachedScale = targetScale;
        cachedPreserveAspect = preserveAspect;
    }
    private static Sprite ResolveReplacement() {
        EnsureCustomImage();
        if(customSprite != null) return customSprite;
        resolvedReplacement ??= MainCore.Spr.Get(Asset.OttoAuto);
        return resolvedReplacement;
    }
    private static void EnsureCustomImage() {
        string path = Conf.ImagePath ?? "";
        if(string.Equals(path, customPath, StringComparison.Ordinal)) return;
        DisposeCustomImage();
        customPath = path;
        if(path.Length > 0) LoadCustomImage(path);
    }
    private static void LoadCustomImage(string path) {
        try {
            if(!File.Exists(path)) return;
            Texture2D texture = new(2, 2, TextureFormat.RGBA32, false) {
                name = "QuartzOttoIcon",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            if(!texture.LoadImage(File.ReadAllBytes(path))) {
                Object.Destroy(texture);
                return;
            }
            customTexture = texture;
            customSprite = SpriteManager.Create(texture);
        } catch(Exception e) {
            MainCore.Log.Wrn($"[OttoIcon] image load failed ({path}): {e.Message}");
        }
    }
    internal static void DisposeCustomImage() {
        if(customSprite != null) Object.Destroy(customSprite);
        if(customTexture != null) Object.Destroy(customTexture);
        customSprite = null;
        customTexture = null;
        customPath = "";
        InvalidateApplyState();
    }
    public static bool HasCustomImage {
        get {
            EnsureConf();
            return !string.IsNullOrWhiteSpace(Conf.ImagePath);
        }
    }
    public static bool ImportImage(out string error) {
        error = null;
        EnsureConf();
        string picked;
        try {
            picked = FileDialog.PickFile("", "Image", ["png", "jpg", "jpeg"], "Select Otto image");
        } catch(Exception e) {
            error = "Picker failed: " + e.Message;
            MainCore.Log.Err($"[OttoIcon] PickFile failed: {e}");
            return false;
        }
        if(string.IsNullOrEmpty(picked)) return false;
        Conf.ImagePath = picked;
        EnsureCustomImage();
        Refresh();
        Save();
        if(customSprite != null) return true;
        error = "Could not read that image";
        MainCore.Log.Wrn($"[OttoIcon] could not read {picked}");
        return false;
    }
    public static void ClearImage() {
        EnsureConf();
        Conf.ImagePath = "";
        EnsureCustomImage();
        Refresh();
        Save();
    }
    private static bool ApplyStateMatches(
        scnEditor editor, Image autoImage, Sprite replacement,
        bool autoState, Color targetColor, Vector2 targetPosition, Vector3 targetScale, bool preserveAspect
    ) {
        if(!applyStateValid) return false;
        return cachedEditor == editor
            && cachedImage == autoImage
            && cachedReplacement == replacement
            && cachedAutoState == autoState
            && cachedTargetColor == targetColor
            && cachedPosition == targetPosition
            && cachedScale == targetScale
            && cachedPreserveAspect == preserveAspect
            && autoImage != null
            && autoImage.sprite == replacement
            && autoImage.color == targetColor
            && autoImage.preserveAspect == preserveAspect
            && autoImage.rectTransform.anchoredPosition == targetPosition
            && autoImage.rectTransform.localScale == targetScale;
    }
    private static void OverrideAutoSpriteArray(scnEditor editor, Sprite replacement) {
        if(editor == null || editor.autoSprites == null || replacement == null) return;
        if(trackedAutoSprites != editor.autoSprites ||
            originalAutoSprites == null ||
            originalAutoSprites.Length != editor.autoSprites.Length) {
            trackedAutoSprites = editor.autoSprites;
            originalAutoSprites = (Sprite[])editor.autoSprites.Clone();
        }
        for(int i = 0; i < editor.autoSprites.Length; i++)
            if(editor.autoSprites[i] != replacement) editor.autoSprites[i] = replacement;
    }
    private static void OverrideAutoButtonSpriteState(Image autoImage, Sprite replacement) {
        if(autoImage == null || replacement == null) return;
        Button btn;
        if(spriteStateImage == autoImage && spriteStateButton != null) {
            btn = spriteStateButton;
        } else {
            btn = autoImage.GetComponent<Button>();
            if(btn == null) btn = autoImage.GetComponentInParent<Button>();
            if(btn != null) spriteStateImage = autoImage;
        }
        if(btn == null) return;
        if(!hasOriginalSpriteState || spriteStateButton != btn) {
            originalSpriteState = btn.spriteState;
            hasOriginalSpriteState = true;
            spriteStateButton = btn;
        }
        SpriteState state = btn.spriteState;
        if(state.highlightedSprite == replacement &&
            state.pressedSprite == replacement &&
            state.selectedSprite == replacement &&
            state.disabledSprite == replacement) return;
        state.highlightedSprite = replacement;
        state.pressedSprite = replacement;
        state.selectedSprite = replacement;
        state.disabledSprite = replacement;
        btn.spriteState = state;
    }
    public static void Restore() {
        InvalidateApplyState();
        try {
            scnEditor editor = scnEditor.instance;
            if(editor == null || editor.autoImage == null) return;
            if(originalSprite != null) editor.autoImage.sprite = originalSprite;
            if(originalAutoSprites != null &&
                editor.autoSprites != null &&
                trackedAutoSprites == editor.autoSprites &&
                editor.autoSprites.Length == originalAutoSprites.Length) {
                for(int i = 0; i < editor.autoSprites.Length; i++)
                    editor.autoSprites[i] = originalAutoSprites[i];
            }
            Button btn = editor.autoImage.GetComponent<Button>();
            if(btn == null) btn = editor.autoImage.GetComponentInParent<Button>();
            if(btn != null && hasOriginalSpriteState) btn.spriteState = originalSpriteState;
            if(hasOriginalTransform && trackedTransformImage == editor.autoImage) {
                RectTransform rt = editor.autoImage.rectTransform;
                if(rt != null) {
                    rt.anchoredPosition = originalAnchoredPosition;
                    rt.localScale = originalLocalScale;
                }
                editor.autoImage.color = originalColor;
                editor.autoImage.preserveAspect = originalPreserveAspect;
            }
            hasOriginalSpriteState = false;
            spriteStateButton = null;
            spriteStateImage = null;
            hasOriginalTransform = false;
            trackedTransformImage = null;
        } catch(Exception e) { Diag.Ignore(e); }
    }
    [HarmonyPatch(typeof(scnEditor), "OttoUpdate")]
    private static class OttoUpdatePatch {
        private static void Postfix() => Apply();
    }
    [HarmonyPatch(typeof(scnEditor), "Update")]
    private static class EditorUpdatePatch {
        private static void Postfix() => Apply();
    }
    [HarmonyPatch(typeof(scnEditor), "OttoBlink")]
    private static class OttoBlinkPatch {
        private static void Postfix() => Apply();
    }
    [HarmonyPatch(typeof(scrController), "StartLoadingScene")]
    private static class ClearOnSceneChangePatch {
        private static void Postfix() => InvalidateApplyState();
    }
}

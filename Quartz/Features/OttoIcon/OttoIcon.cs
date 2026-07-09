using HarmonyLib;
using Quartz.Core;
using Quartz.IO;
using Quartz.Resource;
using UnityEngine;
using UnityEngine.UI;
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
    private static Color ActiveColor =>
        Conf.UseHighBpmColor && IsHighBpm ? Conf.GetHighBpmColor() : Conf.GetColor();
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
    private static Image trackedTransformImage;
    private static Sprite resolvedReplacement;
    private static bool applyStateValid;
    private static scnEditor cachedEditor;
    private static Image cachedImage;
    private static Sprite cachedReplacement;
    private static bool cachedAutoState;
    private static Color cachedTargetColor;
    private static Vector2 cachedPosition;
    private static Vector3 cachedScale;
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
        Sprite replacement = resolvedReplacement;
        if(replacement == null) {
            replacement = MainCore.Spr.Get(Asset.OttoAuto);
            if(replacement == null) return;
            resolvedReplacement = replacement;
        }
        bool autoState;
        try { autoState = RDC.auto; } catch { autoState = false; }
        Color targetColor = autoState ? ActiveColor : IdleColor;
        RectTransform rt = autoImage.rectTransform;
        Vector2 targetPosition = new(Conf.OffsetX, Conf.OffsetY);
        Vector3 targetScale = Vector3.one * Scale;
        if(!hasOriginalTransform || trackedTransformImage != autoImage) {
            originalAnchoredPosition = rt.anchoredPosition;
            originalLocalScale = rt.localScale;
            originalColor = autoImage.color;
            trackedTransformImage = autoImage;
            hasOriginalTransform = true;
        }
        if(ApplyStateMatches(editor, autoImage, replacement, autoState, targetColor, targetPosition, targetScale)) return;
        if(autoImage.sprite != replacement) originalSprite = autoImage.sprite;
        OverrideAutoSpriteArray(editor, replacement);
        if(autoImage.sprite != replacement) autoImage.sprite = replacement;
        OverrideAutoButtonSpriteState(autoImage, replacement);
        if(autoImage.color != targetColor) autoImage.color = targetColor;
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
    }
    private static bool ApplyStateMatches(
        scnEditor editor, Image autoImage, Sprite replacement,
        bool autoState, Color targetColor, Vector2 targetPosition, Vector3 targetScale
    ) {
        if(!applyStateValid) return false;
        return cachedEditor == editor
            && cachedImage == autoImage
            && cachedReplacement == replacement
            && cachedAutoState == autoState
            && cachedTargetColor == targetColor
            && cachedPosition == targetPosition
            && cachedScale == targetScale
            && autoImage != null
            && autoImage.sprite == replacement
            && autoImage.color == targetColor
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
            }
            hasOriginalSpriteState = false;
            spriteStateButton = null;
            spriteStateImage = null;
            hasOriginalTransform = false;
            trackedTransformImage = null;
        } catch {
        }
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

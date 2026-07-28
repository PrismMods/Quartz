using System.Collections.Generic;
using ADOFAI;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using Quartz.Compat.Game;
using Quartz.Core;
namespace Quartz.Features.Editor;
public static partial class EditorFeature {
    internal static bool ShouldPreviewDecorations => Enabled && Conf.DecoPreview;
    private const string DecorationImageKey = "decorationImage";
    private static bool decoDefaultsCaptured;
    private static Vector2 decoDefaultSize;
    private static Sprite decoDefaultTagSprite;
    private static readonly HashSet<ListItem_Decoration> decoPreviewed = new();
    private static readonly Dictionary<ListItem_Decoration, Color> decoVanillaColor = new();
    private static void CaptureDecoDefaults(ListItem_Decoration item) {
        if(decoDefaultsCaptured || decoPreviewed.Contains(item)) return;
        if(item.itemTypeImage == null || item.tagImage == null) return;
        decoDefaultSize = item.itemTypeImage.rectTransform.sizeDelta;
        decoDefaultTagSprite = item.tagImage.sprite;
        decoDefaultsCaptured = true;
    }
    private static void ResetDecoGeometry(ListItem_Decoration item) {
        if(!decoDefaultsCaptured) return;
        if(item.itemTypeImage != null) item.itemTypeImage.rectTransform.sizeDelta = decoDefaultSize;
        if(item.tagImage != null) {
            item.tagImage.rectTransform.sizeDelta = decoDefaultSize;
            item.tagImage.sprite = decoDefaultTagSprite;
        }
    }
    private static void RestoreDecoItem(ListItem_Decoration item) {
        if(item == null) return;
        if(decoVanillaColor.TryGetValue(item, out Color vanilla)) {
            if(item.itemTypeImage != null) item.itemTypeImage.color = vanilla;
            if(item.tagImage != null) item.tagImage.color = vanilla;
            decoVanillaColor.Remove(item);
        }
        ResetDecoGeometry(item);
    }
    private static Sprite DecorationSprite(LevelEvent ev) {
        scrDecoration deco = scrDecorationManager.GetDecoration(ev);
        scrVisualDecoration visual = deco as scrVisualDecoration;
        if(visual == null || visual.spriteRenderer == null) return null;
        return visual.spriteRenderer.sprite;
    }
    private static Sprite ParticleSprite(LevelEvent ev) {
        scrDecorationManager mgr = scrDecorationManager.instance;
        TextureManager holder = mgr != null ? mgr.imageHolder : null;
        Dictionary<string, TextureManager.CustomSprite> sprites = holder != null ? holder.customSprites : null;
        string image = GameApi.EventGet<string>(ev, DecorationImageKey);
        if(sprites == null || string.IsNullOrEmpty(image)) return null;
        return sprites.TryGetValue(image, out TextureManager.CustomSprite custom) && custom != null ? custom.sprite : null;
    }
    private static bool ApplyDecoPreview(ListItem_Decoration item, LevelEvent ev) {
        LevelEventType type = ev.eventType;
        if(type == LevelEventType.AddText) {
            item.itemTypeImage.sprite = item.textSprite;
            return false;
        }
        if(type == LevelEventType.AddObject) {
            item.itemTypeImage.sprite = item.objectSprite;
            return false;
        }
        if(type == LevelEventType.AddDecoration) {
            Sprite deco = DecorationSprite(ev);
            item.itemTypeImage.sprite = deco;
            item.tagImage.sprite = deco;
        } else if(type == LevelEventType.AddParticle) {
            Sprite custom = ParticleSprite(ev);
            item.itemTypeImage.sprite = custom != null ? custom : item.particleSprite;
            if(custom != null) item.tagImage.sprite = custom;
        }
        return true;
    }
    private static void FitToDefault(Image image) {
        if(image == null) return;
        Sprite sprite = image.sprite;
        if(sprite == null) return;
        Vector3 size = sprite.bounds.size;
        if(size.x <= 0f || size.y <= 0f) return;
        image.rectTransform.sizeDelta = size.x > size.y
            ? new Vector2(decoDefaultSize.x, size.y / size.x * decoDefaultSize.x)
            : new Vector2(size.x / size.y * decoDefaultSize.y, decoDefaultSize.y);
    }
    private static void PreviewDecoItem(ListItem_Decoration item, LevelEvent ev) {
        if(item == null) return;
        if(!ShouldPreviewDecorations) {
            if(decoPreviewed.Remove(item)) RestoreDecoItem(item);
            return;
        }
        CaptureDecoDefaults(item);
        if(!decoDefaultsCaptured) return;
        ResetDecoGeometry(item);
        decoPreviewed.Add(item);
        if(ev == null) return;
        if(!ApplyDecoPreview(item, ev)) return;
        if(item.itemTypeImage == null || item.itemTypeImage.sprite == null) return;
        FitToDefault(item.itemTypeImage);
        FitToDefault(item.tagImage);
    }
    public static void RefreshDecoPreview() {
        try {
            if(!ShouldPreviewDecorations) {
                RestoreDecoPreview();
                return;
            }
            foreach(ListItem_Decoration item in UnityEngine.Object.FindObjectsByType<ListItem_Decoration>(FindObjectsSortMode.None))
                PreviewDecoItem(item, item != null ? item.sourceLevelEvent : null);
        } catch(Exception e) { Diag.Ignore(e); }
    }
    private static void RestoreDecoPreview() {
        if(decoPreviewed.Count == 0 && decoVanillaColor.Count == 0) return;
        List<ListItem_Decoration> items = new(decoPreviewed);
        foreach(ListItem_Decoration item in decoVanillaColor.Keys)
            if(!decoPreviewed.Contains(item)) items.Add(item);
        foreach(ListItem_Decoration item in items) {
            try {
                RestoreDecoItem(item);
            } catch(Exception e) { Diag.Ignore(e); }
        }
        decoPreviewed.Clear();
        decoVanillaColor.Clear();
    }
    [HarmonyPatch(typeof(ListItem_Decoration), "SetEvent")]
    private static class DecoPreviewSetEventPatch {
        private static void Postfix(ListItem_Decoration __instance, LevelEvent ev) {
            try {
                PreviewDecoItem(__instance, ev);
            } catch(Exception e) { Diag.Ignore(e); }
        }
    }
    [HarmonyPatch(typeof(ListItem_Decoration), "ShowSelectionBackground")]
    private static class DecoPreviewSelectionPatch {
        private static void Postfix(ListItem_Decoration __instance) {
            try {
                if(__instance == null) return;
                if(!ShouldPreviewDecorations) {
                    if(decoVanillaColor.ContainsKey(__instance)) decoVanillaColor.Remove(__instance);
                    return;
                }
                if(__instance.itemTypeImage == null) return;
                decoVanillaColor[__instance] = __instance.itemTypeImage.color;
                __instance.itemTypeImage.color = Color.white;
                if(__instance.tagImage != null) __instance.tagImage.color = Color.white;
            } catch(Exception e) { Diag.Ignore(e); }
        }
    }
}

using HarmonyLib;
using Quartz.Core;
using Quartz.Resource;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using Quartz.Compat.Game;
namespace Quartz.Features.InGameOverlay;
public static class InGameOverlayFont {
    public enum Category { Judgement = 2 }
    private sealed class Capture {
        public Category Cat;
        public TMP_Text Tmp;
        public TMP_FontAsset Original;
        public float OriginalSize;
        public object OriginalWrap;
    }
    private static readonly Dictionary<int, Capture> tmpCaptures = [];
    private static bool hooked;
    private static bool JudgementActive => MainCore.IsModEnabled && MainCore.Conf.FontJudgement && FontManager.Current != null;
    public static void Initialize() {
        if(hooked) return;
        hooked = true;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }
    public static void Unhook() {
        if(hooked) {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            hooked = false;
        }
    }
    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) {
        Async.MainThread.Enqueue(Refresh);
    }
    public static void Refresh() {
        PruneDeadCaptures();
        RefreshJudgement();
    }
    private static void PruneDeadCaptures() {
        List<int> dead = null;
        foreach(var kv in tmpCaptures)
            if(kv.Value.Tmp == null) (dead ??= []).Add(kv.Key);
        if(dead != null) foreach(int id in dead) tmpCaptures.Remove(id);
    }
    private static void RefreshJudgement() {
        if(!JudgementActive) RestoreCategory(Category.Judgement);
    }
    private static void RestoreCategory(Category cat) {
        List<int> dead = null;
        foreach(var kv in tmpCaptures) {
            if(kv.Value.Cat != cat) continue;
            if(kv.Value.Tmp != null) {
                kv.Value.Tmp.font = kv.Value.Original;
                TextCompat.RestoreWrap(kv.Value.Tmp, kv.Value.OriginalWrap);
                kv.Value.Tmp.fontSize = kv.Value.OriginalSize;
            }
            (dead ??= []).Add(kv.Key);
        }
        if(dead != null) foreach(int id in dead) tmpCaptures.Remove(id);
    }
    public static void RestoreAll() {
        foreach(Capture cap in tmpCaptures.Values) {
            if(cap.Tmp == null) continue;
            cap.Tmp.font = cap.Original;
            TextCompat.RestoreWrap(cap.Tmp, cap.OriginalWrap);
            cap.Tmp.fontSize = cap.OriginalSize;
        }
        tmpCaptures.Clear();
    }
    private static void OverrideTmp(TMP_Text tmp, Category cat) {
        TMP_FontAsset want = FontManager.Current;
        if(tmp == null || want == null) return;
        int id = tmp.GetInstanceID();
        if(!tmpCaptures.ContainsKey(id)) {
            float gameSize = tmp.fontSize;
            if(gameSize <= 0f) return;
            tmpCaptures[id] = new Capture {
                Cat = cat,
                Tmp = tmp,
                Original = tmp.font,
                OriginalSize = gameSize,
                OriginalWrap = TextCompat.CaptureWrap(tmp),
            };
            tmp.font = want;
            tmp.fontSharedMaterial = GameApi.FontMaterial(want);
            ApplySize(tmp, gameSize, cat);
        } else if(tmp.font != want) {
            tmp.font = want;
            tmp.fontSharedMaterial = GameApi.FontMaterial(want);
        }
    }
    private static void ApplySize(TMP_Text tmp, float gameSize, Category cat) {
        TextCompat.NoWrap(tmp);
        float boxW = tmp.rectTransform.rect.width;
        float wantW = tmp.GetPreferredValues(tmp.text).x;
        float fit = (boxW > 0f && wantW > boxW) ? gameSize * (boxW / wantW) * 0.98f : gameSize;
        tmp.fontSize = fit * SizeMultiplier(cat);
    }
    public static void RefreshSizeOnly(Category cat) {
        foreach(Capture cap in tmpCaptures.Values) {
            if(cap.Cat == cat && cap.Tmp != null) ApplySize(cap.Tmp, cap.OriginalSize, cat);
        }
    }
    internal static float SizeMultiplier(Category cat) => cat switch {
        Category.Judgement => MainCore.Conf.FontJudgementSize,
        _ => 1f,
    };
    [HarmonyPatch(typeof(scrHitTextMesh), "Show")]
    private static class JudgementFontPatch {
        private static void Postfix(scrHitTextMesh __instance) {
            if(!JudgementActive) return;
            try {
                TMP_Text label = GameApi.HitTextLabel(__instance);
                if(label != null) OverrideTmp(label, Category.Judgement);
            } catch(Exception e) {
                MainCore.Log.Wrn($"[InGameOverlayFont] JudgementFontPatch: {e}");
            }
        }
    }
}

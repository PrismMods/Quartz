using HarmonyLib;
using Quartz.Core;
using Quartz.Features.Interop;
using Quartz.IO;
using UnityEngine;
namespace Quartz.Features.Judgement;
public static class JudgementPopupHider {
    public static SettingsFile<JudgementPopupHiderSettings> ConfMgr { get; private set; }
    public static JudgementPopupHiderSettings Conf => ConfMgr?.Data;
    public const int JudgementCount = 12;
    public const int XPerfectPerfectBit = JudgementCount;      
    public const int PlusPerfectBit = JudgementCount + 1;      
    public const int MinusPerfectBit = JudgementCount + 2;     
    private static readonly Vector3 HiddenPosition = new(123456f, 123456f, 123456f);
    public static void EnsureConf() => ConfMgr ??= SettingsFile<JudgementPopupHiderSettings>.Loaded("JudgementPopupHider.json");
    public static void Save() => ConfMgr?.RequestSave();
    private static bool Enabled {
        get {
            EnsureConf();
            return MainCore.IsModEnabled && Conf.Enabled;
        }
    }
    private static bool ShouldHide(scrHitTextMesh hitText) {
        if(!Enabled || hitText == null) return false;
        if(hitText.hitMargin == HitMargin.Perfect && XPerfectBridge.Active) {
            int xbit = XPerfectBridge.LastJudgeForText() switch {
                XPerfectBridge.Judge.X => XPerfectPerfectBit,
                XPerfectBridge.Judge.Plus => PlusPerfectBit,
                XPerfectBridge.Judge.Minus => MinusPerfectBit,
                _ => -1,
            };
            if(xbit >= 0) return (Conf.HiddenMask & (1 << xbit)) != 0;
        }
        int bit = (int)hitText.hitMargin;
        return bit >= 0 && bit < JudgementCount && (Conf.HiddenMask & (1 << bit)) != 0;
    }
    [HarmonyPatch(typeof(scrHitTextMesh), "Show")]
    private static class HitTextShowPatch {
        private static void Prefix(scrHitTextMesh __instance, ref Vector3 position, ref Vector3 borderOffset, ref float scale) {
            if(!ShouldHide(__instance)) return;
            position = HiddenPosition;
            borderOffset = Vector3.zero;
            scale = 0f;
        }
        private static void Postfix(scrHitTextMesh __instance) {
            if(!ShouldHide(__instance)) return;
            __instance.dead = true;
            if(__instance.text != null) __instance.text.text = "";
            __instance.transform.localPosition = HiddenPosition;
            __instance.transform.localScale = Vector3.zero;
            __instance.gameObject.SetActive(false);
        }
    }
}

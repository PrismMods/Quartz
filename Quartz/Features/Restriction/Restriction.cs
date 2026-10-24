using HarmonyLib;
using Quartz.Core;
using Quartz.Features.Interop;
using Quartz.Game.Stats;
using Quartz.IO;
using System.Reflection;
using Quartz.Compat.Game;
namespace Quartz.Features.Restriction;
public static class Restriction {
    public static SettingsFile<RestrictionSettings> ConfMgr { get; private set; }
    public static RestrictionSettings Conf => ConfMgr?.Data;
    public static void EnsureConf() => ConfMgr ??= SettingsFile<RestrictionSettings>.Loaded("Restriction.json");
    public static void Save() => ConfMgr?.RequestSave();
    private static bool failTriggered;
    private static void ResetCounters() => failTriggered = false;
    private static void TriggerFail(string reason) {
        try {
            scrController c = scrController.instance;
            if(c == null || failTriggered) return;
            failTriggered = GameApi.FailByHitbox(c, reason);
        } catch(Exception e) { Diag.Ignore(e); }
    }
    public static string JudgementName(HitMargin hit) {
        string key = HitKinds.Of(hit) switch {
            HitKind.TooEarly => "JR_ALLOW_TOOEARLY",
            HitKind.VeryEarly => "JR_ALLOW_VERYEARLY",
            HitKind.EarlyPerfect => "JR_ALLOW_EARLYPERFECT",
            HitKind.Perfect => "JR_ALLOW_PERFECT",
            HitKind.LatePerfect => "JR_ALLOW_LATEPERFECT",
            HitKind.VeryLate => "JR_ALLOW_VERYLATE",
            HitKind.TooLate => "JR_ALLOW_TOOLATE",
            HitKind.Multipress => "JR_ALLOW_MULTIPRESS",
            HitKind.FailMiss => "JR_ALLOW_MISS",
            HitKind.FailOverload => "JR_ALLOW_OVERLOAD_FAIL",
            HitKind.OverPress => "JR_ALLOW_OVERLOAD_FAIL",
            _ => null,
        };
        string fallback = hit.ToString();
        return key == null ? fallback : MainCore.Tr.Get(key, fallback);
    }
    private static string FormatJrMessage(string msg, HitMargin hit) {
        if(string.IsNullOrEmpty(msg)) return msg;
        string name = JudgementName(hit);
        return msg.Replace("{judgement}", name).Replace("{judgment}", name);
    }
    private static bool InRestrictedSection() {
        RestrictionSettings conf = Conf;
        if(conf == null || !conf.JRestrictSectionsEnabled) return true;
        List<JudgementSection> sections = conf.JRestrictSections;
        if(sections == null || sections.Count == 0) return false;
        float percent;
        try {
            percent = GameStats.Progress * 100f;
        } catch(Exception e) {
            Diag.Ignore(e);
            return false;
        }
        if(float.IsNaN(percent) || float.IsInfinity(percent)) return false;
        foreach(JudgementSection section in sections)
            if(section.Contains(percent)) return true;
        return false;
    }
    private static bool ShouldFailFor(HitMargin margin) {
        HitKind kind = HitKinds.Of(margin);
        int marginInt = (int)kind;
        switch(Conf.JRestrictMode) {
            case 1:
                return kind != HitKind.Perfect;
            case 2: {
                if(kind != HitKind.Perfect) return true;
                if(!XPerfectBridge.Active) return false;
                XPerfectBridge.Judge xj = XPerfectBridge.JudgeFor(margin);
                return xj != XPerfectBridge.Judge.None && xj != XPerfectBridge.Judge.X;
            }
            case 3: {
                int mask = Conf.JRestrictAllowedMask;
                if(mask == 0) return false;
                int bit = 1 << marginInt;
                return (mask & bit) == 0;
            }
            case 4:
                return kind == HitKind.TooEarly;
            case 0:
            default: {
                try {
                    scrMistakesManager m = MistakesAccess.Get();
                    if(m == null) return false;
                    float acc = MistakesAccess.PercentAcc(m);
                    if(float.IsNaN(acc) || float.IsInfinity(acc)) return false;
                    return acc * 100f < Conf.JRestrictAccuracy;
                } catch(Exception e) {
                    Diag.Ignore(e);
                    return false;
                }
            }
        }
    }
    private static void AfterAddHit(HitMargin hit) {
        EnsureConf();
        if(!MainCore.IsModEnabled || HitKinds.Of(hit) == HitKind.Auto) return;
        if(!Conf.JRestrictEnabled) return;
        if(InRestrictedSection() && ShouldFailFor(hit))
            TriggerFail(FormatJrMessage(Conf.JRestrictMessage, hit));
    }
    [HarmonyPatch]
    private static class AddHitPatch {
        private static MethodBase TargetMethod() => GameApi.AddHitTarget;
        private static void Postfix(HitMargin hit) => AfterAddHit(hit);
    }
    [HarmonyPatch(typeof(scnGame), "Play")]
    private static class ResetOnRunStartPatch {
        private static void Postfix() => ResetCounters();
    }
    [HarmonyPatch(typeof(scrController), "StartLoadingScene")]
    private static class ResetOnRunExitPatch {
        private static void Postfix() => ResetCounters();
    }
}

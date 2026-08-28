using HarmonyLib;
using Quartz.Compat.Game;
using Quartz.Core;
using Quartz.IO;
using System.Reflection;
namespace Quartz.Features.DeathLimit;
public static class DeathLimit {
    public static SettingsFile<DeathLimitSettings> ConfMgr { get; private set; }
    public static DeathLimitSettings Conf => ConfMgr?.Data;
    public static void EnsureConf() {
        if(ConfMgr != null) return;
        ConfMgr = new SettingsFile<DeathLimitSettings>(
            System.IO.Path.Combine(MainCore.Paths.RootPath, "DeathLimit.json"));
        if(!ConfMgr.Load() && LegacyRestriction.Adopt(ConfMgr.Data)) ConfMgr.Save();
    }
    public static void Save() => ConfMgr?.RequestSave();
    private static int missCount;
    private static int overloadCount;
    private static bool failTriggered;
    private static void ResetCounters() {
        missCount = 0;
        overloadCount = 0;
        failTriggered = false;
    }
    private static void TriggerFail(string reason) {
        try {
            scrController c = scrController.instance;
            if(c == null || failTriggered) return;
            failTriggered = GameApi.FailByHitbox(c, reason);
        } catch(Exception e) { Diag.Ignore(e); }
    }
    private static void AfterAddHit(HitMargin hit) {
        EnsureConf();
        if(!MainCore.IsModEnabled || hit == HitMargin.Auto) return;
        if(hit == HitMargin.FailMiss) missCount++;
        else if(hit == HitMargin.FailOverload) overloadCount++;
        DeathLimitSettings conf = Conf;
        if(conf == null || !conf.DeathLimitEnabled) return;
        int deaths = missCount + overloadCount;
        if((conf.MaxDeathsOn && deaths > conf.MaxDeaths)
        || (conf.MaxMissesOn && missCount > conf.MaxMisses)
        || (conf.MaxOverloadsOn && overloadCount > conf.MaxOverloads)) {
            TriggerFail(conf.DeathLimitMessage);
        }
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

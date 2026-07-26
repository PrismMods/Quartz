using DG.Tweening;
using HarmonyLib;
using Object = UnityEngine.Object;
using Quartz.Compat.Game;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using static Quartz.Features.Nostalgia.Nostalgia;
namespace Quartz.Features.Nostalgia;
public static class NostalgiaGameplayPatches {
    [HarmonyPatch(typeof(scrController), "Fail2_Update")]
    private static class OldPracticeModePatch {
        private static bool Prefix() {
            if(ShouldOldPracticeMode
               && scrController.instance.practiceAvailable
               && !GCS.practiceMode
               && Input.GetKeyDown(KeyCode.P)) {
                scrController.instance.SetPracticeMode(true);
                return false;
            }
            return true;
        }
    }
}

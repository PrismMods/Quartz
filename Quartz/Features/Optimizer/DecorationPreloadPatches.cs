using System.Collections;
using System.Reflection;
using ADOFAI;
using HarmonyLib;
using Quartz.Core;
namespace Quartz.Features.Optimizer;
// Entry points for DecorationPreloader. Both ways a custom level gets loaded are
// synchronous from scnGame.LoadLevel through the decoration pass, so the preload has
// to run (and the progress card has to render) before that chain starts:
//   * play: scnGame.LoadAndPlayLevel is deferred into a coroutine that preloads first
//     and then calls it again;
//   * editor: scnEditor.OpenLevelCo is already a coroutine, so it is wrapped and the
//     preload is yielded right before the step that loads the file.
public static class DecorationPreloadPatches {
    private static bool reentry;
    [HarmonyPatch(typeof(scnGame), nameof(scnGame.LoadAndPlayLevel))]
    private static class PlayLoadPatch {
        private static bool Prefix(scnGame __instance, string levelPath, ref bool __result) {
            if(reentry || __instance == null || ADOBase.isLevelEditor) return true;
            if(!DecorationPreloader.ShouldPreload(levelPath)) return true;
            __instance.StartCoroutine(PreloadThenPlay(__instance, levelPath));
            // The only caller (scnGame.Update) ignores the result.
            __result = true;
            return false;
        }
    }
    private static IEnumerator PreloadThenPlay(scnGame level, string levelPath) {
        yield return DecorationPreloader.Run(levelPath);
        if(level == null) {
            DecorationPreloader.DiscardPending();
            yield break;
        }
        reentry = true;
        try {
            level.LoadAndPlayLevel(levelPath);
        } finally {
            reentry = false;
        }
    }
    [HarmonyPatch(typeof(scnEditor), "OpenLevelCo", new[] { typeof(string) })]
    private static class EditorOpenPatch {
        private static bool Prepare() => AccessTools.Method(typeof(scnEditor), "OpenLevelCo", [typeof(string)]) != null;
        private static void Postfix(scnEditor __instance, string definedLevelPath, ref IEnumerator __result) {
            if(!DecorationPreloader.Active || __instance == null || __result == null) return;
            // Only wrap the real state machine; a prefix that swapped in its own
            // enumerator (AprilFools' quiz gate) means no load happens this time.
            if(!__result.GetType().Name.Contains("OpenLevelCo")) return;
            __result = WrapOpen(__instance, __result, definedLevelPath);
        }
    }
    private static IEnumerator WrapOpen(scnEditor editor, IEnumerator inner, string definedLevelPath) {
        bool preloaded = false;
        FieldInfo pickedField = PickedPathField(inner);
        while(true) {
            if(!preloaded) {
                string path = !string.IsNullOrEmpty(definedLevelPath)
                    ? definedLevelPath
                    : Sanitize(pickedField?.GetValue(inner) as string);
                if(path != null) {
                    preloaded = true;
                    if(DecorationPreloader.ShouldPreload(path))
                        yield return DecorationPreloader.Run(path);
                }
            }
            if(!inner.MoveNext()) yield break;
            yield return inner.Current;
        }
    }
    // OpenLevelCo's file-dialog branch keeps the picked path in a hoisted local; it is
    // set one step before the step that loads the level, which is exactly when the
    // wrapper needs it.
    private static FieldInfo PickedPathField(IEnumerator inner) {
        try {
            foreach(FieldInfo f in inner.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)) {
                if(f.FieldType == typeof(string) && f.Name.StartsWith("<levelPath>", StringComparison.Ordinal)) return f;
            }
        } catch(Exception e) { Diag.Ignore(e); }
        return null;
    }
    // Same as scnEditor.SanitizeLevelPath.
    private static string Sanitize(string path) {
        if(string.IsNullOrEmpty(path)) return null;
        try {
            return Uri.UnescapeDataString(path.Replace("file:", ""));
        } catch(Exception e) {
            Diag.Ignore(e);
            return path;
        }
    }
    [HarmonyPatch(typeof(scnGame), nameof(scnGame.LoadLevel))]
    private static class LoadLevelPatch {
        private static void Postfix(scnGame __instance, string levelPath, bool __result) {
            try {
                DecorationPreloader.Bind(__instance, levelPath, __result);
            } catch(Exception e) {
                Diag.Warn(e, "Optimizer/DecoPreload bind");
            }
        }
    }
    [HarmonyPatch(typeof(scnGame), nameof(scnGame.UpdateDecorationObjects))]
    private static class UpdateDecorationsPatch {
        private static void Prefix(scnGame __instance) {
            try {
                DecorationPreloader.Claim(__instance);
            } catch(Exception e) {
                Diag.Warn(e, "Optimizer/DecoPreload claim");
            }
        }
    }
}

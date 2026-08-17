using System;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using Quartz.Core;
using UnityEngine;
using Fools = Quartz.Core.AprilFools;
namespace Quartz.Features.AprilFools;
public static class QuizGate {
    private static readonly HashSet<string> cleared = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, QuizDifficulty> chartDifficulties =
        new(StringComparer.OrdinalIgnoreCase);
    private static bool officialGrace;
    private static float officialGraceExpiry;
    private static bool editorGrace;
    private static float editorGraceExpiry;
    private static bool bypass;
    private static bool Gated => Fools.Active && MainCore.IsModEnabled && !bypass;
    public static void RegisterChart(string chartPath, string difficultyName) {
        string key = NormalizePath(chartPath);
        if(key.Length == 0) return;
        chartDifficulties[key] = MapTufName(difficultyName);
    }
    public static void GateChart(string chartPath, string difficultyName, Action proceed) {
        RegisterChart(chartPath, difficultyName);
        Gate("path:" + NormalizePath(chartPath), MapTufName(difficultyName), proceed);
    }
    public static void GateMain(string code, string difficultyName, Action proceed) =>
        Gate("main:" + (code ?? ""), MapTufName(difficultyName), proceed, grantOfficialGrace: true);
    private static void Gate(string key, QuizDifficulty difficulty, Action proceed, bool grantOfficialGrace = false) {
        if(proceed == null) return;
        if(!Gated || cleared.Contains(key)) {
            proceed();
            return;
        }
        Announce("launch", key, difficulty);
        QuizOverlay.Show(difficulty, () => {
            cleared.Add(key);
            if(grantOfficialGrace) {
                officialGrace = true;
                officialGraceExpiry = Time.realtimeSinceStartup + 30f;
            }
            try {
                proceed();
            } catch(Exception e) {
                Diag.Warn(e, "AprilFools/QuizProceed");
            }
        });
    }
    public static QuizDifficulty MapTufName(string name) {
        if(string.IsNullOrEmpty(name)) return QuizBank.RandomNormal();
        if(string.Equals(name, "Impossible", StringComparison.OrdinalIgnoreCase))
            return QuizDifficulty.Impossible;
        char band = char.ToUpperInvariant(name[0]);
        if(name.Length >= 3 && char.ToUpperInvariant(name[1]) == 'Q' && char.IsDigit(name[2])) {
            int quantum = name[2] - '0';
            return band switch {
                'G' => quantum <= 1 ? QuizDifficulty.Grade6to7 : QuizDifficulty.Grade8to9,
                'U' => quantum <= 1 ? QuizDifficulty.Grade10to11 : QuizDifficulty.Grade12,
                _ => QuizBank.RandomNormal(),
            };
        }
        if(name.Length >= 2 && int.TryParse(name[1..], out int tier)) {
            switch(band) {
                case 'P': return tier <= 10 ? QuizDifficulty.Grade1to3 : QuizDifficulty.Grade4to5;
                case 'G': return tier <= 10 ? QuizDifficulty.Grade6to7 : QuizDifficulty.Grade8to9;
                case 'U': return tier <= 10 ? QuizDifficulty.Grade10to11 : QuizDifficulty.Grade12;
            }
        }
        return QuizBank.RandomNormal();
    }
    private static QuizDifficulty MapMainCode(string code) {
        if(string.IsNullOrEmpty(code)) return QuizBank.RandomNormal();
        int dash = code.IndexOf('-');
        string world = dash > 0 ? code[..dash] : code;
        if(int.TryParse(world, out int number)) {
            if(number <= 3) return QuizDifficulty.Grade1to3;
            if(number <= 6) return QuizDifficulty.Grade4to5;
            if(number <= 9) return QuizDifficulty.Grade6to7;
            if(number <= 12) return QuizDifficulty.Grade8to9;
            return QuizDifficulty.Grade10to11;
        }
        return QuizDifficulty.Grade12;
    }
    private static QuizDifficulty ChartDifficulty(string path) {
        string key = NormalizePath(path);
        return key.Length > 0 && chartDifficulties.TryGetValue(key, out QuizDifficulty found)
            ? found : QuizBank.RandomNormal();
    }
    private static string NormalizePath(string path) {
        if(string.IsNullOrWhiteSpace(path)) return "";
        try {
            return Path.GetFullPath(path);
        } catch(Exception e) {
            Diag.Ignore(e);
            return path;
        }
    }
    private static string SafeLevelPath() {
        try {
            return ADOBase.levelPath;
        } catch(Exception e) {
            Diag.Ignore(e);
            return null;
        }
    }
    private static string SafeCurrentLevel() {
        try {
            return ADOBase.currentLevel;
        } catch(Exception e) {
            Diag.Ignore(e);
            return null;
        }
    }
    private static string CurrentLevelPathKey() {
        string path = SafeLevelPath();
        return "path:" + (string.IsNullOrEmpty(path) ? "untitled" : NormalizePath(path));
    }
    private static string OfficialKey() => "official:" + (SafeCurrentLevel() ?? "");
    private static bool IsOfficial() {
        try {
            return ADOBase.isOfficialLevel;
        } catch(Exception e) {
            Diag.Ignore(e);
            return false;
        }
    }
    private static void Announce(string hook, string key, QuizDifficulty difficulty) =>
        MainCore.Log.Msg($"[AprilFools] pop quiz at {hook} for '{key}' ({difficulty})");
    private static void Reinvoke(Action action) {
        bypass = true;
        try {
            action();
        } catch(Exception e) {
            Diag.Warn(e, "AprilFools/QuizReinvoke");
        } finally {
            bypass = false;
        }
    }
    [HarmonyPatch(typeof(scnEditor), "OpenLevelCo", new[] { typeof(string) })]
    private static class EditorOpenPatch {
        private static bool Prefix(scnEditor __instance, string definedLevelPath,
            ref System.Collections.IEnumerator __result) {
            if(!Gated || __instance == null) return true;
            bool hasPath = !string.IsNullOrEmpty(definedLevelPath);
            string key = hasPath ? "path:" + NormalizePath(definedLevelPath) : "dialog";
            if(hasPath && cleared.Contains(key)) return true;
            QuizDifficulty difficulty = hasPath ? ChartDifficulty(definedLevelPath) : QuizBank.RandomNormal();
            Announce("scnEditor.OpenLevelCo", key, difficulty);
            scnEditor editor = __instance;
            QuizOverlay.Show(difficulty, () => {
                if(hasPath) cleared.Add(key);
                else {
                    editorGrace = true;
                    editorGraceExpiry = Time.realtimeSinceStartup + 120f;
                }
                if(editor == null) return;
                if(hasPath) Reinvoke(() => editor.OpenLevel(definedLevelPath));
                else Reinvoke(() => editor.OpenLevel());
            });
            __result = System.Linq.Enumerable.Empty<object>().GetEnumerator();
            return false;
        }
    }
    [HarmonyPatch(typeof(scnEditor), nameof(scnEditor.Play))]
    private static class EditorPlayPatch {
        private static bool Prefix(scnEditor __instance) {
            if(!Gated || __instance == null || __instance.playMode) return true;
            string key = CurrentLevelPathKey();
            if(editorGrace && Time.realtimeSinceStartup < editorGraceExpiry) {
                editorGrace = false;
                cleared.Add(key);
                return true;
            }
            if(cleared.Contains(key)) return true;
            QuizDifficulty difficulty = ChartDifficulty(SafeLevelPath());
            Announce("scnEditor.Play", key, difficulty);
            scnEditor editor = __instance;
            QuizOverlay.Show(difficulty, () => {
                cleared.Add(key);
                if(editor != null) Reinvoke(editor.Play);
            });
            return false;
        }
    }
    [HarmonyPatch(typeof(scrController), nameof(scrController.EnterLevel))]
    private static class EnterLevelPatch {
        private static bool Prefix(scrController __instance, string worldAndLevel, bool speedTrial) {
            if(!Gated || __instance == null) return true;
            string key = "main:" + (worldAndLevel ?? "");
            if(cleared.Contains(key)) return true;
            QuizDifficulty difficulty = MapMainCode(worldAndLevel);
            Announce("scrController.EnterLevel", key, difficulty);
            scrController controller = __instance;
            QuizOverlay.Show(difficulty, () => {
                cleared.Add(key);
                officialGrace = true;
                officialGraceExpiry = Time.realtimeSinceStartup + 30f;
                if(controller != null) Reinvoke(() => controller.EnterLevel(worldAndLevel, speedTrial));
            });
            return false;
        }
    }
    [HarmonyPatch(typeof(scnGame), nameof(scnGame.Play), new[] { typeof(int), typeof(bool) })]
    private static class GamePlayPatch {
        private static bool Prefix(scnGame __instance, int seqID, bool remakeFloors, ref bool __result) {
            if(!Gated || __instance == null) return true;
            bool official = IsOfficial();
            string key = official ? OfficialKey() : CurrentLevelPathKey();
            if(officialGrace && official && Time.realtimeSinceStartup < officialGraceExpiry) {
                officialGrace = false;
                cleared.Add(key);
                return true;
            }
            if(cleared.Contains(key)) return true;
            QuizDifficulty difficulty = official
                ? MapMainCode(SafeCurrentLevel())
                : ChartDifficulty(SafeLevelPath());
            Announce("scnGame.Play", key, difficulty);
            scnGame game = __instance;
            QuizOverlay.Show(difficulty, () => {
                cleared.Add(key);
                if(game != null) Reinvoke(() => game.Play(seqID, remakeFloors));
            });
            __result = false;
            return false;
        }
    }
}

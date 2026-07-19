using System.Reflection;
using HarmonyLib;
using Quartz.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Quartz.Features.Tuf;

// The base game's own charts are listed on TUF but have no download, so they land
// in TufItemState.Unavailable. Their API "suffix" carries the in-game level code
// (e.g. "(1-X)"), so instead of a dead "Unavailable" button we resolve that code
// against the game's world table and launch the real level in-game.
//
// Resolution touches WorldData (which lazily loads a Resources TextAsset) and must
// run on the main thread — never during the off-thread API parse. The result is
// memoized on the level so the per-card render check stays cheap.
internal static class TufMainLevel {
    // Set while a TUF-launched level is in play. A DLC level (Neo Cosmos / Taro /
    // Vega) normally quits back to its own DLC menu; since we launched it as a
    // one-off, redirect that exit to the normal level-select hub instead. Cleared
    // on the first quit-to-menu after launch.
    private static bool returnToLevelSelectOnExit;

    // Steam curator referral appended to store links (the mod team's curator).
    private const string CuratorTag = "?curator_clanid=34150082";

    // What an Unavailable card should do: nothing (plain unavailable), launch the
    // base-game level, or open its DLC's Steam store page.
    public enum TufMainAction { None, Play, BuyDlc }

    // Single entry point for the UI and runner. `codeOrUrl` is the in-game level code
    // for Play, or the store URL for BuyDlc.
    public static TufMainAction Resolve(TufLevel level, out string codeOrUrl) {
        codeOrUrl = "";
        if(!TryResolveCode(level, out string code)) return TufMainAction.None;
        string world = WorldOf(code);
        if(IsWorldAccessible(world)) {
            codeOrUrl = code;
            return TufMainAction.Play;
        }
        // A real DLC level the player can't launch (not owned/installed): offer to buy
        // it, on the store that fits the build (Steam vs itch). Null when we have no
        // usable link for this platform, in which case it stays plain "Unavailable".
        string url = DlcStoreUrl(world);
        if(!string.IsNullOrEmpty(url)) {
            codeOrUrl = url;
            return TufMainAction.BuyDlc;
        }
        return TufMainAction.None;
    }

    // Resolves and caches the in-game level code for `level`, if any (regardless of
    // whether its DLC is owned). Only no-download levels — the base game's own charts
    // — qualify; everything else is "".
    public static bool TryResolveCode(TufLevel level, out string code) {
        code = "";
        if(level == null || level.DownloadUri != null) return false;
        if(!level.MainLevelResolved) {
            level.MainLevelCode = Compute(level.Suffix);
            level.MainLevelResolved = true;
        }
        code = level.MainLevelCode;
        return code.Length > 0;
    }

    // "(1-X)"/"1-X" -> "1-X"; "(XO-1)" -> "XO-1"; "(CE-TX)" -> "CETX-X" (some collab
    // worlds are one key that TUF prints with a cosmetic dash). "" when the suffix
    // names no real world.
    private static string Compute(string suffix) {
        if(string.IsNullOrWhiteSpace(suffix)) return "";
        string s = suffix.Trim();
        if(s.Length >= 2 && s[0] == '(' && s[^1] == ')') s = s[1..^1].Trim();
        if(s.Length == 0) return "";
        // 1) Straight "world-level": a real world plus level "X" or a valid number.
        int dash = s.IndexOf('-');
        if(dash > 0 && dash < s.Length - 1) {
            string world = s[..dash].Trim();
            string lvl = s[(dash + 1)..].Trim();
            if(IsWorldCode(world) && IsRealLevel(world, lvl)) return world + "-" + lvl;
        }
        // 2) The dash is cosmetic and the whole thing is one world key ("CE-TX" ->
        //    "CETX"). Those are single showcase worlds, so open the main level ("-X",
        //    which LevelData maps to InternalLevels/<world>/main).
        string joined = s.Replace("-", "").Trim();
        if(joined != s && IsWorldCode(joined)) return joined + "-X";
        return "";
    }

    private static string WorldOf(string code) {
        int dash = code.IndexOf('-');
        return dash > 0 ? code[..dash] : code;
    }

    // A store page to buy the DLC that owns `world`, or null (not a DLC world, or no
    // usable link for this build). Platform-aware: Steam store on Steam, itch page on
    // a non-Steam build.
    private static string DlcStoreUrl(string world) {
        try {
            var managers = DLCManager.DLCManagers;
            if(managers != null)
                foreach(DLCManager mgr in managers)
                    if(mgr != null && mgr.IsDLCLevel(world))
                        return StoreUrlFor(mgr);
        } catch { }
        return null;
    }

    private static string StoreUrlFor(DLCManager mgr) {
        // On Steam, the appId link works for every DLC and opens in the overlay. Off
        // Steam (itch build), a Steam link is useless — use the itch page when known.
        if(SteamInitialized() && mgr.steamAppId != 0)
            return $"https://store.steampowered.com/app/{mgr.steamAppId}/{CuratorTag}";
        // groupName comes straight from the game's DLCManager.
        return mgr.groupName switch {
            "Neo Cosmos" => "https://fizzd.itch.io/neo-cosmos",
            _ => null,
        };
    }

    // Opens the store page in Steam's in-game overlay when it's available, matching
    // how the game shows its own DLC prompts; otherwise the system browser. Steam
    // itself falls back to the browser if the overlay is disabled, so the overlay
    // call is always safe when Steam is initialized.
    public static void OpenStore(string url) {
        if(string.IsNullOrEmpty(url)) return;
        if(TryOpenInSteamOverlay(url)) return;
        try {
            Application.OpenURL(url);
        } catch(Exception e) {
            MainCore.Log.Wrn("[TUF] could not open DLC store page: " + e);
        }
    }

    // Reflection, not a Steamworks reference: the type may be absent (non-Steam
    // build) and its API has drifted before, so a hard dependency is a liability.
    // Runtime Steam presence — false on itch/other non-Steam builds. Cached property
    // lookup; the value itself is read live. Reflection so a Steam-less build (where
    // the type may be missing or its Instance null) degrades instead of throwing.
    private static PropertyInfo steamInitializedProp;
    private static bool steamInitializedResolved;
    private static bool SteamInitialized() {
        try {
            if(!steamInitializedResolved) {
                Type manager = AccessTools.TypeByName("SteamManager");
                steamInitializedProp = manager != null ? AccessTools.Property(manager, "Initialized") : null;
                steamInitializedResolved = true;
            }
            return steamInitializedProp?.GetValue(null) is true;
        } catch {
            return false;
        }
    }

    private static bool TryOpenInSteamOverlay(string url) {
        try {
            if(ADOBase.isSwitch || !SteamInitialized()) return false; // no overlay -> browser
            Type friends = AccessTools.TypeByName("Steamworks.SteamFriends");
            MethodInfo open = friends != null ? AccessTools.Method(friends, "ActivateGameOverlayToWebPage") : null;
            if(open == null) return false;
            // Signature is (string url, EActivateGameOverlayToWebPageMode mode = Default);
            // 0 is the Default mode. Pass it positionally so the optional arg is filled.
            ParameterInfo[] ps = open.GetParameters();
            object[] args = ps.Length >= 2
                ? new object[] { url, Enum.ToObject(ps[1].ParameterType, 0) }
                : new object[] { url };
            open.Invoke(null, args);
            return true;
        } catch(Exception e) {
            MainCore.Log.Wrn("[TUF] Steam overlay open failed, using browser: " + e);
            return false;
        }
    }

    // Neo Cosmos / Vega levels live behind a Steam DLC. Launching one the player does
    // not own softlocks the game (the scene never loads), so treat it as unavailable.
    // Mirrors the game's own level-select gate: a DLC world is enabled iff its DLC is
    // installed and up to date (install implies ownership). Base-game worlds are
    // always accessible.
    private static bool IsWorldAccessible(string world) {
        try {
            var managers = DLCManager.DLCManagers;
            if(managers != null) {
                foreach(DLCManager mgr in managers)
                    if(mgr != null && mgr.IsDLCLevel(world))
                        return mgr.installed && mgr.upToDate;
                return true; // no DLC manager claims it -> base game
            }
        } catch { }
        // Managers not ready: fall back to the same name rule the managers use, and
        // refuse anything that looks like DLC rather than risk a softlock.
        return !LooksLikeDlcWorld(world);
    }

    private static bool LooksLikeDlcWorld(string world) {
        if(string.IsNullOrEmpty(world)) return false;
        if(world[0] == 'T') return true;          // Neo Cosmos (Taro worlds)
        return world.EndsWith("EX");              // Team Vega (non-T "…EX" worlds)
    }

    // A real world key: short, alphanumeric, and present in the game's world table.
    private static bool IsWorldCode(string world) {
        if(world.Length == 0) return false;
        foreach(char c in world)
            if(!char.IsLetterOrDigit(c)) return false;
        try { return WorldData.dict.ContainsKey(world); }
        catch { return false; }
    }

    // Validates against the game's live world table. WorldData.dict lazily loads a
    // bundled TextAsset the first time, so this stays on the main thread.
    private static bool IsRealLevel(string world, string lvl) {
        try {
            if(!WorldData.dict.TryGetValue(world, out WorldData data)) return false;
            if(lvl == "X") return true;
            return int.TryParse(lvl, out int n) && n >= 1 && n <= data.levelCount;
        } catch {
            return false;
        }
    }

    // Loads the base-game level. Prefers the game's own EnterLevel when a controller
    // exists; from the main menu (no controller) it replicates EnterLevel's GCS setup
    // and kicks the scene loader directly.
    public static bool Launch(string code) {
        if(string.IsNullOrEmpty(code)) return false;
        try {
            // Arm the exit redirect before the level loads; the QuitToMainMenu patch
            // consumes it. Cleared here on failure so it can't leak to a later exit.
            returnToLevelSelectOnExit = true;
            scrController controller = ADOBase.controller;
            if(controller != null) {
                controller.EnterLevel(code, false);
                return true;
            }
            bool internalLevel = scrController.IsWorldAndLevelInternalLevel(code);
            GCS.speedTrialMode = false;
            GCS.nextSpeedRun = 1f;
            GCS.practiceMode = false;
            GCS.customLevelPaths = null;
            GCS.customLevelIndex = 0;
            GCS.internalLevelName = internalLevel ? code : null;
            GCS.sceneToLoad = internalLevel ? "scnGame" : code;
            scrLoader loader = ADOBase.loader;
            if(loader != null) loader.LoadSceneWithTransition(WipeDirection.StartsFromRight);
            else SceneManager.LoadScene(GCS.sceneToLoad);
            return true;
        } catch(Exception e) {
            returnToLevelSelectOnExit = false;
            MainCore.Log.Wrn($"[TUF] base-game level launch failed for '{code}': {e}");
            return false;
        }
    }

    // A DLC level quit sets sceneToLoad to its own DLC menu (Neo Cosmos, etc.). When
    // we launched the level from TUF, send the player to the normal level-select hub
    // instead. Leaves web and custom-level-paths exits (which pick their own scene)
    // untouched.
    [HarmonyPatch(typeof(scrController), "QuitToMainMenu")]
    private static class ExitToHubPatch {
        private static void Postfix() {
            if(!returnToLevelSelectOnExit) return;
            returnToLevelSelectOnExit = false;
            try {
                if(!GCS.webVersion && GCS.customLevelPaths == null)
                    GCS.sceneToLoad = GCNS.sceneLevelSelect;
            } catch(Exception e) {
                MainCore.Log.Wrn("[TUF] exit-to-hub redirect failed: " + e);
            }
        }
    }
}

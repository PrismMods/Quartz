#if DEBUG && !IL2CPP
using System.Collections;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Quartz.Compat.Game;
using Quartz.Core;
using Quartz.Features.Combo;
using Quartz.Features.PlayCount;
using Quartz.Game.Stats;
using Quartz.Overlay;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace Quartz.UI.FacetUi;
/// <summary>
/// The Combo preview inside a level, run alone under the marker facet-level-selftest: it enters 1-1 from the level select and
/// opens Overlay ▸ Combo there. The pane shows the live count, paused too, while the element is off every game camera, and
/// switching to General puts the element back on screen within a frame. Logs every camera's culling mask and what a canvas
/// walk costs. Entering a level starts a run, which Quartz's PlayCount counts: the test puts 1-1's entry back and saves it,
/// so the attempts, in memory and in PlayCount.json, end as they began. The game's own save (User/data.sav, its current
/// level) still moves, and only a rig puts that back.
/// </summary>
internal static partial class FacetSelfTestChecks {
    private const string LevelCode = "1-1";
    private const string MapKey = "official:" + LevelCode;   // PlayCount's key for the level

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static IEnumerator InLevel(Action<string, bool, string> check, Action<string> info) {
        var windows = new Dictionary<string, string>(MainCore.Conf.FacetWindows);
        PlayData played = PlayCount.Snapshot(MapKey);
        string file = PlayCount.FilePath;
        byte[] bytesWas = File.Exists(file) ? File.ReadAllBytes(file) : null;
        try {
            yield return EnterLevel(check, info);
            if(!GameStats.InLevel) yield break;
            string id = ComboOverlay.FontKey;
            int layerWas = -1;
            bool shownWas = false;
            yield return OnPage("combo", ComboPage, () => {
                // Read with the features on and the menu still shut: the element as the level shows it.
                Canvas c = OverlayPreview.CanvasOf(id);
                layerWas = c != null ? c.gameObject.layer : -1;
                shownWas = OverlayPreview.ShownOf(id) is { } r && r.activeInHierarchy;
            }, () => { }, () => ComboInLevel(check, info, layerWas, shownWas));
        } finally {
            MainCore.Conf.FacetWindows.Clear();
            foreach(var kv in windows) MainCore.Conf.FacetWindows[kv.Key] = kv.Value;
            MainCore.ConfMgr.RequestSave();
            int before = played?.TotalAttempts ?? 0, inRun = PlayCount.Snapshot(MapKey)?.TotalAttempts ?? 0;
            PlayCount.Restore(MapKey, played);
            int after = PlayCount.Snapshot(MapKey)?.TotalAttempts ?? 0;
            // The file, not just the entry Restore has just written: with none before, the run made one, so only the count is read.
            bool bytes = bytesWas == null ? after == 0 : File.Exists(file) && File.ReadAllBytes(file).SequenceEqual(bytesWas);
            if(inRun == before) info($"level: the run counted no attempt for {LevelCode} (autoplay counts none), so the take-back went untested");
            check($"level: the run leaves {LevelCode}'s attempts in Quartz's play stats as they were, in memory and in PlayCount.json",
                after == before && bytes, $"before={before} inRun={inRun} after={after} entry={played != null} file={bytesWas != null} bytesSame={bytes}");
        }
    }

    // From the level select, as the planet entering the portal does; the level then waits for its first key.
    private static IEnumerator EnterLevel(Action<string, bool, string> check, Action<string> info) {
        float t0 = Time.realtimeSinceStartup;
        while(Time.realtimeSinceStartup - t0 < 30f && (scrController.instance == null || SceneManager.GetActiveScene().name != GameApi.SceneLevelSelect))
            yield return null;
        string from = SceneManager.GetActiveScene().name;
        if(from != GameApi.SceneLevelSelect || scrController.instance == null) {
            check($"level: the level select is up within 30 s, to enter {LevelCode} from", false, $"scene={from} controller={scrController.instance != null}");
            yield break;
        }
        scrController.instance.EnterLevel(LevelCode, false);
        t0 = Time.realtimeSinceStartup;
        while(Time.realtimeSinceStartup - t0 < 30f && !GameStats.InGame) yield return null;
        float s = Time.realtimeSinceStartup - t0;
        yield return Frames(120);   // the wipe in ends
        scrController c = scrController.instance;
        info($"level: from {from}, scene {SceneManager.GetActiveScene().name} after {s:0.0} s, gameworld={c != null && c.gameworld} paused={c != null && c.paused}");
        check($"level: {LevelCode} entered from the level select and playing within 30 s", from == GameApi.SceneLevelSelect && GameStats.InGame,
            $"from={from} scene={SceneManager.GetActiveScene().name} inLevel={GameStats.InLevel} inGame={GameStats.InGame} s={s:0.0}");
    }

    private static IEnumerator ComboInLevel(Action<string, bool, string> check, Action<string> info, int layerWas, bool shownWas) {
        string id = ComboOverlay.FontKey;
        int frames = 0;
        for(; frames < 10 && OverlayPreview.Current?.Texture == null; frames++) yield return null;
        OverlayPreview.Lease lease = OverlayPreview.Current;
        Canvas canvas = OverlayPreview.CanvasOf(id);
        GameObject root = OverlayPreview.ShownOf(id);
        TextMeshProUGUI value = root != null ? root.transform.Find("Value")?.GetComponent<TextMeshProUGUI>() : null;
        if(lease == null || !OverlayPreview.Is(id) || canvas == null || root == null || value == null) {
            check("level: the Combo pane borrows the element within 10 frames", false,
                $"lease={lease != null} target={OverlayPreview.Target} canvas={canvas != null} value={value != null}");
            yield break;
        }
        // Live: the count as the level keeps it, written with SetCharArray, so read off the regenerated mesh.
        string Shows() {
            value.ForceMeshUpdate();
            return value.GetParsedText();
        }
        int count = Combo.Count;
        string seven, zero;
        try {
            Combo.Count = 7;
            for(frames = 0; frames < 5 && Shows() != "7"; frames++) yield return null;
            seven = Shows();
            Combo.Count = 0;
            for(frames = 0; frames < 5 && Shows() != "0"; frames++) yield return null;
            zero = Shows();
        } finally {
            Combo.Count = count;
        }
        check("level: the Combo pane shows the live count, 7 then 0, not the sample 42", GameStats.InLevel && seven == "7" && zero == "0",
            $"seven='{seven}' zero='{zero}' inLevel={GameStats.InLevel}");
        // Paused, InGame is false while InLevel holds: the pane still shows the level's count, not the sample. The property, not
        // TogglePauseGame, which Calibration patches.
        string paused;
        bool pausedInGame, pausedInLevel;
        try {
            scrController.instance.paused = true;
            Combo.Count = 0;
            yield return Frames(2);
            (paused, pausedInGame, pausedInLevel) = (Shows(), GameStats.InGame, GameStats.InLevel);
        } finally {
            if(scrController.instance != null) scrController.instance.paused = false;
            Combo.Count = count;
        }
        check("level: paused in the level, the Combo pane shows the live count 0, not the sample 42", !pausedInGame && pausedInLevel && paused == "0",
            $"shows='{paused}' inGame={pausedInGame} inLevel={pausedInLevel}");
        // Off screen: world space, parked off-world, every object on the preview layer, which no game camera draws.
        int layer = lease.Layer, stray = canvas.GetComponentsInChildren<Transform>(true).Count(t => t.gameObject.layer != layer);
        var drawing = new List<string>();
        bool drawn31 = false;
        foreach(Camera cam in Camera.allCameras) {   // enabled ones only: the preview's own camera is rendered by hand
            info($"level: camera {cam.name} ({cam.gameObject.scene.name}) mask=0x{cam.cullingMask:X8} depth={cam.depth} texture={cam.targetTexture != null}");
            if((cam.cullingMask & (1 << layer)) != 0) drawing.Add(cam.name);
            drawn31 |= (cam.cullingMask & (1 << 31)) != 0;
        }
        check("level: Combo has left the screen while previewed: world space, parked off-world, all on the preview layer, which no camera draws",
            canvas.renderMode == RenderMode.WorldSpace && canvas.transform.position.y < -10000f && stray == 0 && drawing.Count == 0,
            $"mode={canvas.renderMode} y={canvas.transform.position.y:0} layer={layer} stray={stray} drawnBy={string.Join(",", drawing)} cameras={Camera.allCameras.Length}");
        check("level: no camera in the level draws layer 31", !drawn31, $"layer={layer}");
        // Hits as a level makes them, then one object added under the canvas: the walk it costs.
        int walks0 = OverlayPreview.Relayers, renders0 = OverlayPreview.Renders;
        long ticks0 = OverlayPreview.RelayerTicks;
        try {
            for(int i = 0; i < 120; i++) {
                if(i % 6 == 0) {
                    Combo.Count++;
                    Combo.PulseStartTime = Time.realtimeSinceStartup;
                }
                yield return null;
            }
        } finally {
            Combo.Count = count;
            Combo.PulseStartTime = -1f;
        }
        int byHits = OverlayPreview.Relayers - walks0;
        var extra = new GameObject("QuartzSelfTestRelayer", typeof(RectTransform));
        extra.transform.SetParent(root.transform, false);
        yield return Frames(2);
        int extraLayer = extra.layer;
        UnityEngine.Object.Destroy(extra);
        yield return Frames(2);
        int walks = OverlayPreview.Relayers - walks0;
        double us = walks == 0 ? -1 : (OverlayPreview.RelayerTicks - ticks0) * 1e6 / Stopwatch.Frequency / walks;
        MainCore.Log.Msg($"[perf] in a level: {byHits} canvas walks over 120 frames of hits, {walks} with an object added and removed, {us:0.0} µs each");
        info($"[perf] level: Relayers {walks0} -> {OverlayPreview.Relayers} ({byHits} from 120 frames of hits), {us:0.0} µs per walk, "
            + $"{OverlayPreview.Renders - renders0} renders");
        check("level: an object made under the borrowed canvas goes on the preview layer at the next render", extraLayer == layer && walks > byHits,
            $"layer={extraLayer}/{layer} walks={walks} byHits={byHits}");
        // Back: General has no pane, so the lease goes as the tab switches. Every object leaves the preview layer, not just the root.
        QuartzShell.Go("overlay.general");
        yield return Frames(1);
        int left = canvas.GetComponentsInChildren<Transform>(true).Count(t => t.gameObject.layer == layer);
        check("level: switching to General puts Combo back on screen within 1 frame: overlay mode, its layer, nothing left on the preview layer, "
            + "shown as before the menu",
            shownWas && OverlayPreview.Target == null && canvas.renderMode == RenderMode.ScreenSpaceOverlay && canvas.gameObject.layer == layerWas
            && left == 0 && root.activeInHierarchy && GameObject.Find("QuartzPreviewCamera") == null,
            $"shownBefore={shownWas} target={OverlayPreview.Target} mode={canvas.renderMode} layer={canvas.gameObject.layer}/{layerWas} "
            + $"onPreviewLayer={left} shown={root.activeInHierarchy} page={QuartzShell.CurrentPage}");
    }
}
#endif

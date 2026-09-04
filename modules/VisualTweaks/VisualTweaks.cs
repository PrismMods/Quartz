using Quartz.Core;
using Quartz.Compat.Game;
using Quartz.IO;
using UnityEngine;
namespace Quartz.Features.VisualTweaks;
public static partial class VisualTweaks {
    public static SettingsFile<VisualTweaksSettings> ConfMgr { get; private set; }
    public static VisualTweaksSettings Conf => ConfMgr?.Data;
    public static void EnsureConf() {
        if(ConfMgr != null) return;
        ConfMgr = new SettingsFile<VisualTweaksSettings>(
            System.IO.Path.Combine(MainCore.Paths.RootPath, "VisualTweaks.json"));
        if(!ConfMgr.Load() && LegacyTweaks.Adopt(ConfMgr.Data)) ConfMgr.Save();
    }
    public static void Save() => ConfMgr?.RequestSave();
    private static bool Enabled {
        get {
            EnsureConf();
            return MainCore.IsModEnabled;
        }
    }
    private static bool ShouldRemoveCheckpoints => Enabled && Conf.RemoveAllCheckpoints;
    private static bool ShouldRemoveBallCoreParticles => Enabled && Conf.RemoveBallCoreParticles;
    private static bool ShouldDisableTileHitGlow => Enabled && Conf.DisableTileHitGlow;
    private static bool ShouldRemovePlanetGlow => Enabled && Conf.RemovePlanetGlow;
    private static readonly Dictionary<int, bool> particleActiveStates = [];
    private static readonly Dictionary<int, bool> particleRendererEnabledStates = [];
    private static readonly Dictionary<int, bool> particleEmissionEnabledStates = [];
    private static readonly Dictionary<int, ParticleSystem.MinMaxCurve> particleEmissionRateStates = [];
    private static readonly Dictionary<int, int> particleMaxParticleStates = [];
    private static readonly Dictionary<int, bool> lightUpDisableGlowStates = [];
    private static readonly Dictionary<int, bool> planetGlowEnabledStates = [];
    private static readonly Dictionary<int, bool> floorGlowActiveStates = [];
    private static readonly Dictionary<int, (ParticleSystem, ParticleSystem)> planetParticleCache = [];
    private static readonly HashSet<int> suppressNextRandomColorFloorIds = [];
    private static readonly ffxCheckpoint[] EmptyCheckpoints = [];
    private static readonly PlanetRenderer[] EmptyRenderers = [];
    private static readonly scrFloor[] EmptyFloors = [];
    private static ffxCheckpoint[] cachedCheckpoints;
    private static PlanetRenderer[] cachedRenderers;
    private static scrFloor[] cachedFloors;
    private static int lightUpDepth;
    private static T[] FindObjectsCompat<T>() where T : UnityEngine.Object
        => UnityEngine.Object.FindObjectsByType<T>(FindObjectsSortMode.None);
    public static void ClearSceneCaches() {
        InvalidateCheckpointCache();
        InvalidateRendererCache();
        InvalidateFloorCache();
        suppressNextRandomColorFloorIds.Clear();
        planetParticleCache.Clear();
        particleActiveStates.Clear();
        particleRendererEnabledStates.Clear();
        particleEmissionEnabledStates.Clear();
        particleEmissionRateStates.Clear();
        particleMaxParticleStates.Clear();
        lightUpDisableGlowStates.Clear();
        planetGlowEnabledStates.Clear();
        floorGlowActiveStates.Clear();
    }
    private static void InvalidateCheckpointCache() => cachedCheckpoints = null;
    private static void InvalidateRendererCache() => cachedRenderers = null;
    private static void InvalidateFloorCache() => cachedFloors = null;
    private static ffxCheckpoint[] GetCheckpoints() {
        if(cachedCheckpoints != null) return cachedCheckpoints;
        try { cachedCheckpoints = FindObjectsCompat<ffxCheckpoint>(); }
        catch(Exception e) { Diag.Ignore(e); cachedCheckpoints = EmptyCheckpoints; }
        return cachedCheckpoints ?? EmptyCheckpoints;
    }
    private static PlanetRenderer[] GetPlanetRenderers() {
        if(cachedRenderers != null) return cachedRenderers;
        try { cachedRenderers = FindObjectsCompat<PlanetRenderer>(); }
        catch(Exception e) { Diag.Ignore(e); cachedRenderers = EmptyRenderers; }
        return cachedRenderers ?? EmptyRenderers;
    }
    private static scrFloor[] GetFloors() {
        if(cachedFloors != null) return cachedFloors;
        try { cachedFloors = FindObjectsCompat<scrFloor>(); }
        catch(Exception e) { Diag.Ignore(e); cachedFloors = EmptyFloors; }
        return cachedFloors ?? EmptyFloors;
    }
    public static void RefreshAll() {
        RefreshCheckpointTweak();
        RefreshBallCoreParticlesTweak();
        RefreshTileHitGlowTweak();
        RefreshPlanetGlowTweak();
    }
    public static void RestoreAll() {
        RefreshBallCoreParticlesTweak(true);
        RefreshPlanetGlowTweak(true);
        RefreshTileHitGlowTweak(true);
    }
    public static void RefreshCheckpointTweak() {
        if(!ShouldRemoveCheckpoints) return;
        ffxCheckpoint[] checkpoints = GetCheckpoints();
        for(int i = 0; i < checkpoints.Length; i++) {
            RemoveCheckpointVisual(checkpoints[i]);
        }
    }
    public static void RefreshPlanetGlowTweak() => RefreshPlanetGlowTweak(false);
    private static void RefreshPlanetGlowTweak(bool forceRestore) {
        PlanetRenderer[] renderers = GetPlanetRenderers();
        for(int i = 0; i < renderers.Length; i++) {
            ApplyPlanetGlowTweak(renderers[i], forceRestore);
        }
    }
    private static void ApplyPlanetGlowTweak(PlanetRenderer renderer, bool forceRestore = false) {
        if(renderer == null) return;
        SpriteRenderer glow;
        try { glow = renderer.glow; } catch(Exception e) { Diag.Ignore(e); return; }
        if(glow == null) return;
        int id = glow.GetInstanceID();
        if(ShouldRemovePlanetGlow && !forceRestore) {
            if(!planetGlowEnabledStates.ContainsKey(id)) planetGlowEnabledStates[id] = glow.enabled;
            glow.enabled = false;
        } else if(planetGlowEnabledStates.TryGetValue(id, out bool wasEnabled)) {
            glow.enabled = wasEnabled;
            planetGlowEnabledStates.Remove(id);
        }
    }
    public static void RefreshTileHitGlowTweak() => RefreshTileHitGlowTweak(false);
    private static void RefreshTileHitGlowTweak(bool forceRestore) {
        if(!ShouldDisableTileHitGlow && floorGlowActiveStates.Count == 0) return;
        scrFloor[] floors = GetFloors();
        for(int i = 0; i < floors.Length; i++) {
            SuppressFloorHitGlow(floors[i], forceRestore);
        }
    }
    private static void RemoveCheckpointVisual(ffxCheckpoint checkpoint) {
        if(checkpoint == null) return;
        scrFloor floor = null;
        try { floor = checkpoint.floor; } catch(Exception e) { Diag.Ignore(e); }
        if(floor == null) {
            try { floor = checkpoint.GetComponent<scrFloor>(); } catch(Exception e) { Diag.Ignore(e); }
        }
        if(floor == null) return;
        try {
            if(floor.floorIcon == FloorIcon.Checkpoint) {
                floor.floorIcon = FloorIcon.None;
                floor.UpdateIconSprite(true);
            }
        } catch(Exception e) { Diag.Ignore(e); }
    }
    public static void RefreshBallCoreParticlesTweak() => RefreshBallCoreParticlesTweak(false);
    private static void RefreshBallCoreParticlesTweak(bool forceRestore) {
        PlanetRenderer[] renderers = GetPlanetRenderers();
        for(int i = 0; i < renderers.Length; i++) {
            ApplyBallCoreParticlesTweak(renderers[i], forceRestore);
        }
    }
    private static void ApplyBallCoreParticlesTweak(PlanetRenderer renderer, bool forceRestore = false) {
        if(renderer == null) return;
        if(!forceRestore && !ShouldRemoveBallCoreParticles && !HasParticleTweakState) return;
        (ParticleSystem core, ParticleSystem sparks) = GetPlanetParticles(renderer);
        ApplyPlanetParticleTweak(core, forceRestore);
        ApplyPlanetParticleTweak(sparks, forceRestore);
    }
    private static bool HasParticleTweakState => particleActiveStates.Count != 0 || particleRendererEnabledStates.Count != 0
        || particleEmissionEnabledStates.Count != 0 || particleEmissionRateStates.Count != 0 || particleMaxParticleStates.Count != 0;
    private static (ParticleSystem, ParticleSystem) GetPlanetParticles(PlanetRenderer renderer) {
        int id = renderer.GetInstanceID();
        if(planetParticleCache.TryGetValue(id, out (ParticleSystem, ParticleSystem) cached)) return cached;
        (ParticleSystem, ParticleSystem) resolved = (GetCoreParticles(renderer), GetSparks(renderer));
        planetParticleCache[id] = resolved;
        return resolved;
    }
    private static ParticleSystem GetCoreParticles(PlanetRenderer renderer)
        => GetPlanetRendererParticle(renderer, "coreParticles");
    private static ParticleSystem GetSparks(PlanetRenderer renderer)
        => GetPlanetRendererParticle(renderer, "sparks");
    private static ParticleSystem GetPlanetRendererParticle(PlanetRenderer renderer, string name)
        => TryGetPlanetRendererMemberValue(renderer, name, out object value) ? value as ParticleSystem : null;
    private static bool TryGetPlanetRendererMemberValue(PlanetRenderer renderer, string name, out object value) =>
        Refl.TryRead(renderer, name, out value);
    private static bool IsRemovedPlanetParticle(PlanetRenderer renderer, ParticleSystem particles) {
        if(renderer == null || particles == null) return false;
        (ParticleSystem core, ParticleSystem sparks) = GetPlanetParticles(renderer);
        return particles == core || particles == sparks;
    }
    private static void ApplyPlanetParticleTweak(ParticleSystem particles, bool forceRestore) {
        if(particles == null) return;
        GameObject particleObject = particles.gameObject;
        if(particleObject == null) return;
        if(ShouldRemoveBallCoreParticles && !forceRestore) {
            try {
                int rootId = particleObject.GetInstanceID();
                if(particleActiveStates.ContainsKey(rootId) && !particleObject.activeSelf) return;
            } catch(Exception e) { Diag.Ignore(e); }
            DisableParticleSystemTree(particles, particleObject);
            return;
        }
        RestoreParticleSystemTree(particleObject);
    }
    private static void SuppressFloorHitGlow(scrFloor floor, bool forceRestore = false) {
        if(floor == null) return;
        bool remove = ShouldDisableTileHitGlow && !forceRestore;
        HideFloorGlowObject(floor.topGlow, remove);
        HideFloorGlowObject(floor.bottomGlow, remove);
        if(remove) RestoreFloorHitColor(floor);
    }
    private static void HideFloorGlowObject(SpriteRenderer glow, bool remove) {
        if(glow == null) return;
        int id = glow.GetInstanceID();
        if(remove) {
            try {
                if(!floorGlowActiveStates.ContainsKey(id)) floorGlowActiveStates[id] = glow.gameObject.activeSelf;
                glow.gameObject.SetActive(false);
            } catch(Exception e) { Diag.Ignore(e); }
        } else if(floorGlowActiveStates.TryGetValue(id, out bool wasActive)) {
            try { glow.gameObject.SetActive(wasActive); } catch(Exception e) { Diag.Ignore(e); }
            floorGlowActiveStates.Remove(id);
        }
    }
    private static void RestoreFloorHitColor(scrFloor floor) {
        if(floor == null) return;
        try {
            if(floor.floorRenderer == null) return;
            if(floor.specialColorType != TrackColorType.Single
            && floor.specialColorType != TrackColorType.Stripes)
                return;
            Color color = floor.floorRenderer.deselectedColor;
            if(color.a <= 0.001f && floor.floorRenderer.cachedColor.a > 0.001f)
                color = floor.floorRenderer.cachedColor;
            floor.floorRenderer.color = color;
        } catch(Exception e) { Diag.Ignore(e); }
    }
    private static void DisableParticleSystemTree(ParticleSystem particles, GameObject particleObject) {
        RememberActiveState(particleObject);
        try { particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); } catch(Exception e) { Diag.Ignore(e); }
        try { particles.Clear(true); } catch(Exception e) { Diag.Ignore(e); }
        try { DisableParticleSystemEmission(particles); } catch(Exception e) { Diag.Ignore(e); }
        DisableRenderers(particleObject);
        try {
            ParticleSystem[] children = particleObject.GetComponentsInChildren<ParticleSystem>(true);
            for(int i = 0; i < children.Length; i++) {
                ParticleSystem child = children[i];
                if(child == null) continue;
                RememberActiveState(child.gameObject);
                try { child.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); } catch(Exception e) { Diag.Ignore(e); }
                try { child.Clear(true); } catch(Exception e) { Diag.Ignore(e); }
                try { DisableParticleSystemEmission(child); } catch(Exception e) { Diag.Ignore(e); }
            }
        } catch(Exception e) { Diag.Ignore(e); }
        try { particleObject.SetActive(false); } catch(Exception e) { Diag.Ignore(e); }
    }
    private static void DisableParticleSystemEmission(ParticleSystem particles) {
        int id = particles.GetInstanceID();
        ParticleSystem.EmissionModule emission = particles.emission;
        if(!particleEmissionEnabledStates.ContainsKey(id)) particleEmissionEnabledStates[id] = emission.enabled;
        if(!particleEmissionRateStates.ContainsKey(id)) particleEmissionRateStates[id] = emission.rateOverTime;
        emission.enabled = false;
        emission.rateOverTime = 0f;
        ParticleSystem.MainModule main = particles.main;
        if(!particleMaxParticleStates.ContainsKey(id)) particleMaxParticleStates[id] = main.maxParticles;
        main.maxParticles = 0;
    }
    private static void DisableRenderers(GameObject root) {
        if(root == null) return;
        try {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            for(int i = 0; i < renderers.Length; i++) {
                Renderer renderer = renderers[i];
                if(renderer == null) continue;
                int id = renderer.GetInstanceID();
                if(!particleRendererEnabledStates.ContainsKey(id)) particleRendererEnabledStates[id] = renderer.enabled;
                renderer.enabled = false;
            }
        } catch(Exception e) { Diag.Ignore(e); }
    }
    private static void RestoreParticleSystemTree(GameObject particleObject) {
        if(particleObject == null) return;
        if(!HasParticleTweakState) return;
        try {
            GameObject[] objects = CollectGameObjects(particleObject);
            for(int i = 0; i < objects.Length; i++) {
                RestoreActiveState(objects[i]);
            }
        } catch(Exception e) {
            Diag.Ignore(e);
            RestoreActiveState(particleObject);
        }
        try {
            ParticleSystem[] particles = particleObject.GetComponentsInChildren<ParticleSystem>(true);
            for(int i = 0; i < particles.Length; i++) {
                RestoreParticleSystemSettings(particles[i]);
            }
        } catch(Exception e) { Diag.Ignore(e); }
        try {
            Renderer[] renderers = particleObject.GetComponentsInChildren<Renderer>(true);
            for(int i = 0; i < renderers.Length; i++) {
                Renderer renderer = renderers[i];
                if(renderer == null) continue;
                int id = renderer.GetInstanceID();
                if(!particleRendererEnabledStates.TryGetValue(id, out bool wasEnabled)) continue;
                renderer.enabled = wasEnabled;
                particleRendererEnabledStates.Remove(id);
            }
        } catch(Exception e) { Diag.Ignore(e); }
    }
    private static void RestoreParticleSystemSettings(ParticleSystem particles) {
        if(particles == null) return;
        int id = particles.GetInstanceID();
        try {
            ParticleSystem.EmissionModule emission = particles.emission;
            if(particleEmissionEnabledStates.TryGetValue(id, out bool wasEmissionEnabled)) {
                emission.enabled = wasEmissionEnabled;
                particleEmissionEnabledStates.Remove(id);
            }
            if(particleEmissionRateStates.TryGetValue(id, out ParticleSystem.MinMaxCurve rate)) {
                emission.rateOverTime = rate;
                particleEmissionRateStates.Remove(id);
            }
        } catch(Exception e) { Diag.Ignore(e); }
        try {
            if(particleMaxParticleStates.TryGetValue(id, out int maxParticles)) {
                ParticleSystem.MainModule main = particles.main;
                main.maxParticles = maxParticles;
                particleMaxParticleStates.Remove(id);
            }
        } catch(Exception e) { Diag.Ignore(e); }
    }
    private static GameObject[] CollectGameObjects(GameObject root) {
        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
        GameObject[] objects = new GameObject[transforms.Length];
        for(int i = 0; i < transforms.Length; i++) {
            objects[i] = transforms[i].gameObject;
        }
        return objects;
    }
    private static void RememberActiveState(GameObject obj) {
        if(obj == null) return;
        int id = obj.GetInstanceID();
        if(!particleActiveStates.ContainsKey(id)) particleActiveStates[id] = obj.activeSelf;
    }
    private static void RestoreActiveState(GameObject obj) {
        if(obj == null) return;
        int id = obj.GetInstanceID();
        if(!particleActiveStates.TryGetValue(id, out bool wasActive)) return;
        try { obj.SetActive(wasActive); } catch(Exception e) { Diag.Ignore(e); }
        particleActiveStates.Remove(id);
    }
}

using Quartz.Core;
using Quartz.IO;
using UnityEngine;
namespace Quartz.Features.TileArc;
public static partial class TileArc {
    private const float Angle5 = 0.08726646f;
    private const float MinOverriddenDeg = 89.9f;
    private const float MaxOverriddenDeg = 170f;
    public static SettingsFile<TileArcSettings> ConfMgr { get; private set; }
    public static TileArcSettings Conf => ConfMgr?.Data;
    public static void EnsureConf() => ConfMgr ??= SettingsFile<TileArcSettings>.Loaded("TileArc.json");
    public static void Save() => ConfMgr?.RequestSave();
    internal static bool Enabled {
        get {
            EnsureConf();
            return MainCore.IsModEnabled && Conf.Enabled;
        }
    }
    public static void Refresh() {
        ClearMeshCache();
        RebuildLiveMeshes();
    }
    public static void ClearMeshCache() {
        try { FloorMesh.cache?.Clear(); } catch(Exception e) { Diag.Ignore(e); }
    }
    public static void RebuildLiveMeshes() {
        FloorMesh[] meshes;
        try {
            meshes = UnityEngine.Object.FindObjectsByType<FloorMesh>(FindObjectsSortMode.None);
        } catch(Exception e) {
            Diag.Ignore(e);
            return;
        }
        if(meshes == null) return;
        for(int i = 0; i < meshes.Length; i++) {
            FloorMesh mesh = meshes[i];
            if(mesh == null) continue;
            try { mesh.UpdateMesh(); } catch(Exception e) { Diag.Ignore(e); }
        }
    }
    internal static float ArcGate(float vanillaThreshold) => Enabled ? Mathf.PI : vanillaThreshold;
    internal static float ApplyCornerArcOverride(float original, float angleA, float angleB) {
        if(!Enabled) return original;
        float intensity = Mathf.Clamp01(Conf.Intensity);
        if(intensity <= 0f) return original;
        float minDiff = Mathf.Abs(Mathf.DeltaAngle(angleA * Mathf.Rad2Deg, angleB * Mathf.Rad2Deg)) * Mathf.Deg2Rad;
        float minDiffDeg = minDiff * Mathf.Rad2Deg;
        if(minDiffDeg < MinOverriddenDeg || minDiffDeg > MaxOverriddenDeg) return original;
        return ShortAngleForCornerRadius(intensity);
    }
    private static float ShortAngleForCornerRadius(float radiusFraction) {
        if(radiusFraction >= 1f) return Angle5;
        if(radiusFraction > 0.83f) {
            float t = (1f - radiusFraction) / 0.17f;
            return Angle5 + t * t * (Mathf.PI * 5f / 36f);
        }
        if(radiusFraction > 0.77f)
            return Mathf.PI / 6f + (0.83f - radiusFraction) / 0.06f * (Mathf.PI / 12f);
        if(radiusFraction > 0.15f) {
            float t = Mathf.Pow((0.77f - radiusFraction) / 0.62f, 1f / 0.7f);
            return Mathf.PI / 4f + t * (Mathf.PI / 4f);
        }
        float u = (0.15f - radiusFraction) / 0.15f;
        return Mathf.PI / 2f + u * u * (Mathf.PI / 6f);
    }
}

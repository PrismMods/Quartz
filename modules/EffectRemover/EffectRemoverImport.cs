using Quartz.Interop;
namespace Quartz.Features.EffectRemover;
public sealed class EffectRemoverImport : IImportHandler {
    public int Apply(ImportSource source) => source.Kind switch {
        ImportSourceKind.KorenResourcePackV1 => ApplyV1(source),
        ImportSourceKind.EnhancedEffectRemover => ApplyEnhanced(source),
        _ => 0,
    };
    private static int ApplyV1(ImportSource source) {
        EffectRemover.EnsureConf();
        EffectRemoverSettings c = EffectRemover.Conf;
        int count = 0;
        if(source.TryBool("EffectRemoverOn", out bool on)) {
            c.On = on;
            count++;
        }
        if(source.TryFloat("EffectRemoverCameraZoomScale", out float zoom)) {
            c.CameraZoomScale = zoom;
            count++;
        }
        void Flag(string name, Action<bool> set) {
            if(!source.TryBool(name, out bool value)) return;
            set(value);
            count++;
        }
        Flag("EffectRemoverEnableSave", v => c.EnableSave = v);
        Flag("EffectRemoverResetTrackAnimation", v => c.ResetTrackAnimation = v);
        Flag("EffectRemoverResetTrackColor", v => c.ResetTrackColor = v);
        Flag("EffectRemoverRemoveAllDecorations", v => c.RemoveAllDecorations = v);
        Flag("EffectRemoverResetTrackOpacity", v => c.LimitTrackOpacity = v);
        Flag("EffectRemoverSetCameraZoom", v => c.SetCameraZoom = v);
        Flag("EffectRemoverFilters", v => c.Filters = v);
        Flag("EffectRemoverAdvancedFilters", v => c.AdvancedFilters = v);
        Flag("EffectRemoverParticles", v => c.Particles = v);
        Flag("EffectRemoverDecorations", v => c.Decorations = v);
        Flag("EffectRemoverBackgrounds", v => c.Backgrounds = v);
        Flag("EffectRemoverCameras", v => c.Cameras = v);
        Flag("EffectRemoverRepeatEvents", v => c.RepeatEvents = v);
        Flag("EffectRemoverFrameRate", v => c.FrameRate = v);
        Flag("EffectRemoverHitSounds", v => c.HitSounds = v);
        Flag("EffectRemoverPlanetOrbit", v => c.PlanetOrbit = v);
        Flag("EffectRemoverPlanetScale", v => c.PlanetScale = v);
        Flag("EffectRemoverPlanetRadius", v => c.PlanetRadius = v);
        Flag("EffectRemoverTrackAnimations", v => c.TrackAnimations = v);
        Flag("EffectRemoverTrackPositions", v => c.TrackPositions = v);
        Flag("EffectRemoverTrackMoves", v => c.TrackMoves = v);
        Flag("EffectRemoverTrackColors", v => c.TrackColors = v);
        Flag("EffectRemoverHoldSounds", v => c.HoldSounds = v);
        Flag("EffectRemoverHideIcons", v => c.HideIcons = v);
        return count;
    }
    private static int ApplyEnhanced(ImportSource source) {
        EffectRemover.EnsureConf();
        EffectRemoverSettings c = EffectRemover.Conf;
        int count = 1;
        c.On = true;
        void Flag(string name, Action<bool> set) {
            if(!source.TryBool(name, out bool value)) return;
            set(value);
            count++;
        }
        if(source.TryFloat("CameraZoomScale", out float zoom)) {
            c.CameraZoomScale = zoom;
            count++;
        }
        Flag("EnableSave", v => c.EnableSave = v);
        Flag("ResetTrackAnimation", v => c.ResetTrackAnimation = v);
        Flag("ResetTrackColor", v => c.ResetTrackColor = v);
        Flag("RemoveAllDecorations", v => c.RemoveAllDecorations = v);
        Flag("ResetTrackOpacity", v => c.LimitTrackOpacity = v);
        Flag("SetCameraZoomScale", v => c.SetCameraZoom = v);
        Flag("Filters", v => c.Filters = v);
        Flag("AdvFilters", v => c.AdvancedFilters = v);
        Flag("Particles", v => c.Particles = v);
        Flag("Decorations", v => c.Decorations = v);
        Flag("Backgrounds", v => c.Backgrounds = v);
        Flag("Cameras", v => c.Cameras = v);
        Flag("RepeatEvents", v => c.RepeatEvents = v);
        Flag("FrameRate", v => c.FrameRate = v);
        Flag("HitSounds", v => c.HitSounds = v);
        Flag("PlanetOrbit", v => c.PlanetOrbit = v);
        Flag("PlanetScale", v => c.PlanetScale = v);
        Flag("PlanetRadius", v => c.PlanetRadius = v);
        Flag("TrackAnimations", v => c.TrackAnimations = v);
        Flag("TrackPos", v => c.TrackPositions = v);
        Flag("TrackMove", v => c.TrackMoves = v);
        Flag("TrackColors", v => c.TrackColors = v);
        Flag("HoldSounds", v => c.HoldSounds = v);
        Flag("HideIcons", v => c.HideIcons = v);
        return count;
    }
    public void Refresh() {
        EffectRemover.EnsureConf();
        EffectRemover.RefreshEditorSaveButtons();
        EffectRemover.Save();
    }
}

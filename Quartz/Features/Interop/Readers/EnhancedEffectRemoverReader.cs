using Quartz.Features.EffectRemover;
using Newtonsoft.Json.Linq;
using static Quartz.Features.Interop.ReflectionHelpers;

namespace Quartz.Features.Interop.Readers;

// ===== EnhancedEffectRemover =====
internal static class EnhancedEffectRemoverReader {
    public static int ImportEnhancedEffectRemover(SettingsImportOption option) {
        int count = 0;
        object settings = GetStaticMember(SettingsImporter.FindType(option, "EnhancedEffectRemover.Settings"), "Instance");
        if(settings != null) count += ApplyEffectRemover(name => GetMemberValue(settings, name));

        if(count == 0) {
            string json = ReadFirstText([Path.Combine(option.Directory ?? "", "Settings.json")]);
            if(!string.IsNullOrEmpty(json)) {
                try {
                    JObject root = JObject.Parse(json);
                    count += ApplyEffectRemover(name =>
                        root.TryGetValue(name, StringComparison.OrdinalIgnoreCase, out JToken t) ? t : null);
                } catch { }
            }
        }
        return count;
    }

    // Shared mapping for both the runtime object and the JSON file: `get`
    // returns a raw value (boxed CLR value or JToken) for a source field name.
    private static int ApplyEffectRemover(Func<string, object> get) {
        Features.EffectRemover.EffectRemover.EnsureConf();
        EffectRemoverSettings c = Features.EffectRemover.EffectRemover.Conf;
        int count = 0;

        c.On = true;
        count++;

        void Flag(string srcName, Action<bool> set) {
            if(TryConvertBool(get(srcName), out bool v)) { set(v); count++; }
        }

        if(TryConvertFloat(get("CameraZoomScale"), out float zoom)) { c.CameraZoomScale = zoom; count++; }
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
        if(TryConvertBool(get("CheckPoints"), out bool cp)) {
            Features.Tweaks.Tweaks.EnsureConf();
            Features.Tweaks.Tweaks.Conf.RemoveAllCheckpoints = cp;
            count++;
        }
        return count;
    }
}

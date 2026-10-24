using Quartz.Core;
using Quartz.Interop;
namespace Quartz.Features.UiHider;
public sealed class UiHiderImport : IImportHandler {
    public int Apply(ImportSource source) {
        if(source.Kind is not (ImportSourceKind.AdofaiTweaks or ImportSourceKind.KorenResourcePackV1)) return 0;
        bool hasPlaying = source.TryExtra(ImportKeys.UiHiderPlayingProfile, out Func<string, bool?> playing);
        bool hasRecording = source.TryExtra(ImportKeys.UiHiderRecordingProfile, out Func<string, bool?> recording);
        bool hasShortcut = source.TryExtra(ImportKeys.UiHiderShortcut, out Func<string, bool> shortcut);
        bool hasShortcutKey = source.TryExtra(ImportKeys.UiHiderShortcutKey, out int shortcutKey);
        bool hasRecordingMode = source.TryExtra(ImportKeys.UiHiderRecordingMode, out bool recordingMode);
        bool hasUseShortcut = source.TryExtra(ImportKeys.UiHiderUseShortcut, out bool useShortcut);
        bool hasEnabled = source.TryExtra(ImportKeys.UiHiderEnabled, out bool enabled);
        if(!hasPlaying && !hasRecording && !hasShortcut && !hasShortcutKey
            && !hasRecordingMode && !hasUseShortcut && !hasEnabled) return 0;
        UiHider.EnsureConf();
        int count = 0;
        count += ApplyProfile(playing, UiHider.Conf.Playing);
        count += ApplyProfile(recording, UiHider.Conf.Recording);
        if(hasRecordingMode) {
            UiHider.Conf.RecordingMode = recordingMode;
            count++;
        }
        if(hasUseShortcut) {
            UiHider.Conf.UseShortcut = useShortcut;
            count++;
        }
        if(hasShortcut) UiHider.Conf.ShortcutModifier = (int)Modifier(shortcut);
        if(hasShortcutKey) {
            UiHider.Conf.ShortcutKey = shortcutKey;
            count++;
        }
        if(hasEnabled) {
            UiHider.Conf.Enabled = enabled;
            count++;
        } else if(count > 0) {
            UiHider.Conf.Enabled = true;
            count++;
        }
        return count;
    }
    private static Keybind.KeyModifier Modifier(Func<string, bool> pressed) =>
        pressed("PressCtrl") ? Keybind.KeyModifier.Ctrl
        : pressed("PressAlt") ? Keybind.KeyModifier.Alt
        : pressed("PressShift") ? Keybind.KeyModifier.Shift
        : Keybind.KeyModifier.None;
    private static int ApplyProfile(Func<string, bool?> read, UiHiderProfile target) {
        if(read == null || target == null) return 0;
        int count = 0;
        void Flag(string name, Action<bool> set) {
            if(read(name) is not { } value) return;
            set(value);
            count++;
        }
        Flag("HideEverything", v => target.HideEverything = v);
        Flag("HideJudgment", v => target.HideJudgment = v);
        Flag("HideMissIndicators", v => target.HideMissIndicators = v);
        Flag("HideTitle", v => target.HideTitle = v);
        Flag("HideOtto", v => target.HideOtto = v);
        Flag("HideTimingTarget", v => target.HideTimingTarget = v);
        Flag("HideNoFailIcon", v => target.HideNoFailIcon = v);
        Flag("HideBeta", v => target.HideBeta = v);
        Flag("HideResult", v => target.HideResult = v);
        Flag("HideHitErrorMeter", v => target.HideHitErrorMeter = v);
        Flag("HideLastFloorFlash", v => target.HideLastFloorFlash = v);
        return count;
    }
    public void Refresh() {
        UiHider.EnsureConf();
        UiHider.ApplyNow();
        UiHider.Save();
    }
}

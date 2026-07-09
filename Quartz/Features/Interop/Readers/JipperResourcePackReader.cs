using Quartz.Features.Combo;
using Quartz.Features.Judgement;
using Quartz.Features.ProgressBar;
using UnityEngine;
using static Quartz.Features.Interop.ReflectionHelpers;
using static Quartz.Features.Interop.Readers.KeyViewerImportShared;
namespace Quartz.Features.Interop.Readers;
internal static class JipperResourcePackReader {
    public static int ImportJipperResourcePack(
        SettingsImportOption option,
        SettingsImportReplaceMode mode,
        SettingsImportKeyViewerPart parts
    ) {
        int count = 0;
        count += ImportJrpProgressBar(option);
        count += ImportJrpCombo(option);
        count += ImportJrpJudgement(option);
        count += ImportJrpResourceChanger(option);
        count += ImportJrpKeyViewer(option, mode, parts);
        return count;
    }
    private static int ImportJrpProgressBar(SettingsImportOption option) {
        object settings = GetStaticMember(SettingsImporter.FindType(option, "JipperResourcePack.OverlayContents.Status"), "Settings")
            ?? GetStaticMember(SettingsImporter.FindType(option, "JipperResourcePack.Jongyeol.JStatus"), "Settings");
        if(settings == null) return 0;
        if(!TryGetBool(settings, "ShowProgressBar", out bool barOn)) return 0;
        int count = 0;
        ProgressBarOverlay.EnsureConf();
        ProgressBarOverlay.Conf.Enabled = barOn;
        count++;
        if(TryGetColorRangeEndpoints(GetMemberValue(settings, "ProgressBarColor"), out Color fill, out _)) {
            ProgressBarOverlay.Conf.SetFillColor(fill);
            count++;
        }
        if(TryGetColorRangeEndpoints(GetMemberValue(settings, "ProgressBarBackgroundColor"), out Color back, out _)) {
            ProgressBarOverlay.Conf.SetBackColor(back);
            count++;
        }
        if(TryGetColorRangeEndpoints(GetMemberValue(settings, "ProgressBarBorderColor"), out Color border, out _)) {
            ProgressBarOverlay.Conf.SetOutlineColor(border);
            count++;
        }
        return count;
    }
    private static int ImportJrpCombo(SettingsImportOption option) {
        object settings = GetStaticMember(SettingsImporter.FindType(option, "JipperResourcePack.OverlayContents.Combo"), "Settings")
            ?? GetStaticMember(SettingsImporter.FindType(option, "JipperResourcePack.Jongyeol.JCombo"), "Settings");
        if(settings == null) return 0;
        int count = 0;
        ComboOverlay.EnsureConf();
        ComboOverlay.Conf.Enabled = true;
        count++;
        if(TryGetBool(settings, "EnableAutoCombo", out bool auto)) {
            ComboOverlay.Conf.CountAuto = auto;
            count++;
        }
        if(TryGetInt(settings, "ComboColorMax", out int colorMax)) {
            ComboOverlay.Conf.ColorMax = colorMax;
            count++;
        }
        if(TryGetColorRangeEndpoints(GetMemberValue(settings, "ComboColor"), out Color low, out Color high)) {
            ComboOverlay.Conf.SetColorLow(low);
            ComboOverlay.Conf.SetColorHigh(high);
            count++;
        }
        return count;
    }
    private static int ImportJrpJudgement(SettingsImportOption option) {
        object settings = GetStaticMember(SettingsImporter.FindType(option, "JipperResourcePack.OverlayContents.Judgement"), "Settings");
        if(settings == null) return 0;
        int count = 0;
        JudgementOverlay.EnsureConf();
        JudgementOverlay.Conf.Enabled = true;
        count++;
        if(TryGetBool(settings, "LocationUp", out bool up)) {
            JudgementOverlay.Conf.OffsetY = up ? 90f : 0f;
            count++;
        }
        return count;
    }
    private static int ImportJrpResourceChanger(SettingsImportOption option) {
        object settings = GetStaticMember(SettingsImporter.FindType(option, "JipperResourcePack.ResourceChanger"), "_settings");
        if(settings == null) return 0;
        int count = 0;
        if(TryGetBool(settings, "ChangeRabbit", out bool otto)) {
            Features.OttoIcon.OttoIcon.EnsureConf();
            Features.OttoIcon.OttoIcon.Conf.Enabled = otto;
            count++;
        }
        if(TryGetBool(settings, "ChangeBallColor", out bool ball)) {
            Features.PlanetColors.PlanetColors.EnsureConf();
            Features.PlanetColors.PlanetColors.Conf.Enabled = ball;
            count++;
        }
        return count;
    }
    private static int ImportJrpKeyViewer(
        SettingsImportOption option,
        SettingsImportReplaceMode mode,
        SettingsImportKeyViewerPart parts
    ) {
        if(mode == SettingsImportReplaceMode.KeepOld) return 0;
        object settings = GetStaticMember(SettingsImporter.FindType(option, "JipperResourcePack.KeyViewerContents.KeyViewer"), "Settings");
        if(settings == null) return 0;
        ImportedKeyViewer imported = ReadKeyViewerFromObject(settings);
        if(imported == null || imported.Available == SettingsImportKeyViewerPart.None) return 0;
        return ApplyKeyViewerImport(imported, mode, parts);
    }
}

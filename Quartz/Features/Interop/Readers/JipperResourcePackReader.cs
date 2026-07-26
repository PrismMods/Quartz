using Quartz.Interop;
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
        ImportSource source = new(ImportSourceKind.JipperResourcePack, name => GetMemberValue(settings, name));
        source.Put(ImportKeys.ProgressBarEnabled, barOn);
        if(TryGetColorRangeEndpoints(GetMemberValue(settings, "ProgressBarColor"), out Color fill, out _))
            source.Put(ImportKeys.ProgressBarFill, fill);
        if(TryGetColorRangeEndpoints(GetMemberValue(settings, "ProgressBarBackgroundColor"), out Color back, out _))
            source.Put(ImportKeys.ProgressBarBack, back);
        if(TryGetColorRangeEndpoints(GetMemberValue(settings, "ProgressBarBorderColor"), out Color border, out _))
            source.Put(ImportKeys.ProgressBarBorder, border);
        return ImportRegistry.Deliver(source);
    }
    private static int ImportJrpCombo(SettingsImportOption option) {
        object settings = GetStaticMember(SettingsImporter.FindType(option, "JipperResourcePack.OverlayContents.Combo"), "Settings")
            ?? GetStaticMember(SettingsImporter.FindType(option, "JipperResourcePack.Jongyeol.JCombo"), "Settings");
        if(settings == null) return 0;
        ImportSource source = new(ImportSourceKind.JipperResourcePack, name => GetMemberValue(settings, name));
        source.Put(ImportKeys.ComboEnabled, true);
        if(TryGetBool(settings, "EnableAutoCombo", out bool auto)) source.Put(ImportKeys.ComboCountAuto, auto);
        if(TryGetInt(settings, "ComboColorMax", out int colorMax)) source.Put(ImportKeys.ComboColorMax, colorMax);
        if(TryGetColorRangeEndpoints(GetMemberValue(settings, "ComboColor"), out Color low, out Color high)) {
            source.Put(ImportKeys.ComboColorLow, low);
            source.Put(ImportKeys.ComboColorHigh, high);
        }
        return ImportRegistry.Deliver(source);
    }
    private static int ImportJrpJudgement(SettingsImportOption option) {
        object settings = GetStaticMember(SettingsImporter.FindType(option, "JipperResourcePack.OverlayContents.Judgement"), "Settings");
        if(settings == null) return 0;
        ImportSource source = new(ImportSourceKind.JipperResourcePack, name => GetMemberValue(settings, name));
        source.Put(ImportKeys.JudgementEnabled, true);
        if(TryGetBool(settings, "LocationUp", out bool up)) source.Put(ImportKeys.JudgementOffsetY, up ? 90f : 0f);
        return ImportRegistry.Deliver(source);
    }
    private static int ImportJrpResourceChanger(SettingsImportOption option) {
        object settings = GetStaticMember(SettingsImporter.FindType(option, "JipperResourcePack.ResourceChanger"), "_settings");
        if(settings == null) return 0;
        int count = ImportRegistry.Deliver(
            new ImportSource(ImportSourceKind.JipperResourcePack, name => GetMemberValue(settings, name)));
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
        return DeliverKeyViewer(imported, mode, parts);
    }
}

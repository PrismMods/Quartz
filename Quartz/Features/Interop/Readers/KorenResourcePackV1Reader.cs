using System.Collections;
using System.Xml.Linq;
using Quartz.Core;
using Quartz.Interop;
using UnityEngine;
using static Quartz.Features.Interop.ReflectionHelpers;
using static Quartz.Features.Interop.Readers.KeyViewerImportShared;
namespace Quartz.Features.Interop.Readers;
internal static class KorenResourcePackV1Reader {
    private sealed class V1Reader {
        public Func<string, object> Scalar;
        public Func<string, int[]> Keys;
        public Func<string, string[]> Labels;
    }
    public static int ImportKorenResourcePackV1(
        SettingsImportOption option,
        SettingsImportReplaceMode keyViewerMode,
        SettingsImportKeyViewerPart keyViewerParts
    ) {
        int count = 0;
        object live = GetStaticMember(SettingsImporter.FindType(option, "KorenResourcePack.Main"), "settings");
        if(live != null) {
            count += ApplyV1Common(V1FromObject(live), keyViewerMode, keyViewerParts);
            count += ImportV1UiHider(live);
        }
        if(count == 0) {
            XDocument doc = LoadXml(option, "Settings.xml");
            if(doc?.Root != null) count += ApplyV1Common(V1FromXml(doc.Root), keyViewerMode, keyViewerParts);
        }
        return count;
    }
    private static V1Reader V1FromObject(object live) => new() {
        Scalar = name => GetMemberValue(live, name),
        Keys = name => ReadPositionalKeys(GetMemberValue(live, name)),
        Labels = name => ReadStringArray(GetMemberValue(live, name)),
    };
    private static V1Reader V1FromXml(XElement root) => new() {
        Scalar = name => FindFirstDescendant(root, name)?.Value,
        Keys = name => ReadPositionalKeysXml(root, name),
        Labels = name => ReadPositionalLabelsXml(root, name),
    };
    private static int ApplyV1Common(
        V1Reader r,
        SettingsImportReplaceMode keyViewerMode,
        SettingsImportKeyViewerPart keyViewerParts
    ) {
        int count = 0;
        ImportedKeyViewer kv = ReadKeyViewerFromV1(r);
        if(kv.Available != SettingsImportKeyViewerPart.None) {
            count += DeliverKeyViewer(kv, keyViewerMode, keyViewerParts);
        }
        count += ImportRegistry.Deliver(new ImportSource(ImportSourceKind.KorenResourcePackV1, r.Scalar, r.Keys, r.Labels));
        return count;
    }
    private static int ImportV1UiHider(object live) {
        ImportSource source = new(ImportSourceKind.KorenResourcePackV1, name => GetMemberValue(live, name));
        if(GetMemberValue(live, "UiHidingPlayingProfile") is { } playing)
            source.Put(ImportKeys.UiHiderPlayingProfile,
                (Func<string, bool?>)(name => TryGetBool(playing, name, out bool v) ? v : null));
        if(GetMemberValue(live, "UiHidingRecordingProfile") is { } recording)
            source.Put(ImportKeys.UiHiderRecordingProfile,
                (Func<string, bool?>)(name => TryGetBool(recording, name, out bool v) ? v : null));
        if(TryGetBool(live, "UiHidingRecordingMode", out bool rec)) source.Put(ImportKeys.UiHiderRecordingMode, rec);
        if(TryGetBool(live, "UiHidingUseRecordingModeShortcut", out bool useShortcut))
            source.Put(ImportKeys.UiHiderUseShortcut, useShortcut);
        source.Put(ImportKeys.UiHiderShortcut, (Func<string, bool>)(name => name switch {
            "PressCtrl" => TryGetBool(live, "UiHidingShortcutCtrl", out bool ctrl) && ctrl,
            "PressAlt" => TryGetBool(live, "UiHidingShortcutAlt", out bool alt) && alt,
            "PressShift" => TryGetBool(live, "UiHidingShortcutShift", out bool shift) && shift,
            _ => false,
        }));
        if(TryGetInt(live, "UiHidingShortcutKey", out int key))
            source.Put(ImportKeys.UiHiderShortcutKey, NormalizeKeyInt(key));
        if(TryGetBool(live, "UiHidingOn", out bool on)) source.Put(ImportKeys.UiHiderEnabled, on);
        return ImportRegistry.Deliver(source);
    }
    private static ImportedKeyViewer ReadKeyViewerFromV1(V1Reader r) {
        ImportedKeyViewer kv = new();
        if(TryConvertInt(r.Scalar("KeyViewerSimpleStyle"), out int style)) { kv.HasStyle = true; kv.Style = style; }
        kv.Key10 = r.Keys("KeyViewerSimpleKey10");
        kv.Key12 = r.Keys("KeyViewerSimpleKey12");
        kv.Key16 = r.Keys("KeyViewerSimpleKey16");
        kv.Key20 = r.Keys("KeyViewerSimpleKey20");
        if(TryConvertInt(r.Scalar("KeyViewerSimpleFootStyle"), out int footStyle)) {
            kv.HasFoot = true;
            kv.FootStyle = footStyle == 5 ? 8 : Mathf.Clamp(footStyle, 0, 4);
            string footField = footStyle switch {
                1 => "KeyViewerSimpleFootKey2",
                2 => "KeyViewerSimpleFootKey4",
                3 => "KeyViewerSimpleFootKey6",
                4 => "KeyViewerSimpleFootKey8",
                5 => "KeyViewerSimpleFootKey16",
                _ => null,
            };
            kv.FootKeys = footField == null ? null : r.Keys(footField);
        }
        kv.GhostKey10 = r.Keys("KeyViewerSimpleGhostKey10");
        kv.GhostKey12 = r.Keys("KeyViewerSimpleGhostKey12");
        kv.GhostKey16 = r.Keys("KeyViewerSimpleGhostKey16");
        kv.GhostKey20 = r.Keys("KeyViewerSimpleGhostKey20");
        if(kv.HasStyle || AnyKeys(kv) || kv.HasFoot
            || AnyGhost(kv.GhostKey10, kv.GhostKey12, kv.GhostKey16, kv.GhostKey20)) {
            kv.Available |= SettingsImportKeyViewerPart.KeysLayout;
        }
        kv.Key10Text = r.Labels("KeyViewerSimpleKey10Text");
        kv.Key12Text = r.Labels("KeyViewerSimpleKey12Text");
        kv.Key16Text = r.Labels("KeyViewerSimpleKey16Text");
        kv.Key20Text = r.Labels("KeyViewerSimpleKey20Text");
        if(AnyLabels(kv)) {
            kv.Available |= SettingsImportKeyViewerPart.Labels;
        }
        kv.Bg = V1Color(r, "SKvBg");
        kv.BgClicked = V1Color(r, "SKvBgc");
        kv.Outline = V1Color(r, "SKvOut");
        kv.OutlineClicked = V1Color(r, "SKvOutc");
        kv.Text = V1Color(r, "SKvTxt");
        kv.TextClicked = V1Color(r, "SKvTxtc");
        kv.Rain = V1Color(r, "SKvRain");
        kv.Rain2 = V1Color(r, "SKvRain2");
        kv.Rain3 = V1Color(r, "SKvRain3");
        kv.GhostRain = V1Color(r, "SKvGhostRain");
        if(AnyColors(kv) || kv.GhostRain != null) {
            kv.Available |= SettingsImportKeyViewerPart.Colors;
        }
        if(TryConvertBool(r.Scalar("KeyViewerSimpleUseRain"), out bool useRain)) { kv.HasRainEnabled = true; kv.RainEnabled = useRain; }
        if(TryConvertFloat(r.Scalar("KeyViewerSimpleRainSpeed"), out float rainSpeed)) { kv.HasRainSpeed = true; kv.RainSpeed = rainSpeed; }
        if(TryConvertFloat(r.Scalar("KeyViewerSimpleRainHeight"), out float rainHeight)) { kv.HasRainHeight = true; kv.RainHeight = rainHeight; }
        if(kv.HasRainEnabled || kv.HasRainSpeed || kv.HasRainHeight) {
            kv.Available |= SettingsImportKeyViewerPart.Rain;
        }
        if(TryConvertFloat(r.Scalar("KeyViewerSimpleSize"), out float size)) { kv.HasSize = true; kv.Size = Mathf.Clamp(size, 0.1f, 3f); }
        if(kv.HasSize) {
            kv.Available |= SettingsImportKeyViewerPart.PositionSize;
        }
        if(TryConvertBool(r.Scalar("keyViewerOn"), out bool en)) { kv.HasEnabled = true; kv.Enabled = en; }
        if(TryConvertBool(r.Scalar("KeyViewerSimpleSyncToKeyLimiter"), out bool sync)) { kv.HasSync = true; kv.SyncToKeyLimiter = sync; }
        return kv;
    }
    private static bool AnyGhost(params int[][] arrays) {
        foreach(int[] arr in arrays) {
            if(arr != null && arr.Any(k => k != 0)) return true;
        }
        return false;
    }
    private static Color? V1Color(V1Reader r, string prefix) {
        if(TryConvertFloat(r.Scalar(prefix + "R"), out float cr)
            && TryConvertFloat(r.Scalar(prefix + "G"), out float cg)
            && TryConvertFloat(r.Scalar(prefix + "B"), out float cb)) {
            float a = TryConvertFloat(r.Scalar(prefix + "A"), out float ca) ? ca : 1f;
            return new Color(Mathf.Clamp01(cr), Mathf.Clamp01(cg), Mathf.Clamp01(cb), Mathf.Clamp01(a));
        }
        return null;
    }
    private static int[] ReadPositionalKeys(object value) {
        if(value is not IEnumerable enumerable || value is string) return null;
        List<int> result = [];
        foreach(object item in enumerable) {
            result.Add(TryConvertKeyCode(item, out int key) ? key : 0);
        }
        return result.Count > 0 ? [.. result] : null;
    }
    private static int[] ReadPositionalKeysXml(XElement root, string name) {
        XElement list = FindFirstDescendant(root, name);
        if(list == null) return null;
        List<int> result = [];
        foreach(XElement item in list.Elements()) {
            result.Add(TryConvertKeyCode(item.Value, out int key) ? key : 0);
        }
        return result.Count > 0 ? [.. result] : null;
    }
    private static string[] ReadPositionalLabelsXml(XElement root, string name) {
        XElement list = FindFirstDescendant(root, name);
        if(list == null) return null;
        XNamespace xsi = "http://www.w3.org/2001/XMLSchema-instance";
        List<string> result = [];
        foreach(XElement item in list.Elements()) {
            bool nil = string.Equals((string)item.Attribute(xsi + "nil"), "true", StringComparison.OrdinalIgnoreCase);
            result.Add(nil ? "" : item.Value);
        }
        return result.Count > 0 ? [.. result] : null;
    }
}

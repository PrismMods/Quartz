using System.Globalization;
using Quartz.Features.KeyViewer;
using Quartz.Features.KeyViewer.Layout;
using Newtonsoft.Json.Linq;
using UnityEngine;
using static Quartz.Features.Interop.ReflectionHelpers;
namespace Quartz.Features.Interop.Readers;
internal sealed class ImportedKeyViewer {
    public SettingsImportKeyViewerPart Available;
    public bool HasStyle;
    public int Style;
    public int[] Key10, Key12, Key16, Key20;
    public string[] Key10Text, Key12Text, Key16Text, Key20Text;
    public Color? Bg, BgClicked, Outline, OutlineClicked, Text, TextClicked, Rain, Rain2, Rain3;
    public bool HasRainEnabled; public bool RainEnabled;
    public bool HasRainSpeed; public float RainSpeed;
    public bool HasRainHeight; public float RainHeight;
    public bool HasSize; public float Size;
    public bool HasEnabled; public bool Enabled;
    public bool HasSync; public bool SyncToKeyLimiter;
    public bool HasFoot; public int FootStyle; public int[] FootKeys;
    public int[] GhostKey10, GhostKey12, GhostKey16, GhostKey20;
    public Color? GhostRain;
}
internal static class KeyViewerImportShared {
    public static ImportedKeyViewer ReadKeyViewerFromObject(object src) {
        ImportedKeyViewer kv = new();
        if(TryParseKvStyle(GetMemberValue(src, "KeyViewerStyle"), out int style)) {
            kv.HasStyle = true;
            kv.Style = style;
        }
        kv.Key10 = ReadKeyCodesFromMember(src, "key10");
        kv.Key12 = ReadKeyCodesFromMember(src, "key12");
        kv.Key16 = ReadKeyCodesFromMember(src, "key16");
        kv.Key20 = ReadKeyCodesFromMember(src, "key20");
        if(kv.HasStyle || AnyKeys(kv)) {
            kv.Available |= SettingsImportKeyViewerPart.KeysLayout;
        }
        kv.Key10Text = ReadStringArray(GetMemberValue(src, "key10Text"));
        kv.Key12Text = ReadStringArray(GetMemberValue(src, "key12Text"));
        kv.Key16Text = ReadStringArray(GetMemberValue(src, "key16Text"));
        kv.Key20Text = ReadStringArray(GetMemberValue(src, "key20Text"));
        if(AnyLabels(kv)) {
            kv.Available |= SettingsImportKeyViewerPart.Labels;
        }
        kv.Bg = TryGetColor(GetMemberValue(src, "Background"), out Color bg) ? bg : null;
        kv.BgClicked = TryGetColor(GetMemberValue(src, "BackgroundClicked"), out Color bgc) ? bgc : null;
        kv.Outline = TryGetColor(GetMemberValue(src, "Outline"), out Color ol) ? ol : null;
        kv.OutlineClicked = TryGetColor(GetMemberValue(src, "OutlineClicked"), out Color olc) ? olc : null;
        kv.Text = TryGetColor(GetMemberValue(src, "Text"), out Color tx) ? tx : null;
        kv.TextClicked = TryGetColor(GetMemberValue(src, "TextClicked"), out Color txc) ? txc : null;
        kv.Rain = TryGetColor(GetMemberValue(src, "RainColor"), out Color rc) ? rc : null;
        kv.Rain2 = TryGetColor(GetMemberValue(src, "RainColor2"), out Color rc2) ? rc2 : null;
        kv.Rain3 = TryGetColor(GetMemberValue(src, "RainColor3"), out Color rc3) ? rc3 : null;
        if(AnyColors(kv)) {
            kv.Available |= SettingsImportKeyViewerPart.Colors;
        }
        if(TryGetBool(src, "useRain", out bool useRain)) { kv.HasRainEnabled = true; kv.RainEnabled = useRain; }
        if(TryGetFloat(src, "rainSpeed", out float rs)) { kv.HasRainSpeed = true; kv.RainSpeed = rs; }
        if(TryGetFloat(src, "rainHeight", out float rh)) { kv.HasRainHeight = true; kv.RainHeight = rh; }
        if(kv.HasRainEnabled || kv.HasRainSpeed || kv.HasRainHeight) {
            kv.Available |= SettingsImportKeyViewerPart.Rain;
        }
        if(TryGetFloat(src, "Size", out float size)) { kv.HasSize = true; kv.Size = Mathf.Clamp(size, 0.1f, 3f); }
        if(kv.HasSize) {
            kv.Available |= SettingsImportKeyViewerPart.PositionSize;
        }
        if(TryGetBool(src, "Enabled", out bool en)) { kv.HasEnabled = true; kv.Enabled = en; }
        if(TryGetBool(src, "SyncToKeyLimiter", out bool sync)) { kv.HasSync = true; kv.SyncToKeyLimiter = sync; }
        return kv;
    }
    public static ImportedKeyViewer ReadKeyViewerFromJson(JObject src) {
        if(src == null) return null;
        ImportedKeyViewer kv = new();
        if(TryParseKvStyle(JsonValue(src, "KeyViewerStyle"), out int style)) {
            kv.HasStyle = true;
            kv.Style = style;
        }
        kv.Key10 = ReadKeyCodesFromJson(src["key10"]);
        kv.Key12 = ReadKeyCodesFromJson(src["key12"]);
        kv.Key16 = ReadKeyCodesFromJson(src["key16"]);
        kv.Key20 = ReadKeyCodesFromJson(src["key20"]);
        if(kv.HasStyle || AnyKeys(kv)) {
            kv.Available |= SettingsImportKeyViewerPart.KeysLayout;
        }
        kv.Key10Text = ReadStringArrayJson(src["key10Text"]);
        kv.Key12Text = ReadStringArrayJson(src["key12Text"]);
        kv.Key16Text = ReadStringArrayJson(src["key16Text"]);
        kv.Key20Text = ReadStringArrayJson(src["key20Text"]);
        if(AnyLabels(kv)) {
            kv.Available |= SettingsImportKeyViewerPart.Labels;
        }
        kv.Bg = ReadJsonColor(src["Background"]);
        kv.BgClicked = ReadJsonColor(src["BackgroundClicked"]);
        kv.Outline = ReadJsonColor(src["Outline"]);
        kv.OutlineClicked = ReadJsonColor(src["OutlineClicked"]);
        kv.Text = ReadJsonColor(src["Text"]);
        kv.TextClicked = ReadJsonColor(src["TextClicked"]);
        kv.Rain = ReadJsonColor(src["RainColor"]);
        kv.Rain2 = ReadJsonColor(src["RainColor2"]);
        kv.Rain3 = ReadJsonColor(src["RainColor3"]);
        if(AnyColors(kv)) {
            kv.Available |= SettingsImportKeyViewerPart.Colors;
        }
        if(TryConvertBool(JsonValue(src, "useRain"), out bool useRain)) { kv.HasRainEnabled = true; kv.RainEnabled = useRain; }
        if(TryConvertFloat(JsonValue(src, "rainSpeed"), out float rs)) { kv.HasRainSpeed = true; kv.RainSpeed = rs; }
        if(TryConvertFloat(JsonValue(src, "rainHeight"), out float rh)) { kv.HasRainHeight = true; kv.RainHeight = rh; }
        if(kv.HasRainEnabled || kv.HasRainSpeed || kv.HasRainHeight) {
            kv.Available |= SettingsImportKeyViewerPart.Rain;
        }
        if(TryConvertFloat(JsonValue(src, "Size"), out float size)) { kv.HasSize = true; kv.Size = Mathf.Clamp(size, 0.1f, 3f); }
        if(kv.HasSize) {
            kv.Available |= SettingsImportKeyViewerPart.PositionSize;
        }
        if(TryConvertBool(JsonValue(src, "Enabled"), out bool en)) { kv.HasEnabled = true; kv.Enabled = en; }
        if(TryConvertBool(JsonValue(src, "SyncToKeyLimiter"), out bool sync)) { kv.HasSync = true; kv.SyncToKeyLimiter = sync; }
        return kv;
    }
    public static int ApplyKeyViewerImport(
        ImportedKeyViewer kv,
        SettingsImportReplaceMode mode,
        SettingsImportKeyViewerPart parts
    ) {
        if(kv == null || mode == SettingsImportReplaceMode.KeepOld) return 0;
        SettingsImportKeyViewerPart effective = mode == SettingsImportReplaceMode.ReplaceCertain
            ? parts & kv.Available
            : kv.Available;
        if(effective == SettingsImportKeyViewerPart.None) return 0;
        KeyViewerOverlay.EnsureConf();
        KeyViewerSettings target = KeyViewerOverlay.Conf;
        target.Mode = KvMigrationPlan.LegacyModeSimple;
        int count = 0;
        if((effective & SettingsImportKeyViewerPart.KeysLayout) != 0) {
            if(kv.HasStyle) { target.Style = Mathf.Clamp(kv.Style, 0, 3); }
            if(kv.Key10 is { Length: 10 }) { target.Key10 = kv.Key10; }
            if(kv.Key12 is { Length: 12 }) { target.Key12 = kv.Key12; }
            if(kv.Key16 is { Length: 16 }) { target.Key16 = kv.Key16; }
            if(kv.Key20 is { Length: 20 }) { target.Key20 = kv.Key20; }
            if(kv.HasFoot) {
                target.FootStyle = Mathf.Clamp(kv.FootStyle, 0, KeyViewerSettings.MaxFootStyle);
                if(kv.FootKeys is { Length: > 0 }) {
                    int[] dest = target.FootKeysForStyle(target.FootStyle);
                    int n = Mathf.Min(kv.FootKeys.Length, dest.Length);
                    for(int i = 0; i < n; i++) { dest[i] = kv.FootKeys[i]; }
                }
            }
            if(kv.GhostKey10 is { Length: 10 }) { target.GhostKey10 = kv.GhostKey10; }
            if(kv.GhostKey12 is { Length: 12 }) { target.GhostKey12 = kv.GhostKey12; }
            if(kv.GhostKey16 is { Length: 16 }) { target.GhostKey16 = kv.GhostKey16; }
            if(kv.GhostKey20 is { Length: 20 }) { target.GhostKey20 = kv.GhostKey20; }
            count++;
        }
        if((effective & SettingsImportKeyViewerPart.Labels) != 0) {
            if(kv.Key10Text is { Length: 10 }) { target.Key10Text = kv.Key10Text; }
            if(kv.Key12Text is { Length: 12 }) { target.Key12Text = kv.Key12Text; }
            if(kv.Key16Text is { Length: 16 }) { target.Key16Text = kv.Key16Text; }
            if(kv.Key20Text is { Length: 20 }) { target.Key20Text = kv.Key20Text; }
            count++;
        }
        if((effective & SettingsImportKeyViewerPart.Colors) != 0) {
            if(kv.Bg is { } bg) { target.SetBg(bg); }
            if(kv.BgClicked is { } bgc) { target.SetBgPressed(bgc); }
            if(kv.Outline is { } ol) { target.SetOutline(ol); }
            if(kv.OutlineClicked is { } olc) { target.SetOutlinePressed(olc); }
            if(kv.Text is { } tx) { target.SetText(tx); }
            if(kv.TextClicked is { } txc) { target.SetTextPressed(txc); }
            if(kv.Rain is { } rc) { target.SetRain(rc); }
            if(kv.Rain2 is { } rc2) { target.SetRain2(rc2); }
            if(kv.Rain3 is { } rc3) { target.SetRain3(rc3); }
            if(kv.GhostRain is { } gr) { target.SetGhostRain(gr); }
            count++;
        }
        if((effective & SettingsImportKeyViewerPart.Rain) != 0) {
            if(kv.HasRainEnabled) { target.RainEnabled = kv.RainEnabled; }
            if(kv.HasRainSpeed) { target.RainSpeed = kv.RainSpeed; }
            if(kv.HasRainHeight) { target.RainHeight = kv.RainHeight; }
            count++;
        }
        if((effective & SettingsImportKeyViewerPart.PositionSize) != 0) {
            if(kv.HasSize) { target.Size = kv.Size; }
            count++;
        }
        if(mode == SettingsImportReplaceMode.ReplaceAll) {
            if(kv.HasEnabled) { target.Enabled = kv.Enabled; }
            if(kv.HasSync) { target.SyncToKeyLimiter = kv.SyncToKeyLimiter; }
        }
        return count;
    }
    public static bool AnyKeys(ImportedKeyViewer kv) =>
        kv.Key10?.Length > 0 || kv.Key12?.Length > 0 || kv.Key16?.Length > 0 || kv.Key20?.Length > 0;
    public static bool AnyLabels(ImportedKeyViewer kv) =>
        kv.Key10Text?.Length > 0 || kv.Key12Text?.Length > 0 || kv.Key16Text?.Length > 0 || kv.Key20Text?.Length > 0;
    public static bool AnyColors(ImportedKeyViewer kv) =>
        kv.Bg != null || kv.BgClicked != null || kv.Outline != null || kv.OutlineClicked != null
        || kv.Text != null || kv.TextClicked != null || kv.Rain != null || kv.Rain2 != null || kv.Rain3 != null;
    public static bool TryParseKvStyle(object value, out int style) {
        style = 0;
        if(value == null) return false;
        string text = value.ToString();
        string digits = new(text.Where(char.IsDigit).ToArray());
        if(int.TryParse(digits, out int keys)) {
            style = keys switch { 10 => 0, 12 => 1, 20 => 3, _ => 2 };
            return true;
        }
        if(int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int raw)) {
            style = Mathf.Clamp(raw, 0, 3);
            return true;
        }
        return false;
    }
}

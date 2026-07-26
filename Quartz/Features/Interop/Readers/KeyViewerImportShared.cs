using System.Globalization;
using Newtonsoft.Json.Linq;
using Quartz.Interop;
using UnityEngine;
using static Quartz.Features.Interop.ReflectionHelpers;
namespace Quartz.Features.Interop.Readers;
internal static class KeyViewerImportShared {
    public static int DeliverKeyViewer(ImportedKeyViewer kv, SettingsImportReplaceMode mode, SettingsImportKeyViewerPart parts) {
        if(kv == null || mode == SettingsImportReplaceMode.KeepOld) return 0;
        ImportSource source = new(ImportSourceKind.KorenResourcePackV1, static _ => null);
        source.Put(ImportKeys.KeyViewerPayload, kv);
        source.Put(ImportKeys.KeyViewerMode, mode);
        source.Put(ImportKeys.KeyViewerParts, parts);
        return ImportRegistry.Deliver(source);
    }
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

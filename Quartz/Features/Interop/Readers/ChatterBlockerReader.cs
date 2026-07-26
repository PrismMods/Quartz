using System.Collections;
using System.Globalization;
using System.Xml.Linq;
using UnityEngine;
using Quartz.Interop;
using Quartz.Utility;
using static Quartz.Features.Interop.ReflectionHelpers;
namespace Quartz.Features.Interop.Readers;
internal static class ChatterBlockerReader {
    private const int LegacyAsyncKeyOffset = 0x1000;
    public static int ImportKeyboardChatterBlocker(SettingsImportOption option) {
        int count = 0;
        bool importedKeys = false;
        Type mainType = SettingsImporter.FindType(option, "KeyboardChatterBlocker.Main");
        object setting = GetStaticMember(mainType, "setting");
        object profile = GetStaticMember(mainType, "selectedKeyLimiterProfile");
        if(setting != null) {
            ImportSource source = new(ImportSourceKind.KeyboardChatterBlocker, static _ => null);
            if(TryGetInt(setting, "inputInterval", out int interval))
                source.Put(ImportKeys.ChatterThresholdMs, interval);
            if(TryGetBool(setting, "enableKeyLimiter", out bool limiterOn))
                source.Put(ImportKeys.KeyLimiterEnabled, limiterOn);
            profile ??= FindSelectedProfile(GetMemberValue(setting, "keyLimiterProfiles"));
            int[] keys = ReadChatterBlockerProfileKeys(profile);
            if(keys.Length > 0) {
                source.Put(ImportKeys.KeyLimiterAllowedKeys, keys);
                importedKeys = true;
            }
            count += ImportRegistry.Deliver(source);
        }
        if(count == 0 || !importedKeys) count += ImportChatterBlockerXml(option, count == 0, !importedKeys);
        if(count > 0) {
            ImportSource enable = new(ImportSourceKind.KeyboardChatterBlocker, static _ => null);
            enable.Put(ImportKeys.ChatterEnabled, true);
            count += ImportRegistry.Deliver(enable);
        }
        return count;
    }
    private static int ImportChatterBlockerXml(SettingsImportOption option, bool importBasics, bool importKeys) {
        XDocument doc = LoadXml(option, "Setting.xml");
        if(doc == null) return 0;
        ImportSource source = new(ImportSourceKind.KeyboardChatterBlocker, static _ => null);
        if(importBasics) {
            if(TryReadXmlInt(doc, "inputInterval", out int interval))
                source.Put(ImportKeys.ChatterThresholdMs, interval);
            if(TryReadXmlBool(doc, "enableKeyLimiter", out bool limiterOn))
                source.Put(ImportKeys.KeyLimiterEnabled, limiterOn);
        }
        if(importKeys) {
            XElement profile = FindSelectedProfileElement(doc, "KeyLimiterProfile");
            int[] keys = ReadChatterBlockerProfileKeys(profile);
            if(keys.Length > 0) source.Put(ImportKeys.KeyLimiterAllowedKeys, keys);
        }
        return ImportRegistry.Deliver(source);
    }
    private static int[] ReadChatterBlockerProfileKeys(object profile) {
        List<int> result = [];
        AddChatterBlockerKeys(result, ReadKeyCodesFromMember(profile, "allowedKeys"));
        AddChatterBlockerVkKeys(result, GetMemberValue(profile, "allowedAsyncKeys"));
        return [.. result];
    }
    private static int[] ReadChatterBlockerProfileKeys(XElement profile) {
        List<int> result = [];
        AddChatterBlockerKeys(result, ReadKeyCodesFromXml(profile, "allowedKeys"));
        if(profile != null && FindFirstDescendant(profile, "allowedAsyncKeys") is XElement asyncList) {
            foreach(XElement item in asyncList.Elements()) {
                if(int.TryParse(item.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int vk)) AddVk(result, vk);
            }
        }
        return [.. result];
    }
    private static void AddChatterBlockerKeys(List<int> result, int[] keys) {
        foreach(int raw in keys) {
            int key = raw;
            if(key == (int)KeyCode.None || KeyCodes.IsMouse((KeyCode)key) || result.Contains(key)) continue;
            result.Add(key);
        }
    }
    private static void AddChatterBlockerVkKeys(List<int> result, object value) {
        if(value is not IEnumerable enumerable || value is string) return;
        foreach(object item in enumerable) {
            if(TryConvertInt(item, out int vk)) AddVk(result, vk);
        }
    }
    private static void AddVk(List<int> result, int vk) {
        if(vk is < ushort.MinValue or > ushort.MaxValue) return;
        int key = (int)KeyCodes.NormalizeNumeric(LegacyAsyncKeyOffset + vk);
        if(key == (int)KeyCode.None || KeyCodes.IsMouse((KeyCode)key) || result.Contains(key)) return;
        result.Add(key);
    }
    private static object FindSelectedProfile(object profiles) {
        if(profiles is not IEnumerable enumerable) return null;
        object first = null;
        foreach(object profile in enumerable) {
            first ??= profile;
            if(TryGetBool(profile, "isSelected", out bool selected) && selected) return profile;
        }
        return first;
    }
}

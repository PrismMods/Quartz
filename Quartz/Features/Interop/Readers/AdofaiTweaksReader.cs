using System.Collections;
using System.Xml.Linq;
using UnityEngine;
using Quartz.Interop;
using static Quartz.Features.Interop.ReflectionHelpers;
namespace Quartz.Features.Interop.Readers;
internal static class AdofaiTweaksReader {
    public static int ImportAdofaiTweaks(SettingsImportOption option) {
        int count = 0;
        foreach(object settings in GetAdofaiTweaksRuntimeSettings(option)) {
            count += ImportAdofaiTweaksSettingsObject(settings);
        }
        count += ImportAdofaiTweaksXml(option);
        return count;
    }
    private static int ImportAdofaiTweaksSettingsObject(object settings) {
        if(settings == null) return 0;
        return settings.GetType().Name switch {
            "KeyLimiterSettings" => ImportAdofaiKeyLimiterObject(settings),
            "KeyViewerSettings" => ImportAdofaiKeyViewerObject(settings),
            "MiscellaneousSettings" => ImportAdofaiMiscObject(settings),
            "HideUiElementsSettings" => ImportAdofaiHideUiObject(settings),
            "RestrictGameplaySettings" => ImportAdofaiRestrictObject(settings),
            _ => 0,
        };
    }
    private static int ImportAdofaiKeyLimiterObject(object settings) {
        ImportSource source = new(ImportSourceKind.AdofaiTweaks, static _ => null);
        if(TryGetBool(settings, "IsEnabled", out bool enabled)) source.Put(ImportKeys.KeyLimiterEnabled, enabled);
        int[] keys = ReadKeyCodesFromMember(settings, "ActiveKeys");
        if(keys.Length > 0) source.Put(ImportKeys.KeyLimiterAllowedKeys, keys);
        return ImportRegistry.Deliver(source);
    }
    private static int ImportAdofaiKeyViewerObject(object settings) {
        object profile = GetActiveIndexedProfile(settings, "Profiles", "ProfileIndex");
        int[] keys = ReadKeyCodesFromMember(profile, "ActiveKeys");
        if(keys.Length == 0) return 0;
        return DeliverAllowedKeys(keys);
    }
    private static int DeliverAllowedKeys(int[] keys) {
        ImportSource source = new(ImportSourceKind.AdofaiTweaks, static _ => null);
        source.Put(ImportKeys.KeyLimiterAllowedKeys, keys);
        return ImportRegistry.Deliver(source);
    }
    private static int ImportAdofaiMiscObject(object settings) {
        if(TryGetBool(settings, "IsEnabled", out bool enabled) && enabled
            && TryGetBool(settings, "DisableEditorZoom", out bool noZoom) && noZoom) {
            return DeliverBlockScroll();
        }
        return 0;
    }
    private static int ImportAdofaiHideUiObject(object settings) {
        if(!TryGetBool(settings, "IsEnabled", out bool enabled) || !enabled) return 0;
        ImportSource source = new(ImportSourceKind.AdofaiTweaks, name => GetMemberValue(settings, name));
        Put(source, ImportKeys.UiHiderPlayingProfile, ObjectProfile(GetMemberValue(settings, "PlayingProfile")));
        Put(source, ImportKeys.UiHiderRecordingProfile, ObjectProfile(GetMemberValue(settings, "RecordingProfile")));
        if(TryGetBool(settings, "RecordingMode", out bool rec)) source.Put(ImportKeys.UiHiderRecordingMode, rec);
        if(TryGetBool(settings, "UseRecordingModeShortcut", out bool useShortcut))
            source.Put(ImportKeys.UiHiderUseShortcut, useShortcut);
        object shortcut = GetMemberValue(settings, "RecordingModeShortcut");
        if(shortcut != null) {
            source.Put(ImportKeys.UiHiderShortcut, (Func<string, bool>)(name => TryGetBool(shortcut, name, out bool v) && v));
            if(TryGetInt(shortcut, "PressKey", out int key))
                source.Put(ImportKeys.UiHiderShortcutKey, NormalizeKeyInt(key));
        }
        return ImportRegistry.Deliver(source);
    }
    private static Func<string, bool?> ObjectProfile(object profile) =>
        profile == null ? null : name => TryGetBool(profile, name, out bool v) ? v : null;
    private static Func<string, bool?> XmlProfile(XElement profile) =>
        profile == null ? null : name => TryReadXmlBool(profile, name, out bool v) ? v : null;
    private static void Put(ImportSource source, string key, Func<string, bool?> read) {
        if(read != null) source.Put(key, read);
    }
    private static int ImportAdofaiRestrictObject(object settings) {
        if(!TryGetBool(settings, "IsEnabled", out bool enabled) || !enabled) return 0;
        if(!TryGetBool(settings, "RestrictJudgment", out bool restrict) || !restrict) return 0;
        bool[] restricted = ReadBoolArray(GetMemberValue(settings, "RestrictedJudgments"));
        if(restricted.Length == 0) return 0;
        return DeliverRestrictMask(restricted);
    }
    private static int DeliverBlockScroll() {
        ImportSource source = new(ImportSourceKind.AdofaiTweaks, static _ => null);
        source.Put(ImportKeys.TweaksBlockScroll, true);
        return ImportRegistry.Deliver(source);
    }
    private static int DeliverRestrictMask(bool[] restricted) {
        int allowedMask = 0;
        for(int i = 0; i < restricted.Length; i++)
            if(!restricted[i]) allowedMask |= 1 << i;
        ImportSource source = new(ImportSourceKind.AdofaiTweaks, static _ => null);
        source.Put(ImportKeys.RestrictionAllowedMask, allowedMask);
        return ImportRegistry.Deliver(source);
    }
    private static int ImportAdofaiTweaksXml(SettingsImportOption option) {
        int count = 0;
        XDocument keyLimiter = LoadXml(option, "KeyLimiterSettings.xml");
        if(keyLimiter != null) {
            ImportSource source = new(ImportSourceKind.AdofaiTweaks, static _ => null);
            if(TryReadXmlBool(keyLimiter, "IsEnabled", out bool enabled))
                source.Put(ImportKeys.KeyLimiterEnabled, enabled);
            int[] keys = ReadKeyCodesFromXml(keyLimiter.Root, "ActiveKeys");
            if(keys.Length > 0) source.Put(ImportKeys.KeyLimiterAllowedKeys, keys);
            count += ImportRegistry.Deliver(source);
        }
        XDocument keyViewer = LoadXml(option, "KeyViewerSettings.xml");
        if(keyViewer != null) {
            int[] keys = ReadAdofaiKeyViewerXmlKeys(keyViewer);
            if(keys.Length > 0) {
                count += DeliverAllowedKeys(keys);
            }
        }
        XDocument misc = LoadXml(option, "MiscellaneousSettings.xml");
        if(misc != null
            && TryReadXmlBool(misc, "IsEnabled", out bool miscOn) && miscOn
            && TryReadXmlBool(misc, "DisableEditorZoom", out bool noZoom) && noZoom) {
            count += DeliverBlockScroll();
        }
        XDocument hideUi = LoadXml(option, "HideUiElementsSettings.xml");
        if(hideUi != null && TryReadXmlBool(hideUi, "IsEnabled", out bool hideOn) && hideOn) {
            ImportSource source = new(ImportSourceKind.AdofaiTweaks, static _ => null);
            Put(source, ImportKeys.UiHiderPlayingProfile, XmlProfile(FindFirstDescendant(hideUi, "PlayingProfile")));
            Put(source, ImportKeys.UiHiderRecordingProfile, XmlProfile(FindFirstDescendant(hideUi, "RecordingProfile")));
            if(TryReadXmlBool(hideUi, "RecordingMode", out bool rec)) source.Put(ImportKeys.UiHiderRecordingMode, rec);
            if(TryReadXmlBool(hideUi, "UseRecordingModeShortcut", out bool useSc))
                source.Put(ImportKeys.UiHiderUseShortcut, useSc);
            XElement shortcut = FindFirstDescendant(hideUi, "RecordingModeShortcut");
            if(shortcut != null) {
                source.Put(ImportKeys.UiHiderShortcut, (Func<string, bool>)(name => TryReadXmlBool(shortcut, name, out bool v) && v));
                if(TryReadXmlKeyCode(shortcut, "PressKey", out int key))
                    source.Put(ImportKeys.UiHiderShortcutKey, NormalizeKeyInt(key));
            }
            count += ImportRegistry.Deliver(source);
        }
        XDocument restrict = LoadXml(option, "RestrictGameplaySettings.xml");
        if(restrict != null
            && TryReadXmlBool(restrict, "IsEnabled", out bool rOn) && rOn
            && TryReadXmlBool(restrict, "RestrictJudgment", out bool rJ) && rJ) {
            bool[] restricted = ReadXmlBoolArray(restrict, "RestrictedJudgments");
            if(restricted.Length > 0) {
                count += DeliverRestrictMask(restricted);
            }
        }
        return count;
    }
    private static List<object> GetAdofaiTweaksRuntimeSettings(SettingsImportOption option) {
        List<object> settings = [];
        object runners = GetStaticMember(SettingsImporter.FindType(option, "AdofaiTweaks.AdofaiTweaks"), "tweakRunners");
        if(runners is not IEnumerable enumerable) return settings;
        foreach(object runner in enumerable) {
            object value = GetMemberValue(runner, "Settings");
            if(value != null) settings.Add(value);
        }
        return settings;
    }
    private static object GetActiveIndexedProfile(object settings, string listMember, string indexMember) {
        if(GetMemberValue(settings, listMember) is not IEnumerable enumerable) return null;
        int index = TryGetInt(settings, indexMember, out int i) ? i : 0;
        int n = 0;
        object first = null;
        foreach(object item in enumerable) {
            first ??= item;
            if(n == index) return item;
            n++;
        }
        return first;
    }
    private static int[] ReadAdofaiKeyViewerXmlKeys(XDocument doc) {
        TryReadXmlInt(doc, "ProfileIndex", out int profileIndex);
        XElement profiles = FindFirstDescendant(doc, "Profiles");
        if(profiles == null) return [];
        List<XElement> list = profiles.Elements().ToList();
        if(list.Count == 0) return [];
        if(profileIndex < 0 || profileIndex >= list.Count) profileIndex = 0;
        return ReadKeyCodesFromXml(list[profileIndex], "ActiveKeys");
    }
}

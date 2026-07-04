using System.Collections;
using System.Xml.Linq;
using Quartz.Features.Restriction;
using Quartz.Features.UiHider;
using UnityEngine;
using static Quartz.Features.Interop.ReflectionHelpers;

namespace Quartz.Features.Interop.Readers;

// ===== AdofaiTweaks =====
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
        int count = 0;
        if(TryGetBool(settings, "IsEnabled", out bool enabled)) {
            Features.KeyLimiter.KeyLimiter.EnsureConf();
            Features.KeyLimiter.KeyLimiter.Conf.Enabled = enabled;
            count++;
        }
        int[] keys = ReadKeyCodesFromMember(settings, "ActiveKeys");
        if(keys.Length > 0) {
            Features.KeyLimiter.KeyLimiter.SetAllowedKeys(keys);
            count++;
        }
        return count;
    }

    private static int ImportAdofaiKeyViewerObject(object settings) {
        object profile = GetActiveIndexedProfile(settings, "Profiles", "ProfileIndex");
        int[] keys = ReadKeyCodesFromMember(profile, "ActiveKeys");
        if(keys.Length == 0) return 0;
        Features.KeyLimiter.KeyLimiter.SetAllowedKeys(keys);
        return 1;
    }

    private static int ImportAdofaiMiscObject(object settings) {
        if(TryGetBool(settings, "IsEnabled", out bool enabled) && enabled
            && TryGetBool(settings, "DisableEditorZoom", out bool noZoom) && noZoom) {
            Features.Tweaks.Tweaks.EnsureConf();
            Features.Tweaks.Tweaks.Conf.BlockMouseWheelScrollWhilePlaying = true;
            return 1;
        }
        return 0;
    }

    private static int ImportAdofaiHideUiObject(object settings) {
        if(!TryGetBool(settings, "IsEnabled", out bool enabled) || !enabled) return 0;

        Features.UiHider.UiHider.EnsureConf();
        int count = 0;
        count += ApplyAdofaiHideUiProfile(GetMemberValue(settings, "PlayingProfile"), Features.UiHider.UiHider.Conf.Playing);
        count += ApplyAdofaiHideUiProfile(GetMemberValue(settings, "RecordingProfile"), Features.UiHider.UiHider.Conf.Recording);

        if(TryGetBool(settings, "RecordingMode", out bool rec)) {
            Features.UiHider.UiHider.Conf.RecordingMode = rec;
            count++;
        }
        if(TryGetBool(settings, "UseRecordingModeShortcut", out bool useShortcut)) {
            Features.UiHider.UiHider.Conf.UseShortcut = useShortcut;
            count++;
        }

        object shortcut = GetMemberValue(settings, "RecordingModeShortcut");
        if(shortcut != null) {
            ApplyShortcutModifier(shortcut);
            if(TryGetInt(shortcut, "PressKey", out int key)) {
                Features.UiHider.UiHider.Conf.ShortcutKey = NormalizeKeyInt(key);
                count++;
            }
        }

        if(count > 0) {
            Features.UiHider.UiHider.Conf.Enabled = true;
            count++;
        }
        return count;
    }

    private static int ImportAdofaiRestrictObject(object settings) {
        if(!TryGetBool(settings, "IsEnabled", out bool enabled) || !enabled) return 0;
        if(!TryGetBool(settings, "RestrictJudgment", out bool restrict) || !restrict) return 0;

        bool[] restricted = ReadBoolArray(GetMemberValue(settings, "RestrictedJudgments"));
        if(restricted.Length == 0) return 0;

        return ApplyRestrictMask(restricted);
    }

    private static int ImportAdofaiTweaksXml(SettingsImportOption option) {
        int count = 0;

        XDocument keyLimiter = LoadXml(option, "KeyLimiterSettings.xml");
        if(keyLimiter != null) {
            if(TryReadXmlBool(keyLimiter, "IsEnabled", out bool enabled)) {
                Features.KeyLimiter.KeyLimiter.EnsureConf();
                Features.KeyLimiter.KeyLimiter.Conf.Enabled = enabled;
                count++;
            }
            int[] keys = ReadKeyCodesFromXml(keyLimiter.Root, "ActiveKeys");
            if(keys.Length > 0) {
                Features.KeyLimiter.KeyLimiter.SetAllowedKeys(keys);
                count++;
            }
        }

        XDocument keyViewer = LoadXml(option, "KeyViewerSettings.xml");
        if(keyViewer != null) {
            int[] keys = ReadAdofaiKeyViewerXmlKeys(keyViewer);
            if(keys.Length > 0) {
                Features.KeyLimiter.KeyLimiter.SetAllowedKeys(keys);
                count++;
            }
        }

        XDocument misc = LoadXml(option, "MiscellaneousSettings.xml");
        if(misc != null
            && TryReadXmlBool(misc, "IsEnabled", out bool miscOn) && miscOn
            && TryReadXmlBool(misc, "DisableEditorZoom", out bool noZoom) && noZoom) {
            Features.Tweaks.Tweaks.EnsureConf();
            Features.Tweaks.Tweaks.Conf.BlockMouseWheelScrollWhilePlaying = true;
            count++;
        }

        XDocument hideUi = LoadXml(option, "HideUiElementsSettings.xml");
        if(hideUi != null && TryReadXmlBool(hideUi, "IsEnabled", out bool hideOn) && hideOn) {
            Features.UiHider.UiHider.EnsureConf();
            int profileCount = 0;
            profileCount += ApplyAdofaiHideUiProfileXml(FindFirstDescendant(hideUi, "PlayingProfile"), Features.UiHider.UiHider.Conf.Playing);
            profileCount += ApplyAdofaiHideUiProfileXml(FindFirstDescendant(hideUi, "RecordingProfile"), Features.UiHider.UiHider.Conf.Recording);
            if(TryReadXmlBool(hideUi, "RecordingMode", out bool rec)) { Features.UiHider.UiHider.Conf.RecordingMode = rec; profileCount++; }
            if(TryReadXmlBool(hideUi, "UseRecordingModeShortcut", out bool useSc)) { Features.UiHider.UiHider.Conf.UseShortcut = useSc; profileCount++; }

            XElement shortcut = FindFirstDescendant(hideUi, "RecordingModeShortcut");
            if(shortcut != null) {
                ApplyShortcutModifierXml(shortcut);
                if(TryReadXmlKeyCode(shortcut, "PressKey", out int key)) {
                    Features.UiHider.UiHider.Conf.ShortcutKey = NormalizeKeyInt(key);
                    profileCount++;
                }
            }

            if(profileCount > 0) {
                Features.UiHider.UiHider.Conf.Enabled = true;
                count += profileCount + 1;
            }
        }

        XDocument restrict = LoadXml(option, "RestrictGameplaySettings.xml");
        if(restrict != null
            && TryReadXmlBool(restrict, "IsEnabled", out bool rOn) && rOn
            && TryReadXmlBool(restrict, "RestrictJudgment", out bool rJ) && rJ) {
            bool[] restricted = ReadXmlBoolArray(restrict, "RestrictedJudgments");
            if(restricted.Length > 0) {
                count += ApplyRestrictMask(restricted);
            }
        }

        return count;
    }

    private static int ApplyRestrictMask(bool[] restricted) {
        int allowedMask = 0;
        for(int i = 0; i < restricted.Length; i++) {
            if(!restricted[i]) {
                allowedMask |= 1 << i;
            }
        }
        Features.Restriction.Restriction.EnsureConf();
        Features.Restriction.Restriction.Conf.JRestrictEnabled = true;
        Features.Restriction.Restriction.Conf.JRestrictMode = 3;
        Features.Restriction.Restriction.Conf.JRestrictAllowedMask = allowedMask;
        return 3;
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

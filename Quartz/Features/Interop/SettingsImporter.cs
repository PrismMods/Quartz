using System.Reflection;
using System.Text;
using Quartz.Core;
using Quartz.Features.Interop.Readers;
using Quartz.IO;
using Newtonsoft.Json.Linq;

namespace Quartz.Features.Interop;

public enum SettingsImportSource {
    KeyboardChatterBlocker,
    JipperKeyViewer,
    JipperResourcePack,
    AdofaiTweaks,
    EnhancedEffectRemover,
    KorenResourcePackV1,
}

public enum SettingsImportReplaceMode {
    ReplaceAll,
    ReplaceCertain,
    KeepOld,
}

[Flags]
public enum SettingsImportKeyViewerPart {
    None = 0,
    KeysLayout = 1 << 0,
    Labels = 1 << 1,
    Colors = 1 << 2,
    Rain = 1 << 3,
    PositionSize = 1 << 4,
    // v2 KeyViewer has no foot/ghost/per-key/streamer/font data, and its press
    // counts are stored by KeyCode name (not the source mods' indexed arrays),
    // so Counts/Display/Font from v1's importer have no v2 target and are
    // deliberately not offered here.
    All = KeysLayout | Labels | Colors | Rain | PositionSize,
}

public sealed class SettingsImportOption {
    public SettingsImportSource Source;
    public string Id;
    public string Label;
    public Assembly Assembly;
    public string Directory;
    public string OptionId;
}

public sealed class SettingsImportResult {
    public bool Success;
    public int ImportedCount;
    public string Message;
    public string ProfileName;
}

// A UMM mod installed on disk, with the label to show for it. The Import page
// lists every installed mod — the ones Quartz can't import from are surfaced
// as "Not Compatible" rather than hidden, so the user sees their whole roster.
public sealed class InstalledModInfo {
    public string Id;
    public string Label;
}

// Migrates settings INTO Quartz from other ADOFAI mods, reading
// their state purely through reflection + their on-disk config (Quartz never
// hard-links another mod — see UmmInterop). Ported from v1's SettingsImporter,
// with the apply layer rewritten onto v2's per-feature SettingsFile<T> model.
//
// Mapping coverage is bounded by what v2 actually has: feature on/off, key
// limiter keys, the KeyViewer (simple mode), Combo, Judgement, ProgressBar,
// Otto/planet colors, UI hiding, judgement restriction, Tweaks, and the Effect
// Remover all map across. Source data v2 has no home for (text-stat color
// ranges, BPM color, foot/ghost keys, per-key counts, attempt/play history) is
// skipped rather than guessed at.
//
// This class is a thin dispatcher: the per-source-mod import logic lives in
// Quartz/Features/Interop/Readers/ (one reader per source mod), and generic
// reflection/XML/JSON helpers shared across readers live in
// Quartz/Features/Interop/ReflectionHelpers.cs.
public static class SettingsImporter {
    private sealed class ImportSpec {
        public readonly SettingsImportSource Source;
        public readonly string DisplayName;
        public readonly string[] Aliases;

        public ImportSpec(SettingsImportSource source, string displayName, params string[] aliases) {
            Source = source;
            DisplayName = displayName;
            Aliases = aliases;
        }
    }

    private static readonly ImportSpec[] Specs = [
        new(SettingsImportSource.KeyboardChatterBlocker, "KeyboardChatterBlocker",
            "KeyboardChatterBlocker", "Keyboard Chatter Blocker"),
        new(SettingsImportSource.JipperKeyViewer, "JipperKeyViewer",
            "JipperKeyViewer", "JipperKeyViewer-FileBased", "Jipper Key Viewer", "Jipper Key Viewer File Based"),
        new(SettingsImportSource.JipperResourcePack, "JipperResourcePack",
            "JipperResourcePack", "Jipper Resource Pack"),
        new(SettingsImportSource.AdofaiTweaks, "ADOFAI Tweaks",
            "AdofaiTweaks", "ADOFAI Tweaks"),
        new(SettingsImportSource.EnhancedEffectRemover, "EnhancedEffectRemover",
            "EnhancedEffectRemover", "Enhanced Effect Remover"),
        // v1 KorenResourcePack — this mod's own predecessor. It's a UMM mod
        // (Id "KorenResourcePack", DisplayName "koren resource pack"), so it's
        // picked up by the same UMM discovery as the others. Because v2 is a
        // direct rewrite of v1, almost every v1 setting has a v2 home.
        new(SettingsImportSource.KorenResourcePackV1, "KorenResourcePack (v1)",
            "KorenResourcePack", "koren resource pack"),
    ];

    // Every supported source mod UMM knows about, ready to import from. Most
    // sources also have an on-disk config reader (their Settings.xml/.json is on
    // disk whether or not the mod is loaded), so they're matched against INSTALLED
    // mods and can be imported even while disabled in UMM — an upgrader has usually
    // toggled the old mod off, and it should still show up to import from. The lone
    // exception is JipperResourcePack: it's read purely from its live types (no
    // disk fallback), so it must still be ACTIVE for its assembly to be loaded.
    public static List<SettingsImportOption> GetAvailableOptions() {
        List<SettingsImportOption> options = [];
        if(!UmmInterop.IsPresent) return options;

        List<string> activeIds = UmmInterop.ActiveModIds();
        List<string> installedIds = UmmInterop.InstalledModIds();
        foreach(ImportSpec spec in Specs) {
            List<string> ids = spec.Source == SettingsImportSource.JipperResourcePack ? activeIds : installedIds;
            foreach(string id in ids) {
                object entry = UmmInterop.FindMod(id);
                if(entry == null || !EntryMatches(entry, id, spec)) continue;

                string display = StripRichText(ReflectionHelpers.ReadNested(entry, "Info", "DisplayName") as string);
                // v1's own DisplayName is the lowercase "koren resource pack",
                // which reads as ambiguous sitting inside v2 — force the spec's
                // explicit "(v1)" label so the source is unmistakable.
                if(string.IsNullOrEmpty(display) || spec.Source == SettingsImportSource.KorenResourcePackV1) {
                    display = spec.DisplayName;
                }

                options.Add(new SettingsImportOption {
                    Source = spec.Source,
                    Id = id,
                    Label = display,
                    Assembly = UmmInterop.GetModAssembly(id) ?? FindAssemblyByName(id, spec),
                    Directory = ResolveDirectory(ReflectionHelpers.ReadMember(entry, "Path") as string),
                    OptionId = spec.Source + ":" + id,
                });
                break;
            }
        }

        // Safety net for v1: if it didn't surface through modEntries (e.g. UMM
        // didn't parse it, or it was dropped in after UMM started), scan the mods
        // folder on disk for its Info.json + Settings.xml directly.
        if(!options.Any(o => o.Source == SettingsImportSource.KorenResourcePackV1)) {
            SettingsImportOption diskV1 = ScanDiskForKorenV1();
            if(diskV1 != null) options.Add(diskV1);
        }

        return options;
    }

    // Every UMM mod installed on disk (enabled or not), deduped, each with a
    // display label (its UMM DisplayName, rich-text stripped; the id is the
    // fallback). The Import page diff's this against GetAvailableOptions to show
    // the ones with no Quartz importer as "Not Compatible".
    public static List<InstalledModInfo> GetAllInstalledMods() {
        List<InstalledModInfo> mods = [];
        if(!UmmInterop.IsPresent) return mods;

        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        foreach(string id in UmmInterop.InstalledModIds()) {
            if(string.IsNullOrEmpty(id) || !seen.Add(id)) continue;
            object entry = UmmInterop.FindMod(id);
            string display = StripRichText(ReflectionHelpers.ReadNested(entry, "Info", "DisplayName") as string);
            mods.Add(new InstalledModInfo {
                Id = id,
                Label = string.IsNullOrEmpty(display) ? id : display,
            });
        }
        return mods;
    }

    // Walk the UMM mods folder ("UMMMods") looking for an installed v1 — matched
    // by its Info.json Id (or folder name) and only offered if it has a
    // Settings.xml to read. Independent of whether v1 is enabled/loaded in UMM.
    private static SettingsImportOption ScanDiskForKorenV1() {
        ImportSpec spec = Array.Find(Specs, s => s.Source == SettingsImportSource.KorenResourcePackV1);
        if(spec == null) return null;

        foreach(string root in UmmModsRoots()) {
            string[] dirs;
            try {
                if(string.IsNullOrEmpty(root) || !Directory.Exists(root)) continue;
                dirs = Directory.GetDirectories(root);
            } catch { continue; }

            foreach(string dir in dirs) {
                string id = ReadInfoJsonId(dir);
                string folder = Path.GetFileName(dir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                if(!V1FolderMatches(spec, id, folder)) continue;
                if(!File.Exists(Path.Combine(dir, "Settings.xml"))) continue; // nothing to import from

                string resolvedId = string.IsNullOrEmpty(id) ? "KorenResourcePack" : id;
                return new SettingsImportOption {
                    Source = SettingsImportSource.KorenResourcePackV1,
                    Id = resolvedId,
                    Label = spec.DisplayName,
                    Assembly = UmmInterop.GetModAssembly(resolvedId),
                    Directory = dir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                    OptionId = SettingsImportSource.KorenResourcePackV1 + ":" + resolvedId,
                };
            }
        }
        return null;
    }

    // Candidate mods-folder roots to scan, most authoritative first: UMM's own
    // resolved modsPath, then the parent of any already-known mod's folder (which
    // is that same UMMMods dir) as a backstop.
    private static IEnumerable<string> UmmModsRoots() {
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        string modsPath = UmmInterop.ModsPath();
        if(!string.IsNullOrEmpty(modsPath) && seen.Add(modsPath)) yield return modsPath;
        foreach(string id in UmmInterop.InstalledModIds()) {
            object entry = UmmInterop.FindMod(id);
            string dir = ResolveDirectory(ReflectionHelpers.ReadMember(entry, "Path") as string);
            if(string.IsNullOrEmpty(dir)) continue;
            string parent = Path.GetDirectoryName(dir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if(!string.IsNullOrEmpty(parent) && seen.Add(parent)) yield return parent;
        }
    }

    private static bool V1FolderMatches(ImportSpec spec, string id, string folder) {
        string nId = NormalizeModToken(id);
        string nFolder = NormalizeModToken(folder);
        foreach(string alias in spec.Aliases) {
            string a = NormalizeModToken(alias);
            if((nId.Length > 0 && nId == a) || (nFolder.Length > 0 && nFolder == a)) return true;
        }
        return false;
    }

    private static string ReadInfoJsonId(string dir) {
        foreach(string name in new[] { "Info.json", "info.json" }) {
            string path = Path.Combine(dir, name);
            try {
                if(File.Exists(path)
                    && JObject.Parse(File.ReadAllText(path)) is { } obj
                    && obj.TryGetValue("Id", StringComparison.OrdinalIgnoreCase, out JToken t)) {
                    return t?.ToString();
                }
            } catch { }
        }
        return null;
    }

    public static SettingsImportOption FindOption(List<SettingsImportOption> options, string optionId) {
        if(options == null || string.IsNullOrEmpty(optionId)) return null;
        foreach(SettingsImportOption option in options) {
            if(string.Equals(option.OptionId, optionId, StringComparison.Ordinal)) return option;
        }
        return null;
    }

    public static bool HasKeyViewerPayload(SettingsImportSource source) =>
        source is SettingsImportSource.JipperKeyViewer
            or SettingsImportSource.JipperResourcePack
            or SettingsImportSource.KorenResourcePackV1;

    public static SettingsImportKeyViewerPart GetSupportedKeyViewerParts(SettingsImportSource source) =>
        HasKeyViewerPayload(source) ? SettingsImportKeyViewerPart.All : SettingsImportKeyViewerPart.None;

    // NOTE: this file lives under Quartz.Features.Interop, so sibling namespaces
    // (Quartz.Features.Tweaks, .ChatterBlocker, ...) shadow the facade classes
    // whose name matches their namespace leaf. Those are referenced through the
    // fully-qualified Features.X.X form (matching KeyViewerOverlay's convention)
    // rather than a bare name.
    public static SettingsImportResult Import(SettingsImportOption option) =>
        Import(option, SettingsImportReplaceMode.ReplaceAll, SettingsImportKeyViewerPart.All);

    public static SettingsImportResult ImportToProfile(
        SettingsImportOption option,
        SettingsImportReplaceMode keyViewerMode,
        SettingsImportKeyViewerPart keyViewerParts
    ) {
        SettingsImportResult result = new();
        if(option == null) {
            result.Message = MainCore.Tr.Get("IMPORT_ERR_NO_TARGET", "No import target selected.");
            return result;
        }

        string previous = ProfileManager.Active;
        string profile = null;
        try {
            profile = ProfileManager.CreateUnique(ProfileNames.ImportedModName(option.Label));
            if(string.IsNullOrEmpty(profile)) {
                result.Message = MainCore.Tr.Get("IMPORT_ERR_CREATE_PROFILE", "Could not create an import profile.");
                return result;
            }

            result = Import(option, keyViewerMode, keyViewerParts);
            result.ProfileName = profile;

            if(!result.Success || result.ImportedCount <= 0) {
                RestorePreviousProfile(previous);
                ProfileManager.Delete(profile);
                return result;
            }

            if(!RestorePreviousProfile(previous)) {
                result.Message = MainCore.Tr.Get(
                    "IMPORT_ERR_PROFILE_SWITCH_BACK",
                    "Imported into profile, but could not switch back to the previous profile."
                );
            }
            return result;
        } catch(Exception ex) {
            MainCore.Log.Err($"[SettingsImporter] profile import failed: {ex}");
            RestorePreviousProfile(previous);
            if(!string.IsNullOrEmpty(profile)) ProfileManager.Delete(profile);
            result.Message = ex.Message;
            return result;
        }
    }

    private static bool RestorePreviousProfile(string previous) =>
        string.IsNullOrEmpty(previous)
            || previous == ProfileManager.Active
            || ProfileManager.Apply(previous);

    public static SettingsImportResult Import(
        SettingsImportOption option,
        SettingsImportReplaceMode keyViewerMode,
        SettingsImportKeyViewerPart keyViewerParts
    ) {
        SettingsImportResult result = new();
        if(option == null) {
            result.Message = MainCore.Tr.Get("IMPORT_ERR_NO_TARGET", "No import target selected.");
            return result;
        }

        if(HasKeyViewerPayload(option.Source)) {
            keyViewerParts &= GetSupportedKeyViewerParts(option.Source);
            // The "pick at least one group" guard only makes sense when the
            // KeyViewer is the source's ONLY payload (the Jipper mods). v1 also
            // brings combo/judgement/restriction/tweaks/etc., so an empty
            // KeyViewer selection there just means "skip the KeyViewer", not
            // "nothing to do".
            if(option.Source != SettingsImportSource.KorenResourcePackV1
                && keyViewerMode == SettingsImportReplaceMode.ReplaceCertain
                && keyViewerParts == SettingsImportKeyViewerPart.None) {
                result.Message = MainCore.Tr.Get("IMPORT_ERR_SELECT_KEYVIEWER_PART", "Select at least one KeyViewer setting group.");
                return result;
            }
        }

        try {
            int count = option.Source switch {
                SettingsImportSource.KeyboardChatterBlocker => ChatterBlockerReader.ImportKeyboardChatterBlocker(option),
                SettingsImportSource.JipperKeyViewer => JipperKeyViewerReader.ImportJipperKeyViewer(option, keyViewerMode, keyViewerParts),
                SettingsImportSource.JipperResourcePack => JipperResourcePackReader.ImportJipperResourcePack(option, keyViewerMode, keyViewerParts),
                SettingsImportSource.AdofaiTweaks => AdofaiTweaksReader.ImportAdofaiTweaks(option),
                SettingsImportSource.EnhancedEffectRemover => EnhancedEffectRemoverReader.ImportEnhancedEffectRemover(option),
                SettingsImportSource.KorenResourcePackV1 => KorenResourcePackV1Reader.ImportKorenResourcePackV1(option, keyViewerMode, keyViewerParts),
                _ => 0,
            };

            if(count <= 0) {
                if(HasKeyViewerPayload(option.Source) && keyViewerMode == SettingsImportReplaceMode.KeepOld) {
                    result.Success = true;
                    return result;
                }
                result.Message = MainCore.Tr.Get("IMPORT_ERR_NO_READABLE_SETTINGS", "No readable settings found.");
                return result;
            }

            PostImportRefresh();
            result.Success = true;
            result.ImportedCount = count;
            return result;
        } catch(Exception ex) {
            MainCore.Log.Err($"[SettingsImporter] {ex}");
            result.Message = ex.Message;
            return result;
        }
    }

    // ===== post-import refresh =====

    // Re-apply + persist every feature the import could have touched. Each call
    // is guarded so an overlay that isn't live (mod disabled) can't abort the
    // run, and every feature's Apply/Refresh is already change-guarded.
    private static void PostImportRefresh() {
        Try(() => { Features.ChatterBlocker.ChatterBlocker.EnsureConf(); Features.ChatterBlocker.ChatterBlocker.Save(); });
        Try(() => { Features.KeyLimiter.KeyLimiter.EnsureConf(); Features.KeyLimiter.KeyLimiter.Save(); });
        Try(() => { Features.KeyViewer.KeyViewerOverlay.EnsureConf(); Features.KeyViewer.KeyViewerOverlay.SyncKeysToKeyLimiter(); });
        Try(() => { Features.KeyViewer.KeyViewerOverlay.Rebuild(); });
        Try(() => { Features.KeyViewer.KeyViewerOverlay.Apply(); });
        Try(() => { Features.KeyViewer.KeyViewerOverlay.Save(); });
        Try(() => { Features.Combo.ComboOverlay.EnsureConf(); Features.Combo.ComboOverlay.Apply(); Features.Combo.ComboOverlay.Save(); });
        Try(() => { Features.Judgement.JudgementOverlay.EnsureConf(); Features.Judgement.JudgementOverlay.Apply(); Features.Judgement.JudgementOverlay.Save(); });
        Try(() => { Features.ProgressBar.ProgressBarOverlay.EnsureConf(); Features.ProgressBar.ProgressBarOverlay.Apply(); Features.ProgressBar.ProgressBarOverlay.Save(); });
        Try(() => { Features.Tweaks.Tweaks.EnsureConf(); Features.Tweaks.Tweaks.RefreshAll(); Features.Tweaks.Tweaks.Save(); });
        Try(() => { Features.OttoIcon.OttoIcon.EnsureConf(); Features.OttoIcon.OttoIcon.Refresh(); Features.OttoIcon.OttoIcon.Save(); });
        Try(() => { Features.PlanetColors.PlanetColors.EnsureConf(); Features.PlanetColors.PlanetColors.Refresh(); Features.PlanetColors.PlanetColors.Save(); });
        Try(() => { Features.UiHider.UiHider.EnsureConf(); Features.UiHider.UiHider.ApplyNow(); Features.UiHider.UiHider.Save(); });
        Try(() => { Features.Restriction.Restriction.EnsureConf(); Features.Restriction.Restriction.Save(); });
        Try(() => { Features.EffectRemover.EffectRemover.EnsureConf(); Features.EffectRemover.EffectRemover.RefreshEditorSaveButtons(); Features.EffectRemover.EffectRemover.Save(); });
    }

    private static void Try(Action action) {
        try { action(); } catch(Exception e) { MainCore.Log.Wrn($"[SettingsImporter] refresh step failed: {e.Message}"); }
    }

    // ===== matching / discovery =====

    private static bool EntryMatches(object entry, string id, ImportSpec spec) {
        if(entry == null) return false;
        string normId = NormalizeModToken(id);
        string normDisplay = NormalizeModToken(StripRichText(ReflectionHelpers.ReadNested(entry, "Info", "DisplayName") as string));
        string normFolder = NormalizeModToken(Path.GetFileName(ResolveDirectory(ReflectionHelpers.ReadMember(entry, "Path") as string) ?? ""));
        foreach(string alias in spec.Aliases) {
            string normAlias = NormalizeModToken(alias);
            if(normId == normAlias || normDisplay == normAlias || normFolder == normAlias) return true;
        }
        return false;
    }

    private static Assembly FindAssemblyByName(string id, ImportSpec spec) {
        Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
        foreach(Assembly asm in assemblies) {
            string name = NormalizeModToken(asm.GetName().Name);
            if(name == NormalizeModToken(id)) return asm;
            foreach(string alias in spec.Aliases) {
                if(name == NormalizeModToken(alias)) return asm;
            }
        }
        return null;
    }

    // Shared by every reader to resolve a source mod's types by full name,
    // either from the option's own assembly or one loaded from its folder.
    internal static Type FindType(SettingsImportOption option, string fullName) {
        Type type = option.Assembly?.GetType(fullName, false);
        if(type != null) return type;
        // Fallback: an assembly loaded from the mod's folder.
        if(!string.IsNullOrEmpty(option.Directory)) {
            foreach(Assembly asm in AppDomain.CurrentDomain.GetAssemblies()) {
                try {
                    string loc = asm.Location;
                    if(!string.IsNullOrEmpty(loc)
                        && loc.StartsWith(option.Directory, StringComparison.OrdinalIgnoreCase)) {
                        Type t = asm.GetType(fullName, false);
                        if(t != null) return t;
                    }
                } catch { }
            }
        }
        return null;
    }

    private static string ResolveDirectory(string path) {
        if(string.IsNullOrEmpty(path)) return null;
        try {
            return File.Exists(path)
                ? Path.GetDirectoryName(path)
                : path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        } catch {
            return path;
        }
    }

    private static string NormalizeModToken(string text) {
        if(string.IsNullOrEmpty(text)) return "";
        StringBuilder sb = new(text.Length);
        foreach(char ch in text) {
            char c = char.ToLowerInvariant(ch);
            if(char.IsLetterOrDigit(c)) sb.Append(c);
        }
        return sb.ToString();
    }

    private static string StripRichText(string text) {
        if(string.IsNullOrEmpty(text)) return "";
        StringBuilder sb = new(text.Length);
        bool inTag = false;
        foreach(char c in text) {
            if(c == '<') { inTag = true; continue; }
            if(c == '>') { inTag = false; continue; }
            if(!inTag) { sb.Append(c); }
        }
        return sb.ToString().Trim();
    }
}

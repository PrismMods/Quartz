using Quartz.Async;
using Quartz.Compat;
using Quartz.Compat.Interface;
using Quartz.IO;
using Quartz.Localization;
using UnityEngine;
namespace Quartz.Core.Service;
public sealed class LocalizationService(
    string langPath,
    SettingsFile<CoreSettings> configFile,
    QuartzLogger logger
) : IRuntimeService {
    public Translator Translator { get; } = new();
    private SystemLanguage detected = SystemLanguage.Unknown;
    public void Initialize() {
        Translator.SetLog(logger.Msg);
        Translator.SetDispatcher(MainThread.Enqueue);
        Translator.Language = configFile.Data.Language;
        // Read the game's language here: Initialize runs on the main thread, the load below
        // does not, and Application.systemLanguage is main-thread only under Unity 6.
        if(!configFile.Data.LanguageAutoDetected) detected = GameLanguage.Detect();
        _ = LoadThenUpdate();
    }
    private async Task LoadThenUpdate() {
        try {
            LoadAprilOverrides();
            await Translator.Load(langPath);
            TryAutoLanguage();
            if(await LangUpdateService.FetchAsync(langPath) > 0) {
                await Translator.Load(langPath);
                TryAutoLanguage();
            }
            FinishAutoLanguage();
        } catch(System.Exception e) {
            Diag.Ignore(e);
            logger.Wrn($"[LangUpdate] startup refresh failed: {e.Message}");
        }
    }
    private void TryAutoLanguage() {
        if(configFile.Data.LanguageAutoDetected) return;
        string match = GameLanguage.Match(detected, Translator.GetLanguages());
        if(match == null) return;
        logger.Msg($"[Localization] first launch: game language is {detected}, using '{match}'");
        configFile.Data.Language = match;
        configFile.Data.LanguageAutoDetected = true;
        configFile.RequestSave();
        MainThread.Enqueue(() => {
            Translator.Language = match;
            TextLocalization.RefreshAll();
        });
    }
    // Only give up once a load actually produced languages, so a failed first fetch leaves
    // the detection pending for the next launch instead of silently locking in English.
    private void FinishAutoLanguage() {
        if(configFile.Data.LanguageAutoDetected) return;
        if(!Translator.GetLanguages().Any(l => l != Translator.FALLBACK_LANGUAGE)) return;
        logger.Msg($"[Localization] first launch: no translation for game language {detected}, keeping '{configFile.Data.Language}'");
        configFile.Data.LanguageAutoDetected = true;
        configFile.RequestSave();
    }
    private void LoadAprilOverrides() {
        try {
            string dir = Path.Combine(langPath, "AprilFools");
            if(!Directory.Exists(dir)) return;
            Dictionary<string, Dictionary<string, string>> result = new(StringComparer.Ordinal);
            foreach(string file in Directory.GetFiles(dir, "*.json")) {
                Newtonsoft.Json.Linq.JObject root = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(file));
                foreach(Newtonsoft.Json.Linq.JProperty language in root.Properties()) {
                    if(language.Value is not Newtonsoft.Json.Linq.JObject block) continue;
                    if(!result.TryGetValue(language.Name, out Dictionary<string, string> merged))
                        result[language.Name] = merged = new(StringComparer.Ordinal);
                    foreach(KeyValuePair<string, Newtonsoft.Json.Linq.JToken> kv in block) {
                        if(kv.Key.StartsWith("0", StringComparison.Ordinal)) continue;
                        if(kv.Value is Newtonsoft.Json.Linq.JArray) continue;
                        merged[kv.Key] = kv.Value?.ToString() ?? "";
                    }
                }
            }
            Translator.SetAprilOverrides(result);
            if(result.Count > 0) logger.Msg($"[Localization] April Fools overlay ready for {result.Count} language(s)");
        } catch(System.Exception e) {
            Diag.Ignore(e);
            logger.Wrn($"[Localization] April Fools overlay failed to load: {e.Message}");
        }
    }
    public void Dispose() { }
}

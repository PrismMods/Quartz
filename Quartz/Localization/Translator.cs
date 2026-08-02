using Newtonsoft.Json.Linq;
namespace Quartz.Localization;
public enum TranslationFailState {
    Success,
    UnknownCause,
    SomeFailure,
    ErrorReadingDirectory,
    FileDoesNotExist,
    NoValidTranslationFound,
}
public class Translator {
    private readonly string KTLKey;
    private readonly string ExpectedKTLValue;
    private volatile Dictionary<string, Dictionary<string, string>> translations = [];
    // Modules hand their language blocks over at load time, but Load() rebuilds the
    // dictionaries from disk and would drop every one of them — which is why a module
    // page's name showed English while core categories were translated. Keep the
    // blocks and re-apply them after every load, newest per source wins.
    private readonly Dictionary<string, string> mergedSources = new(StringComparer.Ordinal);
    private readonly object mergedLock = new();
    private volatile Dictionary<string, Dictionary<string, string[]>> translationsArr = [];
    public const string FALLBACK_LANGUAGE = "DEFAULT";
    public string Language {
        get;
        set {
            if(field == value) return;
            field = value;
            string lang = field;
            Post(() => OnLanguageChanged.Invoke(lang));
        }
    } = FALLBACK_LANGUAGE;
    public TranslationFailState FailState { get; private set; } = TranslationFailState.Success;
    private Action<string> logAction;
    public void SetLog(Action<string> action) => logAction = action;
    private Action<Action> dispatcher;
    public void SetDispatcher(Action<Action> action) => dispatcher = action;
    private void Post(Action action) {
        Action<Action> d = dispatcher;
        if(d == null) action();
        else d(action);
    }
    private void Log(string message) => logAction?.Invoke(message);
    public const string LOG_PREFIX = "[Translator] ";
    public const string LOG_PREFIX_WARNING = "[Translator Warning] ";
    public const string LOG_PREFIX_ERROR = "[Translator Error] ";
    public const string LOG_PREFIX_EXCEPTION = "[Translator Exception] ";
    public bool IsLoading { get; private set; } = false;
    public bool IsFail => FailState != TranslationFailState.Success;
    public bool IsSomeFail => FailState == TranslationFailState.SomeFailure;
    public bool IsDefault => (IsFail && FailState != TranslationFailState.SomeFailure) || IsLoading || Language == FALLBACK_LANGUAGE;
    public event Action OnLoadStart = delegate { };
    public event Action<TranslationFailState> OnLoadEnd = delegate { };
    public event Action<string> OnLanguageChanged = delegate { };
    public const string DEFAULT_KTL_KEY = "0KTL";
    public const string DEFAULT_EXPECTED_KTL_VALUE = "DO_NOT_TRANSLATE_THIS_KEY!";
    public Translator(string ktlKey = DEFAULT_KTL_KEY, string expectedKtlValue = DEFAULT_EXPECTED_KTL_VALUE) {
        KTLKey = ktlKey;
        ExpectedKTLValue = expectedKtlValue;
    }
    public async Task Load(string baseLangFolderPath) {
        if(IsLoading) return;
        OnLoadStart.Invoke();
        IsLoading = true;
        Log($"{LOG_PREFIX}Starting to load translations from: {baseLangFolderPath}");
        Dictionary<string, Dictionary<string, string>> newTranslations = [];
        Dictionary<string, Dictionary<string, string[]>> newTranslationsArr = [];
        string[] files;
        try {
            files = Directory.GetFiles(baseLangFolderPath, "*.json");
        } catch(Exception e) {
            FailState = TranslationFailState.ErrorReadingDirectory;
            Log($"{LOG_PREFIX_ERROR}Error reading directory: {baseLangFolderPath}");
            Log($"[Translator Exception] {e.GetType().Name}: {e.Message}");
            translations = newTranslations;
            translationsArr = newTranslationsArr;
            ReapplyMerged();
            Finish();
            return;
        }
        Log($"{LOG_PREFIX}Found {files.Length} translation files.");
        if(files.Length == 0) {
            FailState = TranslationFailState.FileDoesNotExist;
            Log($"{LOG_PREFIX_WARNING}No translation files found");
            translations = newTranslations;
            translationsArr = newTranslationsArr;
            ReapplyMerged();
            Finish();
            return;
        }
        foreach(var file in files) {
            try {
                using StreamReader reader = new(file);
                string jsonString = await reader.ReadToEndAsync();
                JObject jsonObject = JObject.Parse(jsonString);
                foreach(var property in jsonObject.Properties()) {
                    if(property.Value is not JObject block) {
                        FailState = TranslationFailState.SomeFailure;
                        Log($"{LOG_PREFIX_ERROR}Block is not an object in file: {file}, block: {property.Name}");
                        continue;
                    }
                    if(!block.TryGetValue(KTLKey, out var ktToken) || ktToken.ToString() != ExpectedKTLValue) {
                        Log($"{LOG_PREFIX}Invalid or missing {KTLKey} in file: {file}, block: {property.Name}, passing");
                        continue;
                    }
                    block.Remove(KTLKey);
                    var stringDict = new Dictionary<string, string>();
                    var arrayDict = new Dictionary<string, string[]>();
                    foreach(var kv in block) {
                        if(kv.Value is JArray arr) {
                            arrayDict[kv.Key] = arr.Select(v => v.ToString()).ToArray();
                        } else {
                            stringDict[kv.Key] = kv.Value?.ToString() ?? "";
                        }
                    }
                    if(stringDict.Count > 0) {
                        if(!newTranslations.TryGetValue(property.Name, out var existing))
                            newTranslations[property.Name] = existing = new Dictionary<string, string>();
                        foreach(var kv in stringDict) existing[kv.Key] = kv.Value;
                    }
                    if(arrayDict.Count > 0) {
                        if(!newTranslationsArr.TryGetValue(property.Name, out var existingArr))
                            newTranslationsArr[property.Name] = existingArr = new Dictionary<string, string[]>();
                        foreach(var kv in arrayDict) existingArr[kv.Key] = kv.Value;
                    }
                }
            } catch(Exception e) {
                FailState = TranslationFailState.SomeFailure;
                Log($"{LOG_PREFIX_ERROR}Error processing file: {file}");
                Log($"{LOG_PREFIX_EXCEPTION}{e.GetType().Name}: {e.Message}");
            }
        }
        if(newTranslations.Count == 0 && newTranslationsArr.Count == 0) {
            FailState = TranslationFailState.NoValidTranslationFound;
            Log($"{LOG_PREFIX_WARNING}No valid translations were found in any files.");
        } else if(FailState != TranslationFailState.SomeFailure) {
            FailState = TranslationFailState.Success;
        }
        translations = newTranslations
            .OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(kv => kv.Key, kv => kv.Value);
        translationsArr = newTranslationsArr
            .OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(kv => kv.Key, kv => kv.Value);
        ReapplyMerged();
        Finish();
    }
    public bool Merge(string json, string source) => Merge(json, source, remember: true);
    private void ReapplyMerged() {
        KeyValuePair<string, string>[] blocks;
        lock(mergedLock) {
            if(mergedSources.Count == 0) return;
            blocks = [.. mergedSources];
        }
        foreach(KeyValuePair<string, string> entry in blocks) Merge(entry.Value, entry.Key, remember: false);
    }
    private bool Merge(string json, string source, bool remember) {
        if(string.IsNullOrWhiteSpace(json)) return false;
        try {
            JObject root = JObject.Parse(json);
            int blocks = 0;
            foreach(var property in root.Properties()) {
                if(property.Value is not JObject block) continue;
                if(!block.TryGetValue(KTLKey, out var ktToken) || ktToken.ToString() != ExpectedKTLValue) {
                    Log($"{LOG_PREFIX_WARNING}Invalid or missing {KTLKey} in {source}, block: {property.Name}, passing");
                    continue;
                }
                if(!translations.TryGetValue(property.Name, out var strings))
                    translations[property.Name] = strings = [];
                if(!translationsArr.TryGetValue(property.Name, out var arrays))
                    translationsArr[property.Name] = arrays = [];
                foreach(var kv in block) {
                    if(kv.Key == KTLKey) continue;
                    if(kv.Value is JArray arr) arrays[kv.Key] = arr.Select(v => v.ToString()).ToArray();
                    else strings[kv.Key] = kv.Value?.ToString() ?? "";
                }
                blocks++;
            }
            if(blocks > 0) {
                if(remember) {
                    lock(mergedLock) mergedSources[source] = json;
                    Log($"{LOG_PREFIX}Merged {blocks} language block(s) from {source}.");
                }
                return true;
            }
            return false;
        } catch(Exception e) {
            Log($"{LOG_PREFIX_ERROR}Could not merge translations from {source}");
            Log($"{LOG_PREFIX_EXCEPTION}{e.GetType().Name}: {e.Message}");
            return false;
        }
    }
    private void Finish() {
        Log($"{LOG_PREFIX}Finished loading translations.");
        IsLoading = false;
        TranslationFailState state = FailState;
        Post(() => {
            try {
                OnLoadEnd.Invoke(state);
            } catch(Exception e) {
                Log($"{LOG_PREFIX_EXCEPTION}Exception during OnLoadEnd event: {e.GetType().Name}: {e.Message}");
            }
        });
    }
    public bool HasKey(string key) {
        if(IsDefault || string.IsNullOrEmpty(key)) return false;
        return (translations.TryGetValue(Language, out var langDict) && langDict.ContainsKey(key))
            || (translationsArr.TryGetValue(Language, out var langArr) && langArr.ContainsKey(key));
    }
    public bool HasKeyForLanguage(string key, string language) {
        if(string.IsNullOrEmpty(language) || language == FALLBACK_LANGUAGE || string.IsNullOrEmpty(key)) return false;
        return (translations.TryGetValue(language, out var langDict) && langDict.ContainsKey(key))
            || (translationsArr.TryGetValue(language, out var langArr) && langArr.ContainsKey(key));
    }
    public string Get(string key, string defaultValue) {
        if(IsDefault) return Quartz.Core.AprilFools.Rebrand(defaultValue);
        if(translations.TryGetValue(Language, out var langDict) && langDict.TryGetValue(key, out var val))
            return Quartz.Core.AprilFools.Rebrand(val);
        return Quartz.Core.AprilFools.Rebrand(defaultValue);
    }
    public string GetForLanguage(string key, string language, string defaultValue) {
        if(string.IsNullOrEmpty(language) || language == FALLBACK_LANGUAGE) return defaultValue;
        if(translations.TryGetValue(language, out var langDict) && langDict.TryGetValue(key, out var val)) return val;
        return defaultValue;
    }
    public string[] GetLanguages() {
        List<string> languages = [];
        if(IsFail) languages.Add(FALLBACK_LANGUAGE);
        languages.AddRange(translations.Keys);
        return [.. languages];
    }
    public string[] GetLanguageNativeNames() {
        List<string> names = [];
        if(IsFail) names.Add(FALLBACK_LANGUAGE);
        names.AddRange(translations.Keys
            .OrderBy(k => k, StringComparer.OrdinalIgnoreCase)
            .Select(lang => GetForLanguage("0NATIVELANG", lang, lang)));
        return [.. names];
    }
    public string GetArr(string key, int index, string defaultValue) {
        if(IsDefault) return defaultValue;
        if(translationsArr.TryGetValue(Language, out var lang)
            && lang.TryGetValue(key, out var values)
            && index >= 0 && index < values.Length) {
            return Quartz.Core.AprilFools.Rebrand(values[index]);
        }
        return Quartz.Core.AprilFools.Rebrand(defaultValue);
    }
    public int GetArrCount(string key) {
        if(IsDefault) return 0;
        if(translationsArr.TryGetValue(Language, out var lang) && lang.TryGetValue(key, out var values)) return values.Length;
        return 0;
    }
    // Prefix-based: a module registers one block per language, each under its own
    // source, so unloading has to drop them all.
    public void Forget(string sourcePrefix) {
        if(string.IsNullOrEmpty(sourcePrefix)) return;
        lock(mergedLock) {
            foreach(string key in mergedSources.Keys.Where(k => k.StartsWith(sourcePrefix, StringComparison.Ordinal)).ToList())
                mergedSources.Remove(key);
        }
    }
    public void Release() {
        lock(mergedLock) mergedSources.Clear();
        translations = [];
        translationsArr = [];
        logAction = null;
        OnLoadEnd = delegate { };
    }
}

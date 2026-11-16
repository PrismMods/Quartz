using Quartz.Async;
using Quartz.Core;
namespace Quartz.Localization;
/// <summary>The language list and actions both menus' Settings pages offer.</summary>
internal static class LanguageActions {
    internal static string[] Languages() => [.. MainCore.Tr.GetLanguages().OrderBy(x => x, StringComparer.OrdinalIgnoreCase)];
    internal static string LanguageName(string lang) {
        if(lang == Translator.FALLBACK_LANGUAGE) return "DEFAULT";
        string native = MainCore.Tr.GetForLanguage("0NATIVELANG", lang, lang);
        return $"{native} ({lang})";
    }
    internal static void Apply(string lang) {
        MainCore.Tr.Language = lang;
        MainCore.Conf.Language = lang;
        MainCore.Conf.LanguageAutoDetected = true;
        MainCore.ConfMgr.RequestSave();
        TextLocalization.RefreshAll();
    }
    /// <summary>Reads the language files again; <paramref name="done"/> runs on the main thread before the labels refresh, <paramref name="after"/> after.</summary>
    internal static void Reload(Action done, Action after = null) {
        _ = Task.Run(async () => {
            await MainCore.Tr.Load(MainCore.Paths.LangPath);
            MainThread.Enqueue(() => {
                done?.Invoke();
                TextLocalization.RefreshAll();
                after?.Invoke();
            });
        });
    }
}

using System;
using System.Collections.Generic;
using Quartz.Compat.Game;
using Quartz.Core;
using UnityEngine;
namespace Quartz.Localization;
public static class GameLanguage {
    private static readonly Refl.Member PersistenceLanguage = new(typeof(Persistence), "language");
    private static readonly Refl.Member RdStringLanguage = new(Refl.Type("RDString"), "language");
    // Persistence.language is the in-game setting (falling back to the OS one), so it is
    // the closest thing to what the player picked. RDString.language is that value clamped
    // to the languages the game itself ships, and stays default(Afrikaans) until
    // RDString.Setup() has run — ADOFAI has no Afrikaans, so that only means "not ready".
    public static SystemLanguage Detect() {
        if(PersistenceLanguage.Get(null) is SystemLanguage saved && saved != SystemLanguage.Unknown) return saved;
        if(RdStringLanguage.Get(null) is SystemLanguage active && active != SystemLanguage.Afrikaans) return active;
        try {
            return Application.systemLanguage;
        } catch(Exception e) {
            Diag.Ignore(e);
            return SystemLanguage.Unknown;
        }
    }
    private static readonly Dictionary<SystemLanguage, string[]> Candidates = new() {
        [SystemLanguage.Afrikaans] = ["af-ZA", "af"],
        [SystemLanguage.Arabic] = ["ar-SA", "ar"],
        [SystemLanguage.Basque] = ["eu-ES", "eu"],
        [SystemLanguage.Belarusian] = ["be-BY", "be"],
        [SystemLanguage.Bulgarian] = ["bg-BG", "bg"],
        [SystemLanguage.Catalan] = ["ca-ES", "ca"],
        [SystemLanguage.Chinese] = ["zh-CN", "zh-Hans", "zh-TW", "zh-Hant", "zh"],
        [SystemLanguage.ChineseSimplified] = ["zh-CN", "zh-Hans", "zh-SG", "zh"],
        [SystemLanguage.ChineseTraditional] = ["zh-TW", "zh-Hant", "zh-HK", "zh"],
        [SystemLanguage.Czech] = ["cs-CZ", "cs"],
        [SystemLanguage.Danish] = ["da-DK", "da"],
        [SystemLanguage.Dutch] = ["nl-NL", "nl"],
        [SystemLanguage.English] = ["en-US", "en-GB", "en"],
        [SystemLanguage.Estonian] = ["et-EE", "et"],
        [SystemLanguage.Faroese] = ["fo-FO", "fo"],
        [SystemLanguage.Finnish] = ["fi-FI", "fi"],
        [SystemLanguage.French] = ["fr-FR", "fr-CA", "fr"],
        [SystemLanguage.German] = ["de-DE", "de"],
        [SystemLanguage.Greek] = ["el-GR", "el"],
        [SystemLanguage.Hebrew] = ["he-IL", "he", "iw"],
        [SystemLanguage.Hindi] = ["hi-IN", "hi"],
        [SystemLanguage.Hungarian] = ["hu-HU", "hu"],
        [SystemLanguage.Icelandic] = ["is-IS", "is"],
        [SystemLanguage.Indonesian] = ["id-ID", "id", "in"],
        [SystemLanguage.Italian] = ["it-IT", "it"],
        [SystemLanguage.Japanese] = ["ja-JP", "ja"],
        [SystemLanguage.Korean] = ["ko-KR", "ko"],
        [SystemLanguage.Latvian] = ["lv-LV", "lv"],
        [SystemLanguage.Lithuanian] = ["lt-LT", "lt"],
        [SystemLanguage.Norwegian] = ["nb-NO", "nb", "no", "nn-NO", "nn"],
        [SystemLanguage.Polish] = ["pl-PL", "pl"],
        [SystemLanguage.Portuguese] = ["pt-BR", "pt-PT", "pt"],
        [SystemLanguage.Romanian] = ["ro-RO", "ro"],
        [SystemLanguage.Russian] = ["ru-RU", "ru"],
        [SystemLanguage.SerboCroatian] = ["sr-RS", "sr", "hr-HR", "hr", "sh"],
        [SystemLanguage.Slovak] = ["sk-SK", "sk"],
        [SystemLanguage.Slovenian] = ["sl-SI", "sl"],
        [SystemLanguage.Spanish] = ["es-ES", "es-419", "es-MX", "es"],
        [SystemLanguage.Swedish] = ["sv-SE", "sv"],
        [SystemLanguage.Thai] = ["th-TH", "th"],
        [SystemLanguage.Turkish] = ["tr-TR", "tr"],
        [SystemLanguage.Ukrainian] = ["uk-UA", "uk"],
        [SystemLanguage.Vietnamese] = ["vi-VN", "vi"],
    };
    public static string[] CandidateCodes(SystemLanguage language) =>
        Candidates.TryGetValue(language, out string[] codes) ? codes : [];
    // Exact tag first across every candidate, then the primary subtag, so a Brazilian
    // client still lands on a pt-PT translation rather than falling back to English.
    public static string Match(SystemLanguage language, IEnumerable<string> available) {
        if(available == null) return null;
        string[] codes = CandidateCodes(language);
        if(codes.Length == 0) return null;
        List<string> pool = [];
        foreach(string code in available) {
            if(string.IsNullOrWhiteSpace(code) || code == Translator.FALLBACK_LANGUAGE) continue;
            pool.Add(code);
        }
        foreach(string candidate in codes)
            foreach(string code in pool)
                if(string.Equals(code, candidate, StringComparison.OrdinalIgnoreCase)) return code;
        foreach(string candidate in codes)
            foreach(string code in pool)
                if(string.Equals(Primary(code), Primary(candidate), StringComparison.OrdinalIgnoreCase)) return code;
        return null;
    }
    private static string Primary(string code) {
        int dash = code.IndexOf('-');
        return dash < 0 ? code : code[..dash];
    }
}

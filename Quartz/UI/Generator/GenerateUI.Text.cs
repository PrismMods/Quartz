using System.Globalization;
using Quartz.Core;
using Quartz.Localization;
using Quartz.Resource;
using Quartz.UI.Objects.Impl;
using Quartz.UI.Utility;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static UnityEngine.EventSystems.PointerEventData;
using GTweens.Tweens;
using GTweens.Easings;
using GTweens.Extensions;
using GTweens.Builders;
using Quartz.Tween;
using GTweenExtensions = GTweens.Extensions.GTweenExtensions;
using TMPro;
using Quartz.Compat.Game;
namespace Quartz.UI.Generator;
public static partial class GenerateUI {
    public static TextMeshProUGUI AddText(Transform parent, bool noPad = false) => CreateText(parent, 24f, false, noPad);
    public static TextMeshProUGUI AddMutedText(Transform parent, float size = 17f, float alpha = 0.45f, bool noPad = false) {
        TextMeshProUGUI text = AddText(parent, noPad);
        text.fontSize = size;
        text.color = new Color(1f, 1f, 1f, alpha);
        return text;
    }
    public static TextMeshProUGUI AddLocalizedMutedText(
        Transform parent,
        string key,
        string defaultValue,
        float size = 17f,
        float alpha = 0.45f,
        bool noPad = false
    ) => Localize(AddMutedText(parent, size, alpha, noPad), key, defaultValue);
    public static TextMeshProUGUI AddTextH1(Transform parent) => CreateText(parent, 32f, true, true);
    public static TextMeshProUGUI Localize(TextMeshProUGUI text, string key, string defaultValue) {
        if(text == null) return null;
        text.text = defaultValue;
        text.gameObject.AddComponent<TextLocalization>().Init(key, defaultValue);
        return text;
    }
    public static TextMeshProUGUI LocalizeById(
        TextMeshProUGUI text,
        string id,
        string defaultValue,
        string suffix = null
    ) {
        string key = LocaleKeyFromId(id, suffix);
        if(string.IsNullOrEmpty(key) || string.IsNullOrEmpty(defaultValue)) return text;
        return Localize(text, key, defaultValue);
    }
    public static string LocaleKeyFromId(string id, string suffix = null) {
        if(string.IsNullOrWhiteSpace(id)) return null;
        string key = NormalizeLocaleKey(id);
        key = StripIndexedPrefix(key, "PANEL");
        key = StripIndexedPrefix(key, "PRACTICE");
        if(key.StartsWith("PANEL_PICK_")) {
            key = "PANEL_STAT_" + key["PANEL_PICK_".Length..];
        }
        if(!string.IsNullOrEmpty(suffix)) key += "_" + NormalizeLocaleKey(suffix);
        return key;
    }
    public static string LocaleKeyFromText(string prefix, string text) {
        string key = NormalizeLocaleKey(text);
        return string.IsNullOrEmpty(prefix) ? key : NormalizeLocaleKey(prefix) + "_" + key;
    }
    private static string StripIndexedPrefix(string key, string prefix) {
        if(key == null || !key.StartsWith(prefix)) return key;
        int i = prefix.Length;
        while(i < key.Length && char.IsDigit(key[i])) i++;
        return i > prefix.Length && i < key.Length && key[i] == '_' ? prefix + key[i..] : key;
    }
    private static string NormalizeLocaleKey(string value) {
        if(string.IsNullOrWhiteSpace(value)) return null;
        List<char> chars = [];
        bool lastUnderscore = false;
        foreach(char raw in value.Trim().ToUpperInvariant()) {
            char c = char.IsLetterOrDigit(raw) ? raw : '_';
            if(c == '_') {
                if(lastUnderscore) continue;
                lastUnderscore = true;
            } else {
                lastUnderscore = false;
            }
            chars.Add(c);
        }
        while(chars.Count > 0 && chars[^1] == '_') chars.RemoveAt(chars.Count - 1);
        return chars.Count == 0 ? null : new string(chars.ToArray());
    }
    private static TextMeshProUGUI CreateText(Transform parent, float size, bool bold, bool noPad) {
        GameObject obj = new("Text");
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.AddComponent<RectTransform>();
        rect.anchorMin = new(0f, 0f);
        rect.anchorMax = new(1f, 1f);
        rect.offsetMin = new(noPad ? 0f : 16f, 0f);
        rect.offsetMax = Vector2.zero;
        TextMeshProUGUI tmp = obj.AddComponent<TextMeshProUGUI>();
        tmp.font = FontManager.Current;
        tmp.fontSize = size;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Left;
        tmp.verticalAlignment = VerticalAlignmentOptions.Middle;
        tmp.characterSpacing = -3f;
        return tmp;
    }
    public static string Tr(string key, string def) => MainCore.Tr.Get(key, def);
}

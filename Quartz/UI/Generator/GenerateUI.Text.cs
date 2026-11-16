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
    public static TextMeshProUGUI Header(Transform parent, string text, string id = null) {
        TextMeshProUGUI tmp = AddTextH1(Row(parent));
        tmp.text = text;
        return string.IsNullOrEmpty(id) ? tmp : LocalizeById(tmp, id, text);
    }
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
    public static string LocaleKeyFromId(string id, string suffix = null) => LocaleKeys.FromId(id, suffix);
    public static string LocaleKeyFromText(string prefix, string text) => LocaleKeys.FromText(prefix, text);
    private static TextMeshProUGUI CreateText(Transform parent, float size, bool bold, bool noPad) {
        GameObject obj = new("Text");
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.AddComponent<RectTransform>();
        rect.anchorMin = new(0f, 0f);
        rect.anchorMax = new(1f, 1f);
        rect.offsetMin = new(noPad ? 0f : 16f, 0f);
        rect.offsetMax = Vector2.zero;
        TextMeshProUGUI tmp = obj.AddComponent<TextMeshProUGUI>();
        tmp.font = FontManager.MenuFontAsset;
        tmp.fontSize = size;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Left;
        tmp.verticalAlignment = VerticalAlignmentOptions.Middle;
        tmp.characterSpacing = -3f;
        return tmp;
    }
    // One line in a fixed slot: shrink, then ellipsize, never wrap.
    public static TMP_Text FitLine(TMP_Text text, float minSize = 16f) => UnityUtils.FitLine(text, minSize);
    // Wrapped text: the row grows to fit.
    public static T WrapGrow<T>(T text, float padding = 6f) where T : TMP_Text {
        TextCompat.Wrap(text);
        text.enableAutoSizing = false;
        GrowRow.Attach(text, padding);
        return text;
    }
    public static string Tr(string key, string def) => MainCore.Tr.Get(key, def);
}

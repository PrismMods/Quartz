using System.Globalization;
using Quartz.Compat.Game;
using Quartz.Core;
using Quartz.Features;
using Quartz.Localization;
using Quartz.Resource;
using Quartz.UI.Generator;
using Quartz.UI.Nav;
using Quartz.UI.Objects.Impl;
using Quartz.UI.Utility;
using Quartz.Utility;
using TMPro;
using UnityEngine;
namespace Quartz.UI.Factory.Page;
internal static class PageFeatures {
    private sealed class FilterRow {
        public GameObject[] Objects;
        public string Haystack;
    }
    private sealed class FilterSection {
        public GenerateUI.CollapsibleSection Section;
        public readonly List<FilterRow> Rows = [];
        public bool RestoreExpanded;
    }
    private static bool filtering;
    private static string filterQuery = "";
    private static readonly List<FilterSection> filterSections = [];
    public static void Create(RectTransform parent) {
        filterSections.Clear();
        RectTransform content = Quartz.UI.Factory.PageFactory.CreateScrollablePage(parent);
        GenerateUI.AddTextH1(GenerateUI.Row(content.transform))
            .gameObject.AddComponent<TextLocalization>().Init("FEATURES", "Features");
        GenerateUI.AddMutedText(GenerateUI.Row(content.transform, 74f), 17f, 0.45f, true)
            .gameObject.AddComponent<TextLocalization>().Init(
                "FEATURES_HINT",
                "Switch any feature on or off. An off feature is fully unloaded. Turn a tab off to hide it from the sidebar."
            );
        Filter(content);
        Tabs(content);
        if(!string.IsNullOrEmpty(filterQuery)) ApplyFilter(filterQuery);
    }
    private static void Filter(RectTransform content) {
        UIInput input = GenerateUI.Input(
            GenerateUI.Row(content.transform),
            "",
            filterQuery,
            ApplyFilter,
            "Filter features",
            MainCore.Spr.Get(UISprite.MagnifyingGlass128),
            "features_filter"
        );
        input.Placeholder.gameObject.AddComponent<TextLocalization>().Init("FEATURES_FILTER", "Filter features");
        input.InputField.characterLimit = 40;
    }
    private static string Norm(string text) {
        string normalized = StringUtils.Normalize(text);
        if(MainCore.Conf.Language == "ko-KR" && !string.IsNullOrEmpty(normalized))
            normalized = StringUtils.NormalizeToHangulChosung(normalized);
        return normalized;
    }
    private static void ApplyFilter(string raw) {
        filterQuery = raw ?? "";
        string query = Norm(filterQuery);
        bool active = !string.IsNullOrWhiteSpace(query);
        if(active && !filtering)
            foreach(FilterSection fs in filterSections)
                if(fs.Section != null) fs.RestoreExpanded = fs.Section.Expanded;
        filtering = active;
        foreach(FilterSection fs in filterSections) {
            int matches = 0;
            foreach(FilterRow row in fs.Rows) {
                bool match = !active || row.Haystack.Contains(query);
                if(match) matches++;
                foreach(GameObject go in row.Objects)
                    if(go != null) go.SetActive(match);
            }
            if(fs.Section?.Body == null) continue;
            bool show = !active || matches > 0;
            fs.Section.Section.gameObject.SetActive(show);
            if(active && show) fs.Section.SetExpanded(true, false, false);
            else if(!active) fs.Section.SetExpanded(fs.RestoreExpanded, false, false);
        }
    }
    private static void Tabs(RectTransform content) {
        foreach(NavCategory category in FeatureCategories.Togglable()) {
            string key = category.Key;
            List<FeatureService.Handle> features = FeatureCategories.In(key);
            string tabName = MainCore.Tr.Get(category.LocaleKey, category.Title);
            if(features.Count == 0) {
                GenerateUI.Toggle(
                    GenerateUI.Row(content.transform, 64f),
                    true,
                    FeatureCategories.IsEnabled(key),
                    v => FeatureCategories.SetEnabled(key, v),
                    tabName,
                    "features_tab_" + key,
                    52f
                );
                continue;
            }
            GenerateUI.CollapsibleSection section = GenerateUI.Collapsible(
                content.transform,
                category.Title,
                false,
                v => FeatureCategories.SetEnabled(key, v),
                FeatureCategories.IsEnabled(key)
            );
            int on = features.Count(static h => h.Enabled);
            Retitle(section, category, on, features.Count);
            FilterSection fs = new() { Section = section, RestoreExpanded = section.Expanded };
            filterSections.Add(fs);
            foreach(FeatureService.Handle handle in features) {
                fs.Rows.Add(new FilterRow {
                    Objects = Row(section.Body, handle),
                    Haystack = Norm(tabName + " " + MainCore.Tr.Get(handle.Info.NameKey, handle.Name) + " " + handle.Id),
                });
            }
        }
    }
    private static GameObject[] Row(Transform parent, FeatureService.Handle handle) {
        string id = handle.Id;
        RectTransform row = GenerateUI.Row(parent, 64f);
        UIToggle toggle = GenerateUI.Toggle(
            row,
            true,
            handle.Enabled,
            v => FeatureService.SetEnabled(id, v),
            MainCore.Tr.Get(handle.Info.NameKey, handle.Name),
            "feature_" + id,
            52f
        );
        toggle.Label?.GetComponent<TextLocalization>()?.Init(handle.Info.NameKey, handle.Name);
        string error = handle.Error;
        if(error == null) return [row.gameObject];
        RectTransform statusRow = GenerateUI.Row(parent, 96f);
        TextMeshProUGUI status = GenerateUI.AddMutedText(statusRow, 15f, 0.45f, true);
        TextCompat.SetWrap(status, true);
        status.overflowMode = TextOverflowModes.Ellipsis;
        status.verticalAlignment = VerticalAlignmentOptions.Top;
        status.color = UIColors.SoftRed;
        status.text = error;
        statusRow.AddToolTip(error.Length > 900 ? error[..900] + "…" : error);
        return [row.gameObject, statusRow.gameObject];
    }
    private static void Retitle(GenerateUI.CollapsibleSection section, NavCategory category, int on, int total) {
        Transform bar = section.HeaderObj?.transform.Find("Bar");
        if(bar == null) return;
        TextMeshProUGUI label = bar.Find("Label")?.GetComponent<TextMeshProUGUI>();
        if(label != null) {
            label.GetComponent<TextLocalization>()?.Init(category.LocaleKey, category.Title);
            label.rectTransform.offsetMax = new Vector2(-170f, 0f);
        }
        GameObject countObj = new("Count");
        countObj.transform.SetParent(bar, false);
        RectTransform rect = countObj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 0f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 0.5f);
        rect.anchoredPosition = new Vector2(-92f, 0f);
        rect.sizeDelta = new Vector2(70f, 0f);
        TextMeshProUGUI count = countObj.AddComponent<TextMeshProUGUI>();
        count.font = FontManager.MenuFontAsset;
        count.fontSize = 16f;
        count.color = new Color(1f, 1f, 1f, 0.45f);
        count.alignment = TextAlignmentOptions.Right;
        count.verticalAlignment = VerticalAlignmentOptions.Middle;
        count.raycastTarget = false;
        count.text = on.ToString(CultureInfo.InvariantCulture)
            + "/" + total.ToString(CultureInfo.InvariantCulture);
    }
}

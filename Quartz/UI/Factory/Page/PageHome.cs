using System.Globalization;
using Quartz.Compat.Game;
using Quartz.Core;
using Quartz.Modules;
using Quartz.UI.Generator;
using Quartz.UI.Home;
using Quartz.UI.Nav;
using Quartz.UI.Objects.Impl;
using Quartz.Update;
using TMPro;
using UnityEngine;
namespace Quartz.UI.Factory.Page;
internal static class PageHome {
    private static GameObject bannerRow;
    private static TextMeshProUGUI bannerText;
    private static GameObject bannerButton;
    public static void Create(RectTransform parent) {
        bannerRow = null;
        bannerText = null;
        bannerButton = null;
        RectTransform content = Quartz.UI.Factory.PageFactory.CreateScrollablePage(parent);
        GenerateUI.Localize(GenerateUI.AddTextH1(GenerateUI.Row(content.transform)), "HOME", "Home");
        TextMeshProUGUI version = GenerateUI.AddMutedText(GenerateUI.Row(content.transform, 30f), 17f, 0.45f, true);
        version.text = Info.DisplayName + " — " + Info.DisplayVersion;
        Banner(content);
        RefreshUpdateBanner();
        UpdateService.OnChanged -= RefreshUpdateBanner;
        UpdateService.OnChanged += RefreshUpdateBanner;
        Setup(content);
        Cards(content);
        TextMeshProUGUI hint = GenerateUI.AddMutedText(GenerateUI.Row(content.transform, 60f), 16f, 0.4f, true);
        TextCompat.Wrap(hint);
        hint.rectTransform.offsetMax = new Vector2(-250f, 0f);
        GenerateUI.Localize(
            hint,
            "HOME_HINT",
            "Everything Quartz does lives in the tabs on the left. Search finds any setting by name."
        );
    }
    private static void Setup(RectTransform content) {
        int installed = ModuleService.Modules.Count;
        int active = 0;
        int updates = 0;
        foreach(ModuleService.Handle handle in ModuleService.Modules) {
            if(handle.Loaded && handle.Enabled) active++;
            if(PageModules.UpdateFor(handle) != null) updates++;
        }
        RectTransform strip = HomeUI.Grid(content.transform);
        HomeUI.Stat(strip, installed.ToString(CultureInfo.InvariantCulture),
            MainCore.Tr.Get("HOME_STAT_MODULES", "Modules installed"),
            static () => Go(CorePages.ModulesPageKey));
        HomeUI.Stat(strip, active.ToString(CultureInfo.InvariantCulture),
            MainCore.Tr.Get("HOME_STAT_ACTIVE", "Active"),
            static () => Go(CorePages.ModulesPageKey));
        HomeUI.Stat(strip, Quartz.Addons.AddonService.Addons.Count.ToString(CultureInfo.InvariantCulture),
            MainCore.Tr.Get("HOME_STAT_ADDONS", "Addons"),
            static () => Go(CorePages.AddonsPageKey));
        if(updates > 0) {
            HomeUI.Stat(strip, updates.ToString(CultureInfo.InvariantCulture),
                MainCore.Tr.Get("HOME_STAT_UPDATES", "Updates available"),
                static () => Go(CorePages.ModulesPageKey));
        }
        if(installed > 0) return;
        RectTransform emptyGrid = HomeUI.Grid(content.transform);
        Transform empty = HomeUI.Card(emptyGrid, MainCore.Tr.Get("HOME_SETUP", "Your setup"));
        HomeUI.Line(empty, MainCore.Tr.Get(
            "HOME_SETUP_EMPTY",
            "No modules yet — every feature lives in one. Open Modules to add some."
        ));
        RectTransform buttonRow = GenerateUI.Row(empty, 56f);
        GenerateUI.ButtonRow(buttonRow);
        UIButton open = GenerateUI.Button(buttonRow, static () => Go(CorePages.ModulesPageKey),
            "Open Modules", "home_open_modules", 0f);
        GenerateUI.FixWidth(open, 200f);
    }
    private static void Cards(RectTransform content) {
        IReadOnlyList<HomeCard> cards = HomeRegistry.Visible();
        for(int i = 0; i < cards.Count; i += 2) {
            RectTransform grid = HomeUI.Grid(content.transform);
            Build(grid, cards[i]);
            if(i + 1 < cards.Count) Build(grid, cards[i + 1]);
            else HomeUI.Spacer(grid);
        }
    }
    private static void Build(Transform grid, HomeCard card) {
        Transform body = HomeUI.Card(grid, card.LocaleKey == null
            ? card.Title
            : MainCore.Tr.Get(card.LocaleKey, card.Title));
        try {
            card.Build(body as RectTransform);
        } catch(Exception e) {
            MainCore.Log.Err($"[Home] card '{card.Key}' failed to build: {e}");
            HomeUI.Line(body, MainCore.Tr.Get("HOME_CARD_FAILED", "This card failed to build — see the log."));
        }
    }
    private static void Go(string pageKey) {
        int state = NavRegistry.StateFor(pageKey);
        if(state >= 0) MenuFactory.SetState(state);
    }
    private static void Banner(RectTransform content) {
        RectTransform row = GenerateUI.Row(content.transform, 64f);
        bannerRow = row.gameObject;
        RectTransform bg = GenerateUI.BackGround(250f);
        bg.SetParent(row, false);
        bg.GetComponent<UnityEngine.UI.Image>().color = Color.Lerp(UIColors.PanelBG, UIColors.ObjectActive, 0.3f);
        bannerText = GenerateUI.AddText(bg, true);
        bannerText.fontSize = 18f;
        bannerText.alignment = TextAlignmentOptions.Left;
        bannerText.verticalAlignment = VerticalAlignmentOptions.Middle;
        bannerText.overflowMode = TextOverflowModes.Ellipsis;
        bannerText.rectTransform.offsetMin = new Vector2(18f, 0f);
        bannerText.rectTransform.offsetMax = new Vector2(-190f, 0f);
        UIButton open = GenerateUI.Button(row, static () => Go(CorePages.SettingsPageKey),
            "Open Settings", "home_update_open");
        RectTransform rect = open.Rect;
        rect.anchorMin = new Vector2(1f, 0.5f);
        rect.anchorMax = new Vector2(1f, 0.5f);
        rect.pivot = new Vector2(1f, 0.5f);
        rect.sizeDelta = new Vector2(170f, 46f);
        rect.anchoredPosition = new Vector2(-258f, 0f);
        bannerButton = rect.gameObject;
    }
    private static void RefreshUpdateBanner() {
        if(bannerRow == null) return;
        UpdateStatus status = UpdateService.Status;
        bool show = status is UpdateStatus.Available or UpdateStatus.Installed;
        bannerRow.SetActive(show);
        if(!show) return;
        bannerText.text = status == UpdateStatus.Available
            ? GenerateUI.Tr("HOME_UPDATE_AVAILABLE", "An update is available — see Settings.")
            : GenerateUI.Tr("HOME_UPDATE_INSTALLED", "An update was installed. Restart the game to use it.");
        bannerButton.SetActive(status == UpdateStatus.Available);
    }
}

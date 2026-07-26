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
    private static TextMeshProUGUI updateText;
    public static void Create(RectTransform parent) {
        updateText = null;
        RectTransform content = Quartz.UI.Factory.PageFactory.CreateScrollablePage(parent);
        GenerateUI.Localize(GenerateUI.AddTextH1(GenerateUI.Row(content.transform)), "HOME", "Home");
        TextMeshProUGUI version = GenerateUI.AddMutedText(GenerateUI.Row(content.transform, 30f), 17f, 0.45f, true);
        version.text = Info.Name + " " + Info.DisplayVersion;
        RectTransform updateRow = GenerateUI.Row(content.transform, 34f);
        updateText = GenerateUI.AddMutedText(updateRow, 16f, 0.45f, true);
        updateText.overflowMode = TextOverflowModes.Ellipsis;
        RefreshUpdateText();
        UpdateService.OnChanged -= RefreshUpdateText;
        UpdateService.OnChanged += RefreshUpdateText;
        RectTransform actions = GenerateUI.Row(content.transform);
        GenerateUI.ButtonRow(actions);
        Jump(actions, "Modules", "home_go_modules", CorePages.ModulesPageKey);
        Jump(actions, "Settings", "home_go_settings", CorePages.SettingsPageKey);
        Jump(actions, "Search", "home_go_search", CorePages.SearchPageKey);
        Jump(actions, "Help", "home_go_help", "help.faq");
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
        foreach(ModuleService.Handle handle in ModuleService.Modules)
            if(handle.Loaded && handle.Enabled) active++;
        RectTransform strip = HomeUI.Grid(content.transform, HomeUI.StatHeight);
        HomeUI.Stat(strip, installed.ToString(CultureInfo.InvariantCulture),
            MainCore.Tr.Get("HOME_STAT_MODULES", "Modules installed"));
        HomeUI.Stat(strip, active.ToString(CultureInfo.InvariantCulture),
            MainCore.Tr.Get("HOME_STAT_ACTIVE", "Active"));
        HomeUI.Stat(strip, Quartz.Addons.AddonService.Addons.Count.ToString(CultureInfo.InvariantCulture),
            MainCore.Tr.Get("HOME_STAT_ADDONS", "Addons"));
        if(installed > 0) return;
        RectTransform emptyGrid = HomeUI.Grid(content.transform, 90f);
        Transform empty = HomeUI.Card(emptyGrid, MainCore.Tr.Get("HOME_SETUP", "Your setup"));
        HomeUI.Line(empty, MainCore.Tr.Get(
            "HOME_SETUP_EMPTY",
            "No modules yet — every feature lives in one. Open Modules to add some."
        ));
    }
    private static void Cards(RectTransform content) {
        IReadOnlyList<HomeCard> cards = HomeRegistry.Visible();
        for(int i = 0; i < cards.Count; i += 2) {
            RectTransform grid = HomeUI.Grid(content.transform, HomeUI.CardHeight);
            Build(grid, cards[i]);
            if(i + 1 < cards.Count) Build(grid, cards[i + 1]);
            else HomeUI.Card(grid, null);
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
    private static void Jump(RectTransform row, string label, string id, string pageKey) {
        UIButton button = GenerateUI.Button(row, () => {
            int state = NavRegistry.StateFor(pageKey);
            if(state >= 0) MenuFactory.SetState(state);
        }, label, id);
        GenerateUI.FixWidth(button, 170f);
    }
    private static void RefreshUpdateText() {
        if(updateText == null) return;
        updateText.text = UpdateService.Status switch {
            UpdateStatus.Available => GenerateUI.Tr("HOME_UPDATE_AVAILABLE", "An update is available — see Settings."),
            UpdateStatus.Installed => GenerateUI.Tr("HOME_UPDATE_INSTALLED", "An update was installed. Restart the game to use it."),
            _ => "",
        };
    }
}

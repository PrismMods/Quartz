using System.Globalization;
using Quartz.Async;
using Quartz.Compat.Game;
using Quartz.Core;
using Quartz.IO;
using Quartz.Features;
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
        ResetCard(content);
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
        int total = FeatureService.Features.Count;
        int on = 0;
        foreach(FeatureService.Handle handle in FeatureService.Features)
            if(handle.Enabled) on++;
        RectTransform strip = HomeUI.Grid(content.transform);
        HomeUI.Stat(strip, on.ToString(CultureInfo.InvariantCulture) + "/" + total.ToString(CultureInfo.InvariantCulture),
            MainCore.Tr.Get("HOME_STAT_FEATURES", "Features on"),
            static () => Go(CorePages.FeaturesPageKey));
        HomeUI.Stat(strip, Quartz.Addons.AddonService.Addons.Count.ToString(CultureInfo.InvariantCulture),
            MainCore.Tr.Get("HOME_STAT_ADDONS", "Addons"),
            static () => Go(CorePages.AddonsPageKey));
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
    private static void ResetCard(RectTransform content) {
        RectTransform grid = HomeUI.Grid(content.transform);
        Transform card = HomeUI.Card(grid, MainCore.Tr.Get("HOME_RESET", "Reset"));
        HomeUI.Line(card, MainCore.Tr.Get(
            "HOME_RESET_DESC",
            "Reset puts every setting back to its default. Clean also empties key layouts, panels and key lists. Feature switches, addons and other profiles stay. Restart the game afterwards."
        ));
        RectTransform buttonRow = GenerateUI.Row(card, 56f);
        GenerateUI.ButtonRow(buttonRow);
        ConfirmButton(buttonRow, "Reset All Settings", "home_reset_all", false);
        ConfirmButton(buttonRow, "Clean", "home_reset_clean", true);
        HomeUI.Spacer(grid);
    }
    // First click arms the button; the second one runs it.
    private static void ConfirmButton(RectTransform row, string text, string id, bool clean) {
        bool armed = false;
        UIButton button = null;
        button = GenerateUI.Button(row, () => {
            if(button == null) return;
            if(!armed) {
                armed = true;
                button.Label.text = GenerateUI.Tr("HOME_RESET_CONFIRM", "Click again to confirm");
                button.RestColor = static () => UIColors.SoftRed;
                button.Background.color = UIColors.SoftRed;
                return;
            }
            button.SetBlocked(true);
            MainThread.Enqueue(() => {
                if(ProfileManager.ResetLive(clean)) UICore.Rebuild(true);
                else button.Label.text = GenerateUI.Tr("HOME_RESET_FAILED", "Failed — see the log");
            });
        }, text, id, 0f).SetSecondary();
        GenerateUI.FixWidth(button, 230f);
    }
    private static void Go(string pageKey) {
        int state = NavRegistry.StateFor(pageKey);
        if(state >= 0) MenuFactory.SetState(state);
    }
    private static void Banner(RectTransform content) {
        RectTransform row = GenerateUI.Row(content.transform, 64f);
        bannerRow = row.gameObject;
        RectTransform bg = GenerateUI.BackGround(0f);
        bg.SetParent(row, false);
        bg.GetComponent<UnityEngine.UI.Image>().color = Color.Lerp(UIColors.PanelBG, UIColors.ObjectActive, 0.3f);
        bannerText = GenerateUI.AddText(bg, true);
        bannerText.fontSize = 18f;
        bannerText.alignment = TextAlignmentOptions.Left;
        bannerText.verticalAlignment = VerticalAlignmentOptions.Middle;
        bannerText.overflowMode = TextOverflowModes.Ellipsis;
        bannerText.rectTransform.offsetMin = new Vector2(18f, 0f);
        bannerText.rectTransform.offsetMax = new Vector2(-200f, 0f);
        UIButton open = GenerateUI.Button(row, static () => Go(CorePages.SettingsPageKey),
            "Open Settings", "home_update_open");
        RectTransform rect = open.Rect;
        rect.anchorMin = new Vector2(1f, 0.5f);
        rect.anchorMax = new Vector2(1f, 0.5f);
        rect.pivot = new Vector2(1f, 0.5f);
        rect.sizeDelta = new Vector2(170f, 46f);
        rect.anchoredPosition = new Vector2(-18f, 0f);
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

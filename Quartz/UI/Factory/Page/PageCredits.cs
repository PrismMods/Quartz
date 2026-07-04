using Quartz.Core;
using Quartz.Localization;
using Quartz.Resource;
using UnityEngine;
using UnityEngine.UI;

using TMPro;

namespace Quartz.UI.Factory.Page;

internal static class PageCredits {
    public static void Create(RectTransform parent) {
        var logoImg = CenteredRect(parent, "Logo", 260, 260, 175).gameObject.AddComponent<Image>();
        logoImg.sprite = MainCore.Spr.Get(UISprite.QuartzLogo);
        logoImg.preserveAspect = true;

        var tmp = CenteredText(parent, "Title", 800, 60, -10, 38);
        tmp.text = "Quartz";

        var subtitleTmp = CenteredText(parent, "Subtitle", 800, 40, -60, 20);
        subtitleTmp.text = "by koren, sbrothers7, and more.";
        subtitleTmp.color = new Color(1f, 1f, 1f, 0.45f);
        subtitleTmp.gameObject.AddComponent<TextLocalization>().Init("CREDITS_SUBTITLE", "by koren, sbrothers7, and more.");

        string creditsBody =
            "<color=#FFFFFF66>UI based on Overlayer (modlist.org)</color>\n" +
            "<color=#FFFFFF88>Thank you for using Quartz.</color>\n" +
            "<size=12><color=#FFFFFF33>\nLicensed under GPLv3</color></size>";

        var creditsTmp = CenteredText(parent, "Credits", 900, 220, -180, 26);
        creditsTmp.text = creditsBody;
        creditsTmp.lineSpacing = 18;
        creditsTmp.gameObject.AddComponent<TextLocalization>().Init("CREDITS_BODY", creditsBody);
    }

    // Center-anchored fixed-size block at (0, y).
    private static RectTransform CenteredRect(Transform parent, string name, float w, float h, float y) {
        GameObject obj = new(name);
        obj.transform.SetParent(parent, false);

        var rect = obj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(w, h);
        rect.anchoredPosition = new Vector2(0, y);
        return rect;
    }

    private static TextMeshProUGUI CenteredText(Transform parent, string name, float w, float h, float y, float size) {
        var tmp = CenteredRect(parent, name, w, h, y).gameObject.AddComponent<TextMeshProUGUI>();
        tmp.font = FontManager.Current;
        tmp.fontSize = size;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        return tmp;
    }
}

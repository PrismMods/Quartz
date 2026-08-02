using Quartz.Compat.Game;
using Quartz.Core;
using Quartz.Resource;
using Quartz.UI.Generator;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
namespace Quartz.UI.Factory.Page;
internal static class PageCredits {
    public static void Create(RectTransform parent) {
        RectTransform content = PageFactory.CreateScrollablePage(parent);
        RectTransform logoRow = GenerateUI.Row(content.transform, 280f);
        var logoImg = CenteredRect(logoRow, "Logo", 260, 260).gameObject.AddComponent<Image>();
        logoImg.sprite = MainCore.Spr.Get(UISprite.QuartzLogo);
        logoImg.preserveAspect = true;
        TextMeshProUGUI title = Line(content, 60f, 38f);
        title.text = Info.DisplayName;
        TextMeshProUGUI subtitle = Line(content, 40f, 20f, 0.45f);
        GenerateUI.Localize(subtitle, "CREDITS_SUBTITLE", "by koren, sbrothers7, and more.");
        GenerateUI.Row(content.transform, 20f);
        Header(content, "CREDITS_TEAM", "Team");
        Block(content, 76f, 22f, "CREDITS_TEAM_BODY",
            "koren — lead developer\n" +
            "sbrothers7 — developer");
        Header(content, "CREDITS_TESTERS", "Mod Testers");
        Block(content, 108f, 22f, "CREDITS_TESTERS_BODY",
            "Refreshin\n" +
            "JeulGemi\n" +
            "Yul");
        Header(content, "CREDITS_PORTS", "Ported & referenced work");
        Block(content, 278f, 20f, "CREDITS_PORTS_BODY",
            "Overlayer — UI foundation (modlist.org)\n" +
            "DM Note (lee-sihun) — Key Viewer editor icons\n" +
            "DecoPreview (rdzip) — Decoration Preview, GPLv3\n" +
            "BackToThePast (tjwogud) — Nostalgia\n" +
            "FlipAndRotateTiles (tjwogud) — Flip & Rotate Tiles\n" +
            "AdofaiTweaks — editor tile-angle readout\n" +
            "HzHitSoundRenderer — Render All Hit Sounds\n" +
            "JipperKeyViewer — 108-key layout preset\n" +
            "enhanced-countdown (IMPL / KGH1113) — Countdown: Metronome mode");
        Header(content, "CREDITS_DONORS", "Special Donor");
        TextMeshProUGUI donors = Line(content, 40f, 22f, 0.7f);
        GenerateUI.Localize(donors, "CREDITS_DONORS_BODY", "Refreshin");
        GenerateUI.Row(content.transform, 12f);
        TextMeshProUGUI thanks = Line(content, 40f, 22f, 0.55f);
        GenerateUI.Localize(thanks, "CREDITS_THANKS", "Thank you for using Quartz.");
        TextMeshProUGUI license = Line(content, 30f, 12f, 0.2f);
        GenerateUI.Localize(license, "CREDITS_LICENSE", "Licensed under GPLv3");
        GenerateUI.Row(content.transform, 20f);
    }
    private static void Header(RectTransform content, string key, string def) {
        TextMeshProUGUI header = Line(content, 44f, 24f, 0.85f);
        GenerateUI.Localize(header, key, def);
    }
    private static void Block(RectTransform content, float height, float size, string key, string def) {
        TextMeshProUGUI body = Line(content, height, size, 0.4f);
        body.lineSpacing = 12f;
        TextCompat.Wrap(body);
        GenerateUI.Localize(body, key, def);
    }
    private static TextMeshProUGUI Line(RectTransform content, float height, float size, float alpha = 1f) {
        var tmp = GenerateUI.Row(content.transform, height).gameObject.AddComponent<TextMeshProUGUI>();
        tmp.font = FontManager.Current;
        tmp.fontSize = size;
        tmp.color = new Color(1f, 1f, 1f, alpha);
        tmp.alignment = TextAlignmentOptions.Center;
        return tmp;
    }
    private static RectTransform CenteredRect(Transform parent, string name, float w, float h) {
        GameObject obj = new(name);
        obj.transform.SetParent(parent, false);
        var rect = obj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(w, h);
        rect.anchoredPosition = Vector2.zero;
        return rect;
    }
}

using Quartz.UI.Factory.Page;
using Quartz.UI.Generator;
using Quartz.UI.Utility;
using UnityEngine;
using UnityEngine.UI;

namespace Quartz.UI.Factory;

public static class PageFactory {
    public static RectTransform PagesContaner;
    public static RectTransform CreatePages(GameObject panel) {
        // Sections registered by a previous build of the pages are stale.
        GenerateUI.ClearSections();

        GameObject pagesContainer = new("PagesContainer");
        pagesContainer.transform.SetParent(panel.transform, false);

        PagesContaner = pagesContainer.AddComponent<RectTransform>();

        PagesContaner.anchorMin = new Vector2(0, 0);
        PagesContaner.anchorMax = new Vector2(1, 1);
        PagesContaner.pivot = new Vector2(0.5f, 0.5f);

        PagesContaner.offsetMin = Vector2.zero;
        PagesContaner.offsetMax = new Vector2(0, -60);

        for(int i = 0; i < Enum.GetValues(typeof(OriginalMenuState)).Length; i++) {
            CreatePageBase(i);
        }

        UICore.Pages[UICore.CurrentMenuState].GetComponent<CanvasGroup>().alpha = 1f;
        UICore.Pages[UICore.CurrentMenuState].GetComponent<CanvasGroup>().interactable = true;
        UICore.Pages[UICore.CurrentMenuState].GetComponent<CanvasGroup>().blocksRaycasts = true;

        PageCredits.Create(UICore.Pages[(int)OriginalMenuState.Credits]);
        PageProfiles.Create(UICore.Pages[(int)OriginalMenuState.Profiles]);
        PageImport.Create(UICore.Pages[(int)OriginalMenuState.Import]);
        PageSettings.Create(UICore.Pages[(int)OriginalMenuState.Settings]);
        PageOverlay.Create(UICore.Pages[(int)OriginalMenuState.Overlay]);
        PageGameplay.Create(UICore.Pages[(int)OriginalMenuState.Gameplay]);
        PageVisuals.Create(UICore.Pages[(int)OriginalMenuState.Visuals]);
        PageTweaks.Create(UICore.Pages[(int)OriginalMenuState.Tweaks]);
        PageEditor.Create(UICore.Pages[(int)OriginalMenuState.Editor]);
        PageSearch.Create(UICore.Pages[(int)OriginalMenuState.Search]);

        // Developer page — only populated in "dev" builds (its tab is likewise
        // only created then).
        if(Quartz.Core.Info.IsDev) {
            PageDeveloper.Create(UICore.Pages[(int)OriginalMenuState.Developer]);
        }

        return PagesContaner;
    }


    public static RectTransform CreateScrollablePage(RectTransform parent) =>
        CreateScrollablePage(parent, out _);

    public static RectTransform CreateScrollablePage(RectTransform parent, out UIScrollController scrollController) {
        GameObject pad = new("Pad");
        pad.transform.SetParent(parent, false);

        RectTransform padRect = pad.AddComponent<RectTransform>();
        padRect.anchorMin = Vector2.zero;
        padRect.anchorMax = Vector2.one;
        padRect.pivot = new Vector2(0.5f, 0.5f);
        padRect.offsetMin = new Vector2(18f, 18f);
        padRect.offsetMax = new Vector2(-18f, -18f);

        GameObject viewport = new("Viewport");
        viewport.transform.SetParent(pad.transform, false);

        RectTransform viewportRect = viewport.AddComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = Vector2.zero;
        viewportRect.offsetMax = Vector2.zero;
        viewportRect.pivot = new Vector2(0.5f, 0.5f);

        viewport.AddComponent<EmptyGraphic>().raycastTarget = true;
        viewport.AddComponent<RectMask2D>();

        GameObject content = new("Content");
        content.transform.SetParent(viewport.transform, false);

        RectTransform contentRect = content.AddComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.offsetMin = Vector2.zero;
        contentRect.offsetMax = Vector2.zero;
        GenerateUI.FitVertical(content);

        scrollController = pad.AddComponent<UIScrollController>();
        scrollController.SetContent(contentRect, viewportRect);
        return contentRect;
    }

    public static RectTransform CreatePageBase(int num) {
        GameObject obj = new($"Page{num}");
        obj.transform.SetParent(PagesContaner, false);

        RectTransform rt = obj.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);

        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        CanvasGroup cg = obj.AddComponent<CanvasGroup>();
        cg.alpha = 0f;
        cg.interactable = false;
        cg.blocksRaycasts = false;

        UICore.Pages[num] = rt;

        return rt;
    }
}

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

        // Page rects from a previous build (a profile-switch or addon-reload
        // Rebuild) are destroyed with the old canvas; dynamic addon states
        // from the previous generation would otherwise linger as dead keys.
        UICore.Pages.Clear();

        GameObject pagesContainer = new("PagesContainer");
        pagesContainer.transform.SetParent(panel.transform, false);

        PagesContaner = pagesContainer.AddComponent<RectTransform>();

        PagesContaner.anchorMin = new Vector2(0, 0);
        PagesContaner.anchorMax = new Vector2(1, 1);
        PagesContaner.pivot = new Vector2(0.5f, 0.5f);

        PagesContaner.offsetMin = Vector2.zero;
        PagesContaner.offsetMax = new Vector2(0, -60);

        for(int i = 0; i < Enum.GetValues(typeof(OriginalMenuState)).Length; i++) CreatePageBase(i);

        // Dynamic addon page bases must exist before the alpha block below,
        // which activates UICore.Pages[CurrentMenuState] — and the current
        // tab can BE a dynamic addon page (e.g. an accent-commit or
        // profile-switch Rebuild fired while an addon tab was open).
        foreach(Quartz.Addons.AddonUI.PageDef def in Quartz.Addons.AddonUI.Pages) {
            RectTransform page = CreatePageBase(def.State);
            RectTransform content = CreateScrollablePage(page);
            try {
                def.Build(content);
            } catch(Exception e) {
                Quartz.Core.MainCore.Log.Err($"[Addon:{def.AddonId}] tab '{def.Title}' build threw: {e}");
                GenerateUI.AddMutedText(GenerateUI.Row(content, 40f)).text = $"'{def.Title}' failed to build — see log";
            }
        }

        // Defensive: a stale CurrentMenuState (dynamic page from a prior
        // generation that no longer exists) falls back to a guaranteed page
        // instead of throwing. AddonService.Reload retargets before Rebuild,
        // but this keeps CreatePages self-contained against any caller.
        if(!UICore.Pages.ContainsKey(UICore.CurrentMenuState))
            UICore.CurrentMenuState = (int)OriginalMenuState.OverlayGeneral;

        UICore.Pages[UICore.CurrentMenuState].GetComponent<CanvasGroup>().alpha = 1f;
        UICore.Pages[UICore.CurrentMenuState].GetComponent<CanvasGroup>().interactable = true;
        UICore.Pages[UICore.CurrentMenuState].GetComponent<CanvasGroup>().blocksRaycasts = true;

        PageCredits.Create(UICore.Pages[(int)OriginalMenuState.Credits]);
        PageProfiles.Create(UICore.Pages[(int)OriginalMenuState.Profiles]);
        PageImport.Create(UICore.Pages[(int)OriginalMenuState.Import]);
        PageSettings.Create(UICore.Pages[(int)OriginalMenuState.Settings]);
        // Overlay category
        PageOverlayGeneral.Create(UICore.Pages[(int)OriginalMenuState.OverlayGeneral]);
        PageKeyViewer.Create(UICore.Pages[(int)OriginalMenuState.KeyViewer]);
        PageProgressBar.Create(UICore.Pages[(int)OriginalMenuState.ProgressBar]);
        PageCombo.Create(UICore.Pages[(int)OriginalMenuState.Combo]);
        PageJudgement.Create(UICore.Pages[(int)OriginalMenuState.Judgement]);
        PageSongTitle.Create(UICore.Pages[(int)OriginalMenuState.SongTitle]);
        PagePanels.Create(UICore.Pages[(int)OriginalMenuState.Panels]);
        // Gameplay category
        PageGameplay.KeyLimiterPage(UICore.Pages[(int)OriginalMenuState.GameplayKeyLimiter]);
        PageGameplay.ChatterBlockerPage(UICore.Pages[(int)OriginalMenuState.GameplayChatter]);
        PageGameplay.JudgementRestrictionPage(UICore.Pages[(int)OriginalMenuState.GameplayJudgement]);
        PageGameplay.DeathLimitPage(UICore.Pages[(int)OriginalMenuState.GameplayDeath]);
        PageGameplay.AutoDeafenPage(UICore.Pages[(int)OriginalMenuState.GameplayAutoDeafen]);
        PageCalibration.Create(UICore.Pages[(int)OriginalMenuState.GameplayCalibration]);
        // Visuals category
        PageVisuals.EffectRemoverPage(UICore.Pages[(int)OriginalMenuState.VisualsEffectRemover]);
        PageVisuals.HideJudgementsPage(UICore.Pages[(int)OriginalMenuState.VisualsHideJudgements]);
        PageVisuals.VisualTweaksPage(UICore.Pages[(int)OriginalMenuState.VisualsVisualTweaks]);
        PageVisuals.PlanetColorsPage(UICore.Pages[(int)OriginalMenuState.VisualsPlanetColors]);
        PageVisuals.OttoIconPage(UICore.Pages[(int)OriginalMenuState.VisualsOttoIcon]);
        PageVisuals.UiHidingPage(UICore.Pages[(int)OriginalMenuState.VisualsUiHiding]);
        // Tweaks category
        PageTweaks.GeneralPage(UICore.Pages[(int)OriginalMenuState.TweaksGeneral]);
        PageTweaks.OptimizerPage(UICore.Pages[(int)OriginalMenuState.TweaksOptimizer]);
        PageTweaks.MainMenuPage(UICore.Pages[(int)OriginalMenuState.TweaksMainMenu]);
        PageTweaks.ResultsPage(UICore.Pages[(int)OriginalMenuState.TweaksResults]);
        // Editor category
        PageEditor.InspectorPage(UICore.Pages[(int)OriginalMenuState.EditorInspector]);
        PageEditor.TileReadoutPage(UICore.Pages[(int)OriginalMenuState.EditorTileReadout]);
        PageEditor.BgaPage(UICore.Pages[(int)OriginalMenuState.EditorBga]);
        // Nostalgia category
        NostalgiaUI.GameplayPage(UICore.Pages[(int)OriginalMenuState.NostalgiaGameplay]);
        NostalgiaUI.VisualsPage(UICore.Pages[(int)OriginalMenuState.NostalgiaVisuals]);
        NostalgiaUI.TweaksPage(UICore.Pages[(int)OriginalMenuState.NostalgiaTweaks]);
        NostalgiaUI.EditorPage(UICore.Pages[(int)OriginalMenuState.NostalgiaEditor]);
        // Single-page categories
        PageSearch.Create(UICore.Pages[(int)OriginalMenuState.Search]);

        // Addons category list/reload page (fixed enum state; the addon-
        // registered dynamic pages were built up top, before the alpha block).
        PageAddons.Create(UICore.Pages[(int)OriginalMenuState.Addons]);

        // Developer page — only populated in "dev" builds (its tab is likewise
        // only created then).
        if(Quartz.Core.Info.IsDev) PageDeveloper.Create(UICore.Pages[(int)OriginalMenuState.Developer]);

        // Every page built + laid out once above; now switch off compositing for
        // the hidden ones so only the visible tab's canvas renders each frame.
        // GameObjects stay active (Update/coroutines run); PageSwicher flips
        // these on/off across a slide. Runs last so init layout is unchanged.
        foreach(var kv in UICore.Pages)
            kv.Value.GetComponent<Canvas>().enabled = kv.Key == UICore.CurrentMenuState;

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

        // Each page is its own nested Canvas so its batch is isolated from the
        // other ~35 pages that share the one root canvas. Without this, the
        // per-frame slide (anchoredPosition) in PageSwicher dirties the root
        // canvas and Unity rebuilds EVERY page's geometry each animation frame
        // — the tab-switch stutter. overrideSorting=false keeps hierarchy draw
        // order. A nested canvas registers its own graphics, so it needs its
        // own GraphicRaycaster or the content becomes unclickable; CanvasGroup
        // blocksRaycasts still gates it, so hidden pages stay inert.
        Canvas pageCanvas = obj.AddComponent<Canvas>();
        pageCanvas.overrideSorting = false;
        obj.AddComponent<GraphicRaycaster>();

        UICore.Pages[num] = rt;

        return rt;
    }
}

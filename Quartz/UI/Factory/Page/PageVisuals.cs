using Quartz.Core;
using Quartz.Features.EffectRemover;
using Quartz.Features.Interop;
using Quartz.Features.Judgement;
using Quartz.Features.OttoIcon;
using Quartz.Features.PlanetColors;
using Quartz.Features.Tweaks;
using Quartz.Features.UiHider;
using Quartz.UI.Generator;
using Quartz.UI.Objects.Impl;
using Quartz.UI.Utility;
using UnityEngine;

namespace Quartz.UI.Factory.Page;

internal static partial class PageVisuals {
    // into CreateEffectRemover below verbatim.
    public static void EffectRemoverPage(RectTransform parent) =>
        CreateEffectRemover(Quartz.UI.Factory.PageFactory.CreateScrollablePage(parent));

    public static void HideJudgementsPage(RectTransform parent) =>
        CreateHideJudgements(Quartz.UI.Factory.PageFactory.CreateScrollablePage(parent));

    public static void VisualTweaksPage(RectTransform parent) =>
        CreateVisualTweaks(Quartz.UI.Factory.PageFactory.CreateScrollablePage(parent));

    public static void PlanetColorsPage(RectTransform parent) =>
        CreatePlanetColors(Quartz.UI.Factory.PageFactory.CreateScrollablePage(parent));

    public static void OttoIconPage(RectTransform parent) =>
        CreateOttoIcon(Quartz.UI.Factory.PageFactory.CreateScrollablePage(parent));

    public static void UiHidingPage(RectTransform parent) =>
        CreateUiHiding(Quartz.UI.Factory.PageFactory.CreateScrollablePage(parent));

}

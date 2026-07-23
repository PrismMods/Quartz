using Quartz.Core;
using Quartz.Features.AutoDeafen;
using Quartz.Features.ChatterBlocker;
using Quartz.Features.Interop;
using Quartz.Features.KeyLimiter;
using Quartz.Features.KeyViewer;
using Quartz.Features.Restriction;
using Quartz.Resource;
using Quartz.UI.Generator;
using Quartz.UI.Objects.Impl;
using Quartz.UI.Utility;
using UnityEngine;
using UnityEngine.UI;
using static UnityEngine.EventSystems.PointerEventData;
using TMPro;
namespace Quartz.UI.Factory.Page;
internal static partial class PageGameplay {
    private static Action keysChangedHandler;
    private static Action syncLockChangedHandler;
    public static void KeyLimiterPage(RectTransform parent) =>
        CreateKeyLimiter(Quartz.UI.Factory.PageFactory.CreateScrollablePage(parent));
    public static void ChatterBlockerPage(RectTransform parent) =>
        CreateChatterBlocker(Quartz.UI.Factory.PageFactory.CreateScrollablePage(parent));
    public static void JudgementRestrictionPage(RectTransform parent) =>
        CreateJudgementRestriction(Quartz.UI.Factory.PageFactory.CreateScrollablePage(parent));
    public static void DeathLimitPage(RectTransform parent) =>
        CreateDeathLimit(Quartz.UI.Factory.PageFactory.CreateScrollablePage(parent));
    public static void AutoDeafenPage(RectTransform parent) =>
        CreateAutoDeafen(Quartz.UI.Factory.PageFactory.CreateScrollablePage(parent));
    public static void PracticePage(RectTransform parent) =>
        CreatePractice(Quartz.UI.Factory.PageFactory.CreateScrollablePage(parent));
}

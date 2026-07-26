using Quartz.Core;
using Quartz.Features.ChatterBlocker;
using Quartz.Features.Interop;
using Quartz.Features.KeyLimiter;
using Quartz.Resource;
using Quartz.UI.Generator;
using Quartz.UI.Objects.Impl;
using Quartz.UI.Utility;
using UnityEngine;
using UnityEngine.UI;
using static UnityEngine.EventSystems.PointerEventData;
using TMPro;
namespace Quartz.UI.Factory.Page;
public static partial class PageKeyLimiter {
    private static Action keysChangedHandler;
    private static Action syncLockChangedHandler;
    public static void KeyLimiterPage(RectTransform parent) =>
        CreateKeyLimiter(Quartz.UI.Factory.PageFactory.CreateScrollablePage(parent));
    public static void ChatterBlockerPage(RectTransform parent) =>
        CreateChatterBlocker(Quartz.UI.Factory.PageFactory.CreateScrollablePage(parent));
}

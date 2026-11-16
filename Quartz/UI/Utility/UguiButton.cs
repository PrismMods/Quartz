using GTweens.Easings;
using GTweens.Extensions;
using GTweens.Tweens;
using Quartz.Core;
using Quartz.Resource;
using Quartz.Tween;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static UnityEngine.EventSystems.PointerEventData;
namespace Quartz.UI.Utility;
/// <summary>A uGUI object as a button: clicks, and an outline that fades in under the pointer. The calibration popup's and the classic menu's.</summary>
public static class UguiButton {
    public static void Add(GameObject obj, Action<InputButton> onClick, bool outline = true) {
        EventTrigger trigger = obj.AddComponent<EventTrigger>();
        UnityUtils.AddClickEvent(trigger, e => onClick?.Invoke(e.button));
        if(outline) OutlineHover(obj, trigger);
    }
    public static Image OutlineHover(GameObject obj, EventTrigger trigger) {
        GTween hoverSeq = null;
        GameObject hover = new("Hover");
        hover.transform.SetParent(obj.transform, false);
        hover.transform.SetAsFirstSibling();
        RectTransform hoverRect = hover.AddComponent<RectTransform>();
        hoverRect.anchorMin = Vector2.zero;
        hoverRect.anchorMax = Vector2.one;
        hoverRect.pivot = new Vector2(0.5f, 0.5f);
        hoverRect.offsetMin = Vector2.zero;
        hoverRect.offsetMax = Vector2.zero;
        hover.AddComponent<LayoutElement>().ignoreLayout = true;
        Image hoverImage = hover.AddComponent<Image>();
        hoverImage.sprite = MainCore.Spr.Get(UISliceSprite.CircleOutline256P2048);
        hoverImage.type = Image.Type.Sliced;
        Color baseColor = UIColors.ObjectActive;
        hoverImage.color = new Color(baseColor.r, baseColor.g, baseColor.b, 0f);
        void FadeOutline(float target) {
            hoverSeq?.Kill();
            hoverSeq = hoverImage.GTAlpha(target, 0.1f).SetEasing(Easing.OutSine);
            MainCore.TC.Play(hoverSeq);
        }
        UnityUtils.AddEvent(EventTriggerType.PointerEnter, e => FadeOutline(1f), trigger);
        UnityUtils.AddEvent(EventTriggerType.PointerExit, e => FadeOutline(0f), trigger);
        return hoverImage;
    }
}

using System.Globalization;
using Quartz.Core;
using Quartz.Localization;
using Quartz.Resource;
using Quartz.UI.Objects.Impl;
using Quartz.UI.Utility;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static UnityEngine.EventSystems.PointerEventData;
using GTweens.Tweens;
using GTweens.Easings;
using GTweens.Extensions;
using GTweens.Builders;
using Quartz.Tween;
using GTweenExtensions = GTweens.Extensions.GTweenExtensions;
using TMPro;
using Quartz.Compat.Game;
namespace Quartz.UI.Generator;
public static partial class GenerateUI {
    public static RectTransform Row(Transform parent, float height = 50f) {
        GameObject obj = new("Row");
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.AddComponent<RectTransform>();
        LayoutElement le = obj.AddComponent<LayoutElement>();
        le.preferredHeight = height;
        le.minHeight = height;
        return rect;
    }
    public static VerticalLayoutGroup FitVertical(GameObject obj, float spacing = 12f) {
        VerticalLayoutGroup layout = obj.AddComponent<VerticalLayoutGroup>();
        layout.spacing = spacing;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        ContentSizeFitter fitter = obj.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        return layout;
    }
    public static void ClearChildren(Transform t) {
        for(int i = t.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(t.GetChild(i).gameObject);
    }
    public static RectTransform BackGround(float rightInset = 250f) {
        GameObject obj = new("Bg");
        RectTransform rect = obj.AddComponent<RectTransform>();
        rect.anchorMin = new(0f, 0f);
        rect.anchorMax = new(1f, 1f);
        rect.pivot = new(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = new(-rightInset, 0f);
        Image img = obj.AddComponent<Image>();
        img.color = UIColors.ObjectBG;
        img.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P2048);
        img.type = Image.Type.Sliced;
        return rect;
    }
    public static GameObject AddSmallChangedCircle(RectTransform parent) {
        GameObject obj = new("Changed");
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(6f, -6f);
        rect.sizeDelta = new Vector2(8f, 8f);
        Image img = obj.AddComponent<Image>();
        img.sprite = MainCore.Spr.Get(UISprite.Circle256);
        Color c = UIColors.ObjectActive;
        c.a = 0f;
        img.color = c;
        return obj;
    }
    public static HorizontalLayoutGroup ButtonRow(RectTransform row, float spacing = 12f) {
        HorizontalLayoutGroup layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = spacing;
        layout.padding = new RectOffset(16, 12, 0, 0);
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;
        layout.childAlignment = TextAnchor.MiddleLeft;
        return layout;
    }
    public static RectTransform MakeBody(Transform parent, string name) {
        GameObject obj = new(name);
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        FitVertical(obj, 8f);
        return rect;
    }
    public static Transform AddToolTip(this Transform parent, string key, string def, Translator tr = null) {
        tr ??= MainCore.Tr;
        return parent.AddToolTipInternal(() => tr.Get(key, def));
    }
    public static Transform AddToolTip(this Transform parent, string tip)
        => parent.AddToolTipInternal(() => tip);
    private static Transform AddToolTipInternal(this Transform parent, System.Func<string> getText) {
        EventTrigger trigger = parent.gameObject.GetComponent<EventTrigger>()
            ?? parent.gameObject.AddComponent<EventTrigger>();
        UnityUtils.AddEvent(
            EventTriggerType.PointerEnter,
            _ => Tooltip.Show(getText()),
            trigger
        );
        UnityUtils.AddEvent(
            EventTriggerType.PointerExit,
            _ => Tooltip.Hide(),
            trigger
        );
        return parent;
    }
}
internal sealed class DropdownLanguageRefresh : MonoBehaviour {
    private Action refresh;
    private Action<TranslationFailState> onLoadEnd;
    private Action<string> onLanguageChanged;
    public void Init(Action refreshAction) {
        refresh = refreshAction;
        onLanguageChanged = _ => refresh?.Invoke();
        onLoadEnd = state => {
            if(state == TranslationFailState.Success) refresh?.Invoke();
        };
        MainCore.Tr.OnLanguageChanged += onLanguageChanged;
        MainCore.Tr.OnLoadEnd += onLoadEnd;
    }
    private void OnDestroy() {
        if(onLanguageChanged != null) MainCore.Tr.OnLanguageChanged -= onLanguageChanged;
        if(onLoadEnd != null) MainCore.Tr.OnLoadEnd -= onLoadEnd;
    }
}

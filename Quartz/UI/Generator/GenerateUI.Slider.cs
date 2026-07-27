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
    public static UISlider Slider(
        Transform parent,
        float defaultValue,
        float min,
        float max,
        float value,
        Func<float, float> filter,
        Action<float> onChanged,
        Action<float> onComplete,
        string text,
        string id,
        float rightInset = 250f
    ) {
        RectTransform rect = BackGround(rightInset);
        rect.SetParent(parent, false);
        rect.gameObject.AddComponent<EventTrigger>();
        GameObject change = AddSmallChangedCircle(rect);
        Image changeImg = change.GetComponent<Image>();
        GameObject fill = new("Fill");
        fill.transform.SetParent(rect, false);
        RectTransform fillRect = fill.AddComponent<RectTransform>();
        fillRect.anchorMin = new Vector2(0f, 0f);
        fillRect.anchorMax = new Vector2(0f, 1f);
        fillRect.pivot = new Vector2(0f, 0.5f);
        fillRect.anchoredPosition = Vector2.zero;
        fillRect.sizeDelta = Vector2.zero;
        TextMeshProUGUI label = AddText(rect);
        label.text = text;
        label.alignment = TextAlignmentOptions.Left;
        LocalizeById(label, id, text);
        TextMeshProUGUI valueText = AddText(rect);
        valueText.alignment = TextAlignmentOptions.Right;
        var valueTextRect = valueText.gameObject.GetComponent<RectTransform>();
        valueTextRect.offsetMin = Vector2.zero;
        valueTextRect.offsetMax = new(-16f, 0f);
        Image fillImg = fill.AddComponent<Image>();
        fillImg.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P2048);
        fillImg.type = Image.Type.Sliced;
        fill.AddComponent<Mask>().showMaskGraphic = true;
        GameObject changeUp = AddSmallChangedCircle(fillRect);
        Image changeUpImg = changeUp.GetComponent<Image>();
        GameObject outline = new("Outline");
        outline.transform.SetParent(rect, false);
        RectTransform outlineRect = outline.AddComponent<RectTransform>();
        outlineRect.anchorMin = Vector2.zero;
        outlineRect.anchorMax = Vector2.one;
        outlineRect.offsetMin = Vector2.zero;
        outlineRect.offsetMax = Vector2.zero;
        Image outlineImg = outline.AddComponent<Image>();
        outlineImg.sprite = MainCore.Spr.Get(UISliceSprite.CircleOutline256P2048);
        outlineImg.type = Image.Type.Sliced;
        outlineImg.color = new Color(1f, 1f, 1f, 0f);
        outlineImg.raycastTarget = false;
        TextMeshProUGUI previewLabel = AddText(rect);
        previewLabel.alignment = TextAlignmentOptions.Right;
        previewLabel.richText = true;
        previewLabel.raycastTarget = false;
        previewLabel.text = "";
        RectTransform previewRect = previewLabel.gameObject.GetComponent<RectTransform>();
        previewRect.offsetMin = Vector2.zero;
        previewRect.offsetMax = new(-16f, 0f);
        UISlider slider = new(
            id,
            rect,
            fillRect,
            fillImg,
            label,
            valueText,
            changeImg,
            changeUpImg,
            outlineImg,
            previewLabel,
            defaultValue,
            min,
            max,
            value,
            filter,
            onChanged,
            onComplete
        );
        float Apply(float v) {
            v = filter != null ? filter(v) : v;
            return Mathf.Clamp(v, min, max);
        }
        void SetFromMouse() {
            Vector2 local = rect.InverseTransformPoint(UnityEngine.Input.mousePosition);
            float width = rect.rect.width;
            float t = Mathf.Clamp01((local.x + (width * 0.5f)) / width);
            float v = Mathf.Lerp(min, max, t);
            slider.Set(Apply(v));
        }
        AddButton(rect.gameObject, e => {
            switch(e) {
                case InputButton.Left:
                    SetFromMouse();
                    slider.OnComplete?.Invoke(slider.Value);
                    break;
                case InputButton.Middle:
                    if(!MainCore.Conf.MiddleClickToDefault) break;
                    slider.Set(Apply(defaultValue));
                    slider.OnComplete?.Invoke(slider.Value);
                    break;
            }
        }, true);
        EventTrigger trigger = rect.gameObject.GetComponent<EventTrigger>()
            ?? rect.gameObject.AddComponent<EventTrigger>();
        bool isDragging = false;
        UnityUtils.AddEvent(EventTriggerType.BeginDrag, _ => {
            if(!UnityEngine.Input.GetMouseButton(0)) return;
            isDragging = true;
            SetFromMouse();
        }, trigger);
        UnityUtils.AddEvent(EventTriggerType.Drag, _ => {
            if(isDragging && UnityEngine.Input.GetMouseButton(0)) {
                SetFromMouse();
            } else {
                isDragging = false;
            }
        }, trigger);
        UnityUtils.AddEvent(EventTriggerType.EndDrag, _ => {
            if(isDragging) {
                isDragging = false;
                slider.OnComplete?.Invoke(slider.Value);
            }
        }, trigger);
        UnityUtils.AddEvent(EventTriggerType.PointerUp, _ => {
            if(isDragging) {
                isDragging = false;
                slider.OnComplete?.Invoke(slider.Value);
            }
        }, trigger);
        AddSliderValueEditor(slider, rect, valueText, () => Apply(defaultValue));
        slider.Set(Apply(value), false);
        return slider;
    }
    public static void AddSliderValueEditor(
        UISlider slider,
        RectTransform rect,
        TextMeshProUGUI valueText,
        Func<float> applyDefault
    ) {
        GameObject editObj = new("ValueEdit");
        editObj.transform.SetParent(rect, false);
        RectTransform editRect = editObj.AddComponent<RectTransform>();
        editRect.anchorMin = new Vector2(1f, 0f);
        editRect.anchorMax = new Vector2(1f, 1f);
        editRect.pivot = new Vector2(1f, 0.5f);
        editRect.anchoredPosition = Vector2.zero;
        editRect.sizeDelta = new Vector2(140f, 0f);
        editObj.AddComponent<RectMask2D>();
        TMP_InputField editField = editObj.AddComponent<TMP_InputField>();
        TextMeshProUGUI editText = AddText(editObj.transform, true);
        editText.alignment = TextAlignmentOptions.Right;
        editText.rectTransform.offsetMax = new Vector2(-16f, 0f);
        editText.richText = false;
        editField.textViewport = editRect;
        editField.textComponent = editText;
        editField.lineType = TMP_InputField.LineType.SingleLine;
        editField.contentType = TMP_InputField.ContentType.Standard;
        slider.EditField = editField;
        editObj.SetActive(false);
        bool editing = false;
        void EndEdit(string raw) {
            if(!editing) return;
            editing = false;
            editObj.SetActive(false);
            valueText.gameObject.SetActive(true);
            slider.CommitExpression(raw);
        }
        void BeginEdit() {
            if(editing) return;
            editing = true;
            valueText.gameObject.SetActive(false);
            editObj.SetActive(true);
            editField.SetTextWithoutNotify(
                slider.Value.ToString("0.###", CultureInfo.InvariantCulture)
            );
            editField.Select();
            editField.ActivateInputField();
        }
        editField.onValueChanged.AddListener(slider.PreviewExpression);
        editField.onEndEdit.AddListener(EndEdit);
        GameObject zone = new("ValueEditZone");
        zone.transform.SetParent(rect, false);
        RectTransform zoneRect = zone.AddComponent<RectTransform>();
        zoneRect.anchorMin = new Vector2(1f, 0f);
        zoneRect.anchorMax = new Vector2(1f, 1f);
        zoneRect.pivot = new Vector2(1f, 0.5f);
        zoneRect.anchoredPosition = Vector2.zero;
        zoneRect.sizeDelta = new Vector2(110f, 0f);
        zone.AddComponent<EmptyGraphic>().raycastTarget = true;
        EventTrigger zoneTrigger = zone.AddComponent<EventTrigger>();
        UnityUtils.AddClickEvent(zoneTrigger, e => {
            switch(e.button) {
                case PointerEventData.InputButton.Left:
                    BeginEdit();
                    break;
                case PointerEventData.InputButton.Middle:
                    if(MainCore.Conf.MiddleClickToDefault) {
                        slider.Set(applyDefault());
                        slider.OnComplete?.Invoke(slider.Value);
                    }
                    break;
            }
        });
        UnityUtils.AddEvent(EventTriggerType.PointerEnter, _ => {
            if(!editing) valueText.color = UIColors.ObjectActiveLightBright;
        }, zoneTrigger);
        UnityUtils.AddEvent(EventTriggerType.PointerExit, _ => {
            valueText.color = Color.white;
        }, zoneTrigger);
    }
    public static UISlider SnapSlider(
        Transform body, string label, string id,
        float defVal, float min, float max, float val,
        string format, float step,
        Action<float> setter,
        Action live, Action save
    ) {
        float Snap(float v) => Mathf.Clamp(Mathf.Round(v / step) * step, min, max);
        UISlider s = Slider(
            Row(body),
            defVal, min, max, val,
            Snap, null, null,
            label, id
        );
        s.Format = format;
        s.OnChanged = v => { setter(v); live?.Invoke(); };
        s.OnComplete = v => { setter(v); live?.Invoke(); save?.Invoke(); };
        return s;
    }
}

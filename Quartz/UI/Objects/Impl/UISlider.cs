using System.Globalization;
using UnityEngine;
using UnityEngine.UI;
using GTweens.Tweens;
using Quartz.Core;
using Quartz.Tween;
using Quartz.Utility.Math;
using GTweens.Extensions;
using GTweens.Builders;
using GTweens.Easings;
using GTweenExtensions = GTweens.Extensions.GTweenExtensions;
using TMPro;
namespace Quartz.UI.Objects.Impl;
public class UISlider : UIObject {
    public float DefaultValue { get; }
    public float Min;
    public float Max;
    public float Value { get; private set; }
    public string Format {
        get;
        set {
            field = value;
            UpdateValueText();
        }
    }
    public Action<float> OnChanged;
    public Action<float> OnComplete;
    public Func<float, float> Filter;
    internal float MinFill;
    private Color? accentOverride;
    public Color AccentColor => accentOverride ?? UIColors.ObjectActive;
    public RectTransform FillRect { get; }
    public Image FillImage { get; }
    public TextMeshProUGUI Label { get; }
    public TextMeshProUGUI ValueText { get; }
    public Image ChangedImage { get; }
    public Image ChangedUpImage { get; }
    public Image OutlineImage { get; }
    public TextMeshProUGUI PreviewLabel { get; }
    public TMP_InputField EditField { get; set; }
    private GTween fillSeq;
    private GTween changeSeq;
    private GTween stateSeq;
    public UISlider(
        string id,
        RectTransform rect,
        RectTransform fillRect,
        Image fillImage,
        TextMeshProUGUI label,
        TextMeshProUGUI valueText,
        Image changedImage,
        Image changedUpImage,
        Image outlineImage,
        TextMeshProUGUI previewLabel,
        float defaultValue,
        float min,
        float max,
        float value,
        Func<float, float> filter,
        Action<float> onChanged,
        Action<float> onComplete,
        string format = "0.##"
    ) : base(id, rect) {
        FillRect = fillRect;
        FillImage = fillImage;
        FillImage.color = UIColors.ObjectActive;
        Label = label;
        ValueText = valueText;
        ChangedImage = changedImage;
        ChangedUpImage = changedUpImage;
        ChangedUpImage.color = UIColors.ObjectBG;
        OutlineImage = outlineImage;
        PreviewLabel = previewLabel;
        DefaultValue = defaultValue;
        Min = min;
        Max = max;
        OnChanged = onChanged;
        OnComplete = onComplete;
        Format = format;
        Filter = filter;
        Value = ApplyFilter(value);
        Value = Mathf.Clamp(Value, Min, Max);
        UpdateVisual(true);
    }
    public void Set(float value, bool invoke = true) {
        if(float.IsNaN(value)) return;
        value = ApplyFilter(value);
        Value = Mathf.Clamp(value, Min, Max);
        if(invoke) OnChanged?.Invoke(Value);
        UpdateVisual();
    }
    public void SetAccent(Color color) {
        accentOverride = color;
        Color fill = FillImage.color;
        FillImage.color = new(color.r, color.g, color.b, fill.a);
    }
    public void SetOnlyValue(float value, bool noAnimate = false) {
        if(float.IsNaN(value)) return;
        Value = Mathf.Clamp(ApplyFilter(value), Min, Max);
        UpdateVisual(noAnimate);
    }
    public float Normalize() => Mathf.InverseLerp(Min, Max, Value);
    public float Normalize(float value) => Mathf.InverseLerp(Min, Max, value);
    private float FillFor(float t) => MinFill > 0f && t > 0f ? Mathf.Max(t, Mathf.Min(MinFill, 1f)) : t;
    private float ApplyFilter(float v) => Filter?.Invoke(v) ?? v;
    private void UpdateValueText() => ValueText?.text = Value.ToString(Format);
    public void UpdateVisual(bool noAnimate = false) {
        fillSeq?.Kill();
        changeSeq?.Kill();
        UpdateValueText();
        float t = FillFor(Normalize());
        float changeAlpha = Mathf.Abs(DefaultValue - Value) > 0.001f ? 1f : 0f;
        if(noAnimate) {
            Vector2 fra = FillRect.anchorMax;
            fra.x = t;
            FillRect.anchorMax = fra;
            Color ci = ChangedImage.color;
            ci.a = changeAlpha;
            ChangedImage.color = ci;
            Color cui = ChangedUpImage.color;
            cui.a = changeAlpha;
            ChangedUpImage.color = cui;
            return;
        }
        fillSeq = GTweenSequenceBuilder.New()
            .Join(
                GTweenExtensions.Tween(
                    () => FillRect.anchorMax.x,
                    x => {
                        Vector2 anchor = FillRect.anchorMax;
                        anchor.x = x;
                        FillRect.anchorMax = anchor;
                    },
                    t,
                    0.6f
                ).SetEasing(Easing.OutExpo)
            ).Build();
        MainCore.TC.Play(fillSeq);
        changeSeq = GTweenSequenceBuilder.New()
            .Join(ChangedImage.GTAlpha(changeAlpha, 0.2f).SetEasing(Easing.OutSine))
            .Join(ChangedUpImage.GTAlpha(changeAlpha, 0.2f).SetEasing(Easing.OutSine))
            .Build();
        MainCore.TC.Play(changeSeq);
    }
    private static bool TryParseLiteral(string raw, out float value) =>
        float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
        || float.TryParse(raw, out value);
    public void PreviewExpression(string raw) {
        var (result, state) = Evaluator.Evaluate(raw, Value, Min, Max);
        if(state == EvalState.Error) {
            if(PreviewLabel != null) PreviewLabel.text = "";
            SetStateVisuals(MathVisuals.GetStateColor(state), true);
            return;
        }
        bool isLiteral = TryParseLiteral(raw, out float typed) && Mathf.Abs(typed - result) < 0.0001f;
        if(isLiteral) {
            if(PreviewLabel != null) PreviewLabel.text = "";
        } else if(PreviewLabel != null) {
            string symbol = state switch {
                EvalState.OverRange => "<",
                EvalState.UnderRange => ">",
                _ => "="
            };
            PreviewLabel.text = $"{ApplyFilter(result).ToString(Format)} {symbol} <color=#00000000>{raw}</color>";
        }
        SetStateVisuals(MathVisuals.GetStateColor(state), true, result);
    }
    public void CommitExpression(string raw) {
        var (result, state) = Evaluator.Evaluate(raw, Value, Min, Max);
        if(state != EvalState.Error) {
            Set(result);
            OnComplete?.Invoke(Value);
        } else {
            UpdateVisual(true);
        }
        if(PreviewLabel != null) PreviewLabel.text = "";
        SetStateVisuals(AccentColor, false);
    }
    private void SetStateVisuals(Color targetColor, bool isCalculating, float? value = null) {
        stateSeq?.Kill();
        float targetFillAlpha = isCalculating ? (value.HasValue ? 0.3f : 0f) : 1f;
        Color startOutline = OutlineImage.color;
        Color startFill = FillImage.color;
        Color startChanged = ChangedImage.color;
        Color startCaret = EditField != null ? EditField.caretColor : Color.clear;
        stateSeq = GTweenSequenceBuilder.New()
            .Join(
                GTweenExtensions.Tween(
                    () => 0f,
                    x => {
                        OutlineImage.color = Color.Lerp(startOutline, new(targetColor.r, targetColor.g, targetColor.b, isCalculating ? targetColor.a : 0f), x);
                        FillImage.color = Color.Lerp(startFill, new(targetColor.r, targetColor.g, targetColor.b, targetFillAlpha), x);
                        ChangedImage.color = Color.Lerp(startChanged, new(targetColor.r, targetColor.g, targetColor.b, ChangedImage.color.a), x);
                        if(EditField != null) {
                            EditField.caretColor = Color.Lerp(startCaret, new(targetColor.r, targetColor.g, targetColor.b, EditField.caretColor.a), x);
                        }
                    },
                    1f,
                    0.2f
                ).SetEasing(Easing.OutSine)
            ).Build();
        MainCore.TC.Play(stateSeq);
        if(value.HasValue && isCalculating) {
            fillSeq?.Kill();
            fillSeq = GTweenSequenceBuilder.New()
                .Join(
                    GTweenExtensions.Tween(
                        () => FillRect.anchorMax.x,
                        x => {
                            Vector2 anchor = FillRect.anchorMax;
                            anchor.x = x;
                            FillRect.anchorMax = anchor;
                        },
                        FillFor(Normalize(value.Value)),
                        0.4f
                    ).SetEasing(Easing.OutExpo)
                ).Build();
            MainCore.TC.Play(fillSeq);
        }
    }
}

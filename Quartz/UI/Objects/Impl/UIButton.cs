using UnityEngine;
using UnityEngine.UI;
using GTweens.Tweens;
using Quartz.Tween;
using GTweens.Easings;
using Quartz.Core;
using TMPro;
namespace Quartz.UI.Objects.Impl;
public class UIButton : UIObject {
    public Action OnClick { get; set; }
    public TextMeshProUGUI Label { get; }
    public Image Background { get; }
    public Func<Color> RestColor { get; set; } = static () => UIColors.ObjectButton;
    public Func<Color> HoverColor { get; set; } = static () => UIColors.ObjectActiveLightBright;
    private GTween hoverTween;
    public UIButton(
        string id,
        RectTransform rect,
        TextMeshProUGUI label,
        Image background,
        Action onClick
    ) : base(id, rect) {
        Label = label;
        Background = background;
        OnClick = onClick;
        UpdateVisual(true);
    }
    public UIButton SetSecondary() {
        RestColor = static () => UIColors.ObjectBG;
        HoverColor = static () => UIColors.ObjectButton;
        UpdateVisual(true);
        return this;
    }
    public UIButton SetNeutral() {
        RestColor = static () => Color.Lerp(UIColors.ObjectBG, UIColors.ObjectButton, 0.55f);
        HoverColor = static () => UIColors.ObjectButton;
        UpdateVisual(true);
        return this;
    }
    public UIButton SetDanger() {
        RestColor = static () => Color.Lerp(UIColors.SoftRed, UIColors.PanelBG, 0.28f);
        HoverColor = static () => UIColors.SoftRed;
        UpdateVisual(true);
        return this;
    }
    public void OnHoverEnter() {
        hoverTween?.Kill();
        hoverTween = Background
            .GTColor(HoverColor(), 0.12f)
            .SetEasing(Easing.OutSine);
        MainCore.TC.Play(hoverTween);
    }
    public void OnHoverExit() {
        hoverTween?.Kill();
        hoverTween = Background
            .GTColor(RestColor(), 0.12f)
            .SetEasing(Easing.OutSine);
        MainCore.TC.Play(hoverTween);
    }
    public void Click(bool invoke = true) {
        if(invoke) OnClick?.Invoke();
        UpdateVisual();
    }
    public void UpdateVisual(bool noAnimate = false) {
        hoverTween?.Kill();
        if(noAnimate) {
            Background.color = RestColor();
            return;
        }
        hoverTween = Background
            .GTColor(RestColor(), 0.2f)
            .SetEasing(Easing.OutSine);
        MainCore.TC.Play(hoverTween);
    }
}

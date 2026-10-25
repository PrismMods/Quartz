using UnityEngine;
using UnityEngine.UI;
using GTweens.Tweens;
using Quartz.Tween;
using GTweens.Easings;
using Quartz.Core;
using TMPro;
using Quartz.UI.Generator;
using UnityEngine.EventSystems;
namespace Quartz.UI.Objects.Impl;
public class UIButton : UIObject {
    public Action OnClick { get; set; }
    public TextMeshProUGUI Label { get; }
    public Image Background { get; }
    public Func<Color> RestColor { get; set; } = static () => UIColors.ObjectButton;
    public Func<Color> HoverColor { get; set; } = static () => Color.Lerp(UIColors.ObjectButton, Color.white, 0.06f);
    private GTween hoverTween;
    private bool hasRing;
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
        HoverColor = static () => UIColors.ObjectBG;
        AddRing();
        UpdateVisual(true);
        return this;
    }
    public UIButton SetNeutral() {
        RestColor = static () => Color.Lerp(UIColors.ObjectBG, UIColors.ObjectButton, 0.55f);
        HoverColor = static () => Color.Lerp(UIColors.ObjectBG, UIColors.ObjectButton, 0.55f);
        AddRing();
        UpdateVisual(true);
        return this;
    }
    public UIButton SetDanger() {
        RestColor = static () => Color.Lerp(UIColors.SoftRed, UIColors.PanelBG, 0.12f);
        HoverColor = static () => UIColors.SoftRed;
        UpdateVisual(true);
        return this;
    }
    private void AddRing() {
        if(hasRing) return;
        EventTrigger trigger = Rect.GetComponent<EventTrigger>();
        if(trigger == null) return;
        hasRing = true;
        GenerateUI.AddOutlineHover(Rect.gameObject, trigger);
    }
    public void OnHoverEnter() {
        if(IsDisposed) return;
        hoverTween?.Kill();
        hoverTween = Background
            .GTColor(HoverColor(), 0.12f)
            .SetEasing(Easing.OutSine);
        MainCore.TC.Play(hoverTween);
    }
    public void OnHoverExit() {
        if(IsDisposed) return;
        hoverTween?.Kill();
        hoverTween = Background
            .GTColor(RestColor(), 0.12f)
            .SetEasing(Easing.OutSine);
        MainCore.TC.Play(hoverTween);
    }
    public void Click(bool invoke = true) {
        if(IsDisposed) return;
        if(invoke) OnClick?.Invoke();
        UpdateVisual();
    }
    public void UpdateVisual(bool noAnimate = false) {
        if(IsDisposed) return;
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
    public override void Dispose() {
        if(IsDisposed) return;
        hoverTween?.Kill();
        hoverTween = null;
        OnClick = null;
        base.Dispose();
    }
}

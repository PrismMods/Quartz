using GTweens.Builders;
using GTweens.Easings;
using GTweens.Extensions;
using GTweens.Tweens;
using Quartz.Core;
using UnityEngine;
namespace Quartz.UI.Objects;
public abstract class UIObject {
    private static readonly List<UIObject> _tickables = [];
    public string Id { get; }
    public RectTransform Rect { get; }
    protected CanvasGroup CanvasGroup {
        get {
            field ??= Rect.GetComponent<CanvasGroup>() ?? Rect.gameObject.AddComponent<CanvasGroup>();
            return field;
        }
    }
    private GTween blockSeq;
    protected UIObject(string id, RectTransform rect) {
        Id = id;
        Rect = rect;
    }
    public virtual void SetBlocked(bool blocked, bool noAnimate = false) {
        blockSeq?.Kill();
        float targetAlpha = blocked ? 0.4f : 1f;
        CanvasGroup cg = CanvasGroup;
        cg.interactable = !blocked;
        cg.blocksRaycasts = !blocked;
        if(noAnimate) {
            cg.alpha = targetAlpha;
            return;
        }
        blockSeq = GTweenSequenceBuilder.New()
            .Join(
                GTweenExtensions.Tween(
                    () => cg == null ? targetAlpha : cg.alpha,
                    x => { if(cg != null) cg.alpha = x; },
                    targetAlpha,
                    0.2f
                ).SetEasing(Easing.OutSine)
            ).Build();
        MainCore.TC.Play(blockSeq);
    }
    public virtual void Dispose() {
        blockSeq?.Kill();
        UnregisterTick();
    }
    protected void RegisterTick() => _tickables.Add(this);
    protected void UnregisterTick() => _tickables.Remove(this);
    public virtual void Tick() { }
    public static void TickAll() {
        for(int i = 0; i < _tickables.Count; i++) {
            UIObject o = _tickables[i];
            if(o.Rect == null || !o.Rect.gameObject.activeInHierarchy) continue;
            o.Tick();
        }
    }
    public static void DisposeAll() {
        for(int i = _tickables.Count - 1; i >= 0; i--) _tickables[i].Dispose();
        _tickables.Clear();
    }
}

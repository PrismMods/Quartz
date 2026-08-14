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
    public bool IsDisposed { get; private set; }
    public Action OnDisposed;
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
        if(IsDisposed) return;
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
        if(IsDisposed) return;
        blockSeq?.Kill();
        blockSeq = null;
        UnregisterTick();
        IsDisposed = true;
        Action disposed = OnDisposed;
        OnDisposed = null;
        try { disposed?.Invoke(); } catch(Exception e) { Diag.Ignore(e); }
    }
    protected void RegisterTick() => _tickables.Add(this);
    protected void UnregisterTick() => _tickables.Remove(this);
    public virtual void Tick() { }
    public virtual void OnHidden() { }
    public static void NotifyHidden() {
        for(int i = 0; i < _tickables.Count; i++) {
            UIObject o = _tickables[i];
            if(o.Rect == null) continue;
            o.OnHidden();
        }
    }
    public static void TickAll() {
        for(int i = 0; i < _tickables.Count; i++) {
            UIObject o = _tickables[i];
            if(o.Rect == null || !o.Rect.gameObject.activeInHierarchy) continue;
            o.Tick();
        }
    }
    public static void DisposeAll() {
        while(_tickables.Count > 0) {
            UIObject o = _tickables[^1];
            _tickables.RemoveAt(_tickables.Count - 1);
            o.Dispose();
        }
    }
}

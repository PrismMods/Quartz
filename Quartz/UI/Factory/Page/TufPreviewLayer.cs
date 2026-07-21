using GTweens.Easings;
using GTweens.Tweens;
using Quartz.Core;
using Quartz.Features.Tuf;
using Quartz.Tween;
using UnityEngine;
using UnityEngine.UI;
namespace Quartz.UI.Factory.Page;
internal sealed class TufPreviewGroup {
    private readonly Dictionary<string, Slot> slots = new(StringComparer.Ordinal);
    private volatile bool dirty;
    public TufPreviewGroup() => TufPreviewCache.Changed += OnReady;
    private void OnReady() => dirty = true;
    public void Attach(RectTransform card, string key, TufPreviewSource source) {
        if(!source.HasThumbnail) return;
        Mask mask = card.gameObject.AddComponent<Mask>();
        mask.showMaskGraphic = true;
        RectTransform host = Fill("Preview", card);
        CanvasGroup group = host.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;
        TufCoverImage image = host.gameObject.AddComponent<TufCoverImage>();
        image.raycastTarget = false;
        image.color = Color.white;
        Image scrim = Fill("Scrim", host).gameObject.AddComponent<Image>();
        scrim.color = new Color(0f, 0f, 0f, 0.5f);
        scrim.raycastTarget = false;
        slots[key] = new Slot { Image = image, Group = group };
        if(TufPreviewCache.TryGet(key, out Texture2D ready) && ready != null) Apply(key, ready);
        else TufPreviewCache.Request(key, source);
    }
    public void Tick() {
        if(!dirty) return;
        dirty = false;
        foreach(KeyValuePair<string, Slot> pair in slots) {
            if(pair.Value.Applied || pair.Value.Image == null) continue;
            if(TufPreviewCache.TryGet(pair.Key, out Texture2D tex) && tex != null) Apply(pair.Key, tex);
        }
    }
    private void Apply(string key, Texture2D texture) {
        if(!slots.TryGetValue(key, out Slot slot) || slot.Applied) return;
        if(slot.Image == null || slot.Group == null) return;
        slot.Image.SetCover(texture);
        slot.Applied = true;
        slot.Fade?.Kill();
        slot.Fade = slot.Group.GTAlpha(1f, 0.28f).SetEasing(Easing.OutSine);
        MainCore.TC.Play(slot.Fade);
    }
    public void ClearSlots() {
        foreach(Slot slot in slots.Values) slot.Fade?.Kill();
        slots.Clear();
    }
    public void Dispose() {
        TufPreviewCache.Changed -= OnReady;
        ClearSlots();
    }
    private static RectTransform Fill(string name, Transform parent) {
        GameObject obj = new(name);
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return rect;
    }
    private sealed class Slot {
        public TufCoverImage Image;
        public CanvasGroup Group;
        public GTween Fade;
        public bool Applied;
    }
}
internal sealed class TufCoverImage : RawImage {
    private Texture cover;
    public void SetCover(Texture texture) {
        cover = texture;
        this.texture = texture;
        Recompute();
    }
    protected override void OnRectTransformDimensionsChange() {
        base.OnRectTransformDimensionsChange();
        Recompute();
    }
    private void Recompute() {
        if(cover == null) return;
        Rect r = rectTransform.rect;
        if(r.width < 1f || r.height < 1f || r.width < r.height) return;
        float tw = Mathf.Max(1, cover.width), th = Mathf.Max(1, cover.height);
        float ra = r.width / r.height, ta = tw / th;
        uvRect = ta > ra
            ? new Rect((1f - ra / ta) * 0.5f, 0f, ra / ta, 1f)
            : new Rect(0f, (1f - ta / ra) * 0.5f, 1f, ta / ra);
    }
}

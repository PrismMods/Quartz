using Quartz.Core;
using Quartz.Features.Panels;
using Quartz.Tween;
using Quartz.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using GTweens.Builders;
using GTweens.Easings;
using GTweens.Extensions;
using GTweens.Tweens;
using GTweenExtensions = GTweens.Extensions.GTweenExtensions;
using static UnityEngine.EventSystems.PointerEventData;
using Object = UnityEngine.Object;
namespace Quartz.UI.Factory.Page;
internal static partial class PagePanels {
    private sealed class PanelSectionMarker : MonoBehaviour {
        public PanelConfig Config;
    }
    private abstract class RowDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler {
        public RectTransform Row;
        private LayoutElement rowLE;
        private RectTransform placeholder;
        private float grabOffsetY;
        private GTween scaleSeq;
        private GTween dropSeq;
        private bool dragging;
        private readonly Dictionary<RectTransform, GTween> rowSlides = [];
        private readonly List<(RectTransform rt, Vector2 oldPos)> reflowCapture = [];
        protected abstract bool IsRow(Transform t);
        protected abstract void OnDropped();
        private void AnimateReflow(Transform container) {
            reflowCapture.Clear();
            for(int i = 0; i < container.childCount; i++) {
                Transform child = container.GetChild(i);
                if(child == Row || !IsRow(child)) continue;
                RectTransform rt = (RectTransform)child;
                reflowCapture.Add((rt, rt.anchoredPosition));
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)container);
            foreach((RectTransform rt, Vector2 oldPos) in reflowCapture) {
                Vector2 target = rt.anchoredPosition;
                if((target - oldPos).sqrMagnitude < 0.01f) continue;
                rt.anchoredPosition = oldPos;
                if(rowSlides.TryGetValue(rt, out GTween running)) running?.Kill();
                GTween slide = GTweenExtensions.Tween(
                    () => rt.anchoredPosition.y,
                    y => rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, y),
                    target.y,
                    0.15f
                ).SetEasing(Easing.OutCubic);
                rowSlides[rt] = slide;
                MainCore.TC.Play(slide);
            }
        }
        public void OnBeginDrag(PointerEventData eventData) {
            if(Row == null || Row.parent == null) return;
            dropSeq.CompleteAndKill();
            dropSeq = null;
            dragging = true;
            rowLE = Row.GetComponent<LayoutElement>();
            if(rowLE == null) rowLE = Row.gameObject.AddComponent<LayoutElement>();
            GameObject ph = new("DragPlaceholder");
            ph.transform.SetParent(Row.parent, false);
            placeholder = ph.AddComponent<RectTransform>();
            LayoutElement phLE = ph.AddComponent<LayoutElement>();
            phLE.preferredHeight = Row.rect.height;
            phLE.minHeight = Row.rect.height;
            placeholder.SetSiblingIndex(Row.GetSiblingIndex());
            rowLE.ignoreLayout = true;
            Row.SetAsLastSibling();
            grabOffsetY = Row.position.y - eventData.position.y;
            PlayScale(1.04f);
        }
        public void OnDrag(PointerEventData eventData) {
            if(!dragging || Row == null || placeholder == null) return;
            Vector3 pos = Row.position;
            pos.y = eventData.position.y + grabOffsetY;
            Row.position = pos;
            Transform container = Row.parent;
            int target = 0;
            for(int i = 0; i < container.childCount; i++) {
                Transform child = container.GetChild(i);
                if(child == Row || child == placeholder) continue;
                if(!IsRow(child)) continue;
                if(((RectTransform)child).position.y > eventData.position.y) target++;
            }
            if(placeholder.GetSiblingIndex() != target) {
                placeholder.SetSiblingIndex(target);
                AnimateReflow(container);
            }
        }
        public void OnEndDrag(PointerEventData eventData) {
            if(!dragging || Row == null || placeholder == null) return;
            dragging = false;
            Transform container = Row.parent;
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)container);
            float targetY = placeholder.position.y;
            int finalIndex = placeholder.GetSiblingIndex();
            RectTransform ph = placeholder;
            placeholder = null;
            PlayScale(1f);
            dropSeq = GTweenSequenceBuilder.New()
                .Append(GTweenExtensions.Tween(
                    () => Row.position.y,
                    y => {
                        Vector3 pos = Row.position;
                        pos.y = y;
                        Row.position = pos;
                    },
                    targetY,
                    0.12f
                ).SetEasing(Easing.OutCubic))
                .AppendCallback(() => {
                    if(ph != null) {
                        ph.gameObject.SetActive(false);
                        Object.Destroy(ph.gameObject);
                    }
                    if(rowLE != null) rowLE.ignoreLayout = false;
                    if(Row != null) {
                        Row.SetSiblingIndex(finalIndex);
                        Row.localScale = Vector3.one;
                        AnimateReflow(Row.parent);
                    }
                    OnDropped();
                })
                .Build();
            MainCore.TC.Play(dropSeq);
        }
        private void PlayScale(float target) {
            if(Row == null) return;
            scaleSeq?.Kill();
            scaleSeq = GTweenExtensions.Tween(
                () => Row.localScale.x,
                x => Row.localScale = new Vector3(x, x, 1f),
                target,
                0.12f
            ).SetEasing(Easing.OutSine);
            MainCore.TC.Play(scaleSeq);
        }
    }
    private sealed class PanelLayerDrag : RowDrag {
        protected override bool IsRow(Transform t) => t.GetComponent<PanelSectionMarker>() != null;
        protected override void OnDropped() => CommitPanelOrder();
    }
    private sealed class StatRowMarker : MonoBehaviour {
        public StatEntry Entry;
    }
    private sealed class StatRowDrag : RowDrag {
        public Action OnReordered;
        protected override bool IsRow(Transform t) => t.GetComponent<StatRowMarker>() != null;
        protected override void OnDropped() => OnReordered?.Invoke();
    }
}

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

// Drag-to-reorder machinery: a marker that ties a panel section to its config,
// the abstract RowDrag engine shared between panel layers and stat rows, and
// the two concrete subclasses.
internal static partial class PagePanels {
    private sealed class PanelSectionMarker : MonoBehaviour {
        public PanelConfig Config;
    }

    // Shared drag-to-reorder engine. The dragged row leaves the layout and
    // floats with the pointer (slightly scaled up); a placeholder gap slides
    // through the list to mark the drop slot. On release the row glides into
    // the gap, rejoins the layout at the gap's slot, and the subclass commits
    // the new hierarchy order.
    private abstract class RowDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler {
        public RectTransform Row;

        private LayoutElement rowLE;
        private RectTransform placeholder;
        private float grabOffsetY;
        private GTween scaleSeq;
        private GTween dropSeq;
        private bool dragging;

        // Per-row slide tweens for the NOT-dragged rows: every placeholder
        // move reflows the layout instantly, which would teleport the rows
        // shoved aside — instead their old visual position is captured, the
        // layout is rebuilt, and they glide from old to new slot.
        private readonly Dictionary<RectTransform, GTween> rowSlides = [];
        private readonly List<(RectTransform rt, Vector2 oldPos)> reflowCapture = [];

        // Which siblings count as reorderable rows.
        protected abstract bool IsRow(Transform t);

        // Commits the new order once the drop animation has rejoined the
        // layout.
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

            // A previous drop animation still running: jump it to its end so
            // the placeholder/layout state is clean before re-grabbing.
            dropSeq.CompleteAndKill();
            dropSeq = null;

            dragging = true;

            // Stat rows carry a LayoutElement already; panel sections size
            // themselves with a ContentSizeFitter and get an inert one added
            // here — either way it lifts the row out of the list layout
            // (ignoreLayout) while it floats.
            rowLE = Row.GetComponent<LayoutElement>();
            if(rowLE == null) rowLE = Row.gameObject.AddComponent<LayoutElement>();

            // Gap that holds the row's slot while it floats.
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

            // Slot index = how many other rows sit above the pointer.
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

            // Glide into the gap, then rejoin the layout at the gap's slot.
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
                        // Rejoining the layout reflows once more — let any
                        // rows still mid-slide glide to their final slots
                        // instead of snapping.
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

    // Drag-to-reorder for whole panel sections — the committed order is the
    // panels' layer (draw) order.
    private sealed class PanelLayerDrag : RowDrag {
        protected override bool IsRow(Transform t) => t.GetComponent<PanelSectionMarker>() != null;
        protected override void OnDropped() => CommitPanelOrder();
    }

    // Ties a list row back to its config entry so a reorder commit can read
    // the new order straight off the hierarchy.
    private sealed class StatRowMarker : MonoBehaviour {
        public StatEntry Entry;
    }

    // Drag-to-reorder for stat rows — dropping commits the hierarchy order to
    // the panel config via OnReordered.
    private sealed class StatRowDrag : RowDrag {
        public Action OnReordered;

        protected override bool IsRow(Transform t) => t.GetComponent<StatRowMarker>() != null;
        protected override void OnDropped() => OnReordered?.Invoke();
    }
}

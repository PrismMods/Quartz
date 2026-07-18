using Quartz.Core;
using Quartz.Tween;
using Quartz.UI.Generator;
using Quartz.UI.Utility;
using GTweens.Builders;
using GTweens.Easings;
using GTweens.Tweens;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static UnityEngine.EventSystems.PointerEventData;
namespace Quartz.UI.Editor;
/// <summary>
/// DM Note's PropertiesPanel tab strip: `flex w-full h-[30px] bg-[#26262C] rounded-[7px] p-[3px]
/// gap-[5px]` holding `h-[24px] rounded-[7px]` pills.
///
/// Its own builder rather than <see cref="GenerateUI.SegmentedControl"/> or
/// <see cref="KvWidgets.Segments"/>, for two reasons. It is a different control: a tab strip has a
/// recessed track the selected pill sits in, where a segmented control is a row of buttons on the
/// panel. And the shared one re-applies <c>UIColors.ObjectActive</c> on every change from inside
/// its own Refresh, so colours restyled from outside would be overwritten on the first click —
/// while every other page depends on it looking exactly as it does.
///
/// Pills take the width their label asks for and the strip drag-scrolls when they overflow, the
/// same trade <see cref="KvTabStrip"/> makes: five 1.5x-scaled labels do not fit the panel, and
/// squeezing them (equal share + auto-size) rendered every tab cramped rather than one of them
/// reachable by a drag. The selected pill is scrolled into view on rebuild, so picking a hidden
/// tab does not leave it hidden.
///
/// The in-panel segmented controls stay on the shared builder. Only the tabs are this.
/// </summary>
internal static class KvTabs {
    /// <summary>30px track.</summary>
    private static float TrackHeight => 30f * KvPalette.Scale;
    /// <summary>3px track padding.</summary>
    private static float TrackPad => 3f * KvPalette.Scale;
    /// <summary>5px gap between pills.</summary>
    private static float PillGap => 5f * KvPalette.Scale;
    /// <summary>24px pill.</summary>
    private static float PillHeight => 24f * KvPalette.Scale;
    /// <summary>text-style-2.</summary>
    private static float LabelSize => 13f * KvPalette.Scale;
    /// <summary>8px px-, either side of the label — the pad KvTabStrip's buttons carry.</summary>
    private static float LabelPadX => 8f * KvPalette.Scale;
    internal static void Build<T>(
        RectTransform root, IReadOnlyList<T> values, Func<T, string> name, Func<T, string> key,
        T value, Action<T> onChanged
    ) {
        RectTransform track = GenerateUI.Row(root, TrackHeight);
        GameObject trackObj = track.gameObject;
        Image bg = trackObj.AddComponent<Image>();
        bg.sprite = MainCore.Spr.GetFilled(KvPalette.Radius);
        bg.type = Image.Type.Sliced;
        bg.color = KvPalette.TabTrack;
        bg.raycastTarget = false;
        // The viewport is inset by the track pad as a float offset, not a layout-group RectOffset:
        // RectOffset is ints, so the 4.5px pad rounded to 4 and the first pill sat a pixel over the
        // track's rounded edge. The mask also clips scrolled pills at the inset, so the track's
        // border stays visible however far the strip is dragged.
        GameObject viewObj = new("TabViewport");
        viewObj.transform.SetParent(track, false);
        RectTransform viewport = viewObj.AddComponent<RectTransform>();
        viewport.anchorMin = Vector2.zero;
        viewport.anchorMax = Vector2.one;
        viewport.offsetMin = new Vector2(TrackPad, TrackPad);
        viewport.offsetMax = new Vector2(-TrackPad, -TrackPad);
        // Transparent: the track behind it is already the recessed fill, and a second one here
        // would darken it. Raycastable anyway, or the ScrollRect never receives a drag.
        viewObj.AddComponent<EmptyGraphic>().raycastTarget = true;
        viewObj.AddComponent<RectMask2D>();
        GameObject tabsObj = new("Tabs");
        tabsObj.transform.SetParent(viewport, false);
        RectTransform tabs = tabsObj.AddComponent<RectTransform>();
        tabs.anchorMin = new Vector2(0f, 0f);
        tabs.anchorMax = new Vector2(0f, 1f);
        tabs.pivot = new Vector2(0f, 0.5f);
        HorizontalLayoutGroup layout = tabsObj.AddComponent<HorizontalLayoutGroup>();
        // gap-[5px]. No padding of its own: the viewport inset already carries the track's p-[3px].
        layout.spacing = PillGap;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        ContentSizeFitter fitter = tabsObj.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
        ScrollRect scroll = viewObj.AddComponent<ScrollRect>();
        scroll.content = tabs;
        scroll.viewport = viewport;
        scroll.horizontal = true;
        scroll.vertical = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        // Drag-only, as KvTabStrip is. The panel behind this has its own scroll, so a live
        // sensitivity here would scroll the strip and the panel at once.
        scroll.scrollSensitivity = 0f;
        scroll.inertia = false;
        viewObj.AddComponent<KvToolbar.StripWheel>().Init(viewport, tabs);
        List<(T Value, Image Bg, TextMeshProUGUI Label)> pills = [];
        List<(LayoutElement Le, TextMeshProUGUI Label, float Pad)> measured = [];
        RectTransform selectedPill = null;
        T current = value;
        // Animate only on a user switch; the first paint is instant so tabs do not fade in on build.
        void Refresh(bool animate) {
            foreach((T optValue, Image pillBg, TextMeshProUGUI label) in pills) {
                bool on = EqualityComparer<T>.Default.Equals(optValue, current);
                Color bgTo = on ? KvPalette.TabActive : KvPalette.TabTrack;
                Color labelTo = on ? KvPalette.TextWhite : KvPalette.TabIdleText;
                if(animate) {
                    MainCore.TC.Play(pillBg.GTColor(bgTo, 0.13f).SetEasing(Easing.OutSine));
                    MainCore.TC.Play(label.GTColor(labelTo, 0.13f).SetEasing(Easing.OutSine));
                } else {
                    pillBg.color = bgTo;
                    label.color = labelTo;
                }
            }
        }
        foreach(T optValue in values) {
            T captured = optValue;
            string text = name(optValue);
            GameObject obj = new("Tab_" + text.Replace(" ", ""));
            obj.transform.SetParent(tabs, false);
            RectTransform rect = obj.AddComponent<RectTransform>();
            LayoutElement le = obj.AddComponent<LayoutElement>();
            le.minHeight = PillHeight;
            le.preferredHeight = PillHeight;
            le.flexibleWidth = 0f;
            Image pillBg = obj.AddComponent<Image>();
            // Nested corner: the pill sits TrackPad inside the track's 7px radius, so its own
            // corner is outer-minus-inset — which lands exactly on RadiusSmall. At the full 7px its
            // arc poked past the track's at the corners.
            pillBg.sprite = MainCore.Spr.GetFilled(KvPalette.RadiusSmall);
            pillBg.type = Image.Type.Sliced;
            TextMeshProUGUI label = GenerateUI.AddText(rect, true);
            label.fontSize = LabelSize;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.raycastTarget = false;
            if(key != null) GenerateUI.Localize(label, key(optValue), text);
            else label.text = text;
            // The width the label asks for, not an equal share: the strip scrolls instead of
            // squeezing. Re-measured for a few frames by KvTabRemeasure below — TMP's first-frame
            // widths are wrong until the font atlas is populated.
            float width = label.GetPreferredValues(label.text).x + LabelPadX * 2f;
            le.preferredWidth = width;
            le.minWidth = width;
            measured.Add((le, label, LabelPadX * 2f));
            pills.Add((optValue, pillBg, label));
            if(EqualityComparer<T>.Default.Equals(optValue, current)) selectedPill = rect;
            GenerateUI.AddButton(obj, btn => {
                if(btn != InputButton.Left) return;
                if(EqualityComparer<T>.Default.Equals(current, captured)) return;
                current = captured;
                Refresh(true);
                onChanged?.Invoke(captured);
                // Not hovered back: the click rebuilds the panel under the cursor, so the pill this
                // came from may not exist by the time a hover-exit would land.
            }, false);
            EventTrigger trigger = obj.GetComponent<EventTrigger>() ?? obj.AddComponent<EventTrigger>();
            // Without this, the pill's EventTrigger eats the drag and the strip never scrolls —
            // a drag could only start from the gaps between pills.
            KvToolbar.ForwardDrag(trigger, scroll);
            // Hover lift on idle pills only — the selected pill has no hover state in DM Note; it
            // stays put. Checked against `current` live, not at build: the strip outlives a
            // selection switch, so a pill's role can change under the pointer.
            GTween hoverSeq = null;
            void HoverTo(Color bgTo, Color labelTo) {
                hoverSeq?.Kill();
                hoverSeq = GTweenSequenceBuilder.New()
                    .Join(pillBg.GTColor(bgTo, 0.12f).SetEasing(Easing.OutSine))
                    .Join(label.GTColor(labelTo, 0.12f).SetEasing(Easing.OutSine))
                    .Build();
                MainCore.TC.Play(hoverSeq);
            }
            UnityUtils.AddEvent(EventTriggerType.PointerEnter, _ => {
                if(EqualityComparer<T>.Default.Equals(current, captured)) return;
                HoverTo(KvPalette.SurfaceHover, KvPalette.TextWhite);
            }, trigger);
            UnityUtils.AddEvent(EventTriggerType.PointerExit, _ => {
                if(EqualityComparer<T>.Default.Equals(current, captured)) return;
                HoverTo(KvPalette.TabTrack, KvPalette.TabIdleText);
            }, trigger);
        }
        Refresh(false);
        KvTabRemeasure.Attach(tabs, measured);
        // Every rebuild resets the scroll to the left edge, so without this, picking a tab past the
        // fold (Push tears the strip down and rebuilds it) would leave the tab that was just
        // clicked off screen while its content shows.
        if(selectedPill != null) {
            RevealSelected reveal = viewObj.AddComponent<RevealSelected>();
            reveal.Content = tabs;
            reveal.Viewport = viewport;
            reveal.Pill = selectedPill;
        }
    }
    /// <summary>
    /// Scrolls the selected pill into view once the strip's widths have settled. The wait mirrors
    /// <see cref="KvTabRemeasure"/>: pill widths move for a few frames while the font atlas
    /// populates, and a reveal computed from the first-frame layout would aim at the wrong offset.
    /// One-shot — it positions the content, then removes itself.
    /// </summary>
    private sealed class RevealSelected : MonoBehaviour {
        internal RectTransform Content, Viewport, Pill;
        private float lastWidth = -1f;
        private int stableFrames, frames;
        private const int MaxFrames = 120;
        private const int StableThreshold = 2;
        private void LateUpdate() {
            if(Content == null || Viewport == null || Pill == null) {
                Destroy(this);
                return;
            }
            float width = Content.rect.width;
            if(Mathf.Abs(width - lastWidth) < 0.5f) stableFrames++;
            else stableFrames = 0;
            lastWidth = width;
            if(++frames < MaxFrames && stableFrames < StableThreshold) return;
            float viewWidth = Viewport.rect.width;
            float overflow = width - viewWidth;
            if(overflow > 0f) {
                // Pill edges in content space: its layout-driven position is measured from the
                // content's left edge (pivot 0), its own pivot is centred.
                float pillMax = Pill.localPosition.x + Pill.rect.xMax;
                float shift = Mathf.Clamp(Mathf.Min(0f, viewWidth - pillMax), -overflow, 0f);
                Content.anchoredPosition = new Vector2(shift, Content.anchoredPosition.y);
            }
            Destroy(this);
        }
    }
}

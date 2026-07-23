using Quartz.Core;
using Quartz.Resource;
using Quartz.Tween;
using Quartz.UI.Utility;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static UnityEngine.EventSystems.PointerEventData;
using GTweens.Builders;
using GTweens.Easings;
using GTweens.Extensions;
using GTweens.Tweens;
using GTweenExtensions = GTweens.Extensions.GTweenExtensions;
using TMPro;
namespace Quartz.UI.Generator;
public static partial class GenerateUI {
    public sealed class CollapsibleSection {
        public string Title;
        public RectTransform Section;
        public GameObject HeaderObj;
        public RectTransform Body;
        public bool Expanded;
        internal string stateKey;
        internal Image arrow;
        internal System.Action applyState;
        internal System.Action applyInstant;
        public void SetExpanded(bool v) => SetExpanded(v, true, true);
        public void SetExpanded(bool v, bool animate) => SetExpanded(v, animate, true);
        public void SetExpanded(bool v, bool animate, bool save) {
            if(Expanded == v) return;
            Expanded = v;
            if(save && !string.IsNullOrEmpty(stateKey)) {
                MainCore.Conf.SetCollapsibleExpanded(stateKey, v);
                MainCore.ConfMgr.RequestSave();
            }
            if(animate) applyState?.Invoke();
            else applyInstant?.Invoke();
        }
    }
    public static readonly List<CollapsibleSection> Sections = [];
    public static void ClearSections() => Sections.Clear();
    public static void PruneSections() => Sections.RemoveAll(s => s == null || s.Body == null);
    private static bool IsDynamicTitleList(Transform parent) =>
        parent != null && parent.name is "PanelsList" or "PracticeBindings";
    public static CollapsibleSection FlatSection(
        Transform parent,
        string title,
        Action<bool> onToggle = null,
        bool toggleValue = false,
        string enableLabel = null,
        string enableId = null
    ) {
        Localize(AddTextH1(Row(parent)), LocaleKeyFromText("SECTION", title), title);
        if(onToggle != null)
            Toggle(Row(parent), false, toggleValue, onToggle, enableLabel ?? ("Enable " + title), enableId);
        RectTransform body = parent as RectTransform;
        return new CollapsibleSection {
            Title = title,
            Section = body,
            Body = body,
            Expanded = true,
        };
    }
    private static string GetCollapsibleKey(Transform parent, string title) {
        List<string> parts = [];
        Transform current = parent;
        while(current != null) {
            string name = current.name;
            if(name.StartsWith("Page") || name.StartsWith("Section_")) parts.Add(name);
            current = current.parent;
        }
        parts.Reverse();
        parts.Add("Section_" + title);
        return string.Join("/", parts);
    }
    public static CollapsibleSection Collapsible(
        Transform parent,
        string title,
        bool startExpanded
    ) => Collapsible(parent, title, startExpanded, null, false);
    public static CollapsibleSection Collapsible(
        Transform parent,
        string title,
        bool startExpanded,
        Action<bool> onToggle,
        bool toggleValue
    ) {
        GameObject sectionObj = new("Section_" + title);
        sectionObj.transform.SetParent(parent, false);
        string stateKey = GetCollapsibleKey(parent, title);
        bool expanded = MainCore.Conf.GetCollapsibleExpanded(stateKey);
        RectTransform sectionRect = sectionObj.AddComponent<RectTransform>();
        GenerateUI.FitVertical(sectionObj, 6f);
        GameObject headerObj = new("Header");
        headerObj.transform.SetParent(sectionRect, false);
        LayoutElement headerLE = headerObj.AddComponent<LayoutElement>();
        headerLE.preferredHeight = 44f;
        headerLE.minHeight = 44f;
        GameObject barObj = new("Bar");
        barObj.transform.SetParent(headerObj.transform, false);
        RectTransform barRect = barObj.AddComponent<RectTransform>();
        barRect.anchorMin = new Vector2(0f, 0f);
        barRect.anchorMax = new Vector2(1f, 1f);
        barRect.offsetMin = Vector2.zero;
        barRect.offsetMax = new Vector2(-250f, 0f);
        Image headerBg = barObj.AddComponent<Image>();
        headerBg.color = UIColors.ObjectBG;
        headerBg.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P2048);
        headerBg.type = Image.Type.Sliced;
        headerBg.raycastTarget = true;
        GameObject arrowObj = new("Arrow");
        arrowObj.transform.SetParent(barObj.transform, false);
        RectTransform arrowRect = arrowObj.AddComponent<RectTransform>();
        arrowRect.anchorMin = new Vector2(1f, 0.5f);
        arrowRect.anchorMax = new Vector2(1f, 0.5f);
        arrowRect.pivot = new Vector2(0.5f, 0.5f);
        arrowRect.anchoredPosition = new Vector2(-23f, 0f);
        arrowRect.sizeDelta = new Vector2(26f, 26f);
        Image arrowImg = arrowObj.AddComponent<Image>();
        arrowImg.sprite = MainCore.Spr.Get(UISprite.Triangle128);
        arrowImg.color = UIColors.ObjectInactive;
        arrowImg.raycastTarget = false;
        if(onToggle != null) {
            GameObject toggleZone = new("HeaderToggle");
            toggleZone.transform.SetParent(barObj.transform, false);
            RectTransform zoneRect = toggleZone.AddComponent<RectTransform>();
            zoneRect.anchorMin = new Vector2(1f, 0.5f);
            zoneRect.anchorMax = new Vector2(1f, 0.5f);
            zoneRect.pivot = new Vector2(0.5f, 0.5f);
            zoneRect.anchoredPosition = new Vector2(-64f, 0f);
            zoneRect.sizeDelta = new Vector2(40f, 44f);
            toggleZone.AddComponent<EmptyGraphic>().raycastTarget = true;
            GameObject dotObj = new("Dot");
            dotObj.transform.SetParent(toggleZone.transform, false);
            RectTransform dotRect = dotObj.AddComponent<RectTransform>();
            dotRect.anchorMin = new Vector2(0.5f, 0.5f);
            dotRect.anchorMax = new Vector2(0.5f, 0.5f);
            dotRect.pivot = new Vector2(0.5f, 0.5f);
            dotRect.sizeDelta = new Vector2(26f, 26f);
            Image dotImg = dotObj.AddComponent<Image>();
            dotImg.sprite = MainCore.Spr.Get(UISprite.Circle256);
            dotImg.raycastTarget = false;
            bool on = toggleValue;
            Color OffColor() => new(1f, 1f, 1f, 0.18f);
            dotImg.color = on ? UIColors.ObjectActive : OffColor();
            GTween dotSeq = null;
            EventTrigger zoneTrigger = toggleZone.AddComponent<EventTrigger>();
            UnityUtils.AddClickEvent(zoneTrigger, e => {
                if(e.button != InputButton.Left) return;
                on = !on;
                onToggle(on);
                dotSeq?.Kill();
                dotSeq = GTweenSequenceBuilder.New()
                    .Append(dotImg.GTColor(on ? UIColors.ObjectActive : OffColor(), 0.15f)
                        .SetEasing(Easing.OutSine))
                    .Build();
                MainCore.TC.Play(dotSeq);
            });
        }
        GameObject labelObj = new("Label");
        labelObj.transform.SetParent(barObj.transform, false);
        RectTransform labelRect = labelObj.AddComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0f, 0f);
        labelRect.anchorMax = new Vector2(1f, 1f);
        labelRect.offsetMin = new Vector2(16f, 0f);
        labelRect.offsetMax = new Vector2(onToggle != null ? -88f : -44f, 0f);
        TextMeshProUGUI label = labelObj.AddComponent<TextMeshProUGUI>();
        label.font = FontManager.Current;
        label.fontSize = 24f;
        label.color = Color.white;
        label.alignment = TextAlignmentOptions.Left;
        label.verticalAlignment = VerticalAlignmentOptions.Middle;
        label.text = title;
        label.characterSpacing = -3f;
        label.raycastTarget = false;
        if(!IsDynamicTitleList(parent)) Localize(label, LocaleKeyFromText("SECTION", title), title);
        GameObject bodyObj = new("Body");
        bodyObj.transform.SetParent(sectionRect, false);
        RectTransform bodyRect = bodyObj.AddComponent<RectTransform>();
        VerticalLayoutGroup bodyLayout = GenerateUI.FitVertical(bodyObj, 8f);
        bodyLayout.padding = new RectOffset(20, 0, 0, 0);
        ContentSizeFitter bodyFitter = bodyObj.GetComponent<ContentSizeFitter>();
        bodyFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        LayoutElement bodyLE = bodyObj.AddComponent<LayoutElement>();
        bodyObj.AddComponent<RectMask2D>();
        CanvasGroup bodyCg = bodyObj.AddComponent<CanvasGroup>();
        CollapsibleSection c = new() {
            Title = title,
            Section = sectionRect,
            HeaderObj = headerObj,
            Body = bodyRect,
            Expanded = expanded,
            stateKey = stateKey,
            arrow = arrowImg,
        };
        Sections.Add(c);
        GTween openSeq = null;
        GTween arrowSeq = null;
        void Apply(bool animate) {
            bool exp = c.Expanded;
            Vector3 targetRot = exp ? new Vector3(0f, 0f, 180f) : Vector3.zero;
            Color targetCol = exp ? UIColors.ObjectActive : UIColors.ObjectInactive;
            bodyCg.blocksRaycasts = exp;
            bodyCg.interactable = exp;
            openSeq?.Kill();
            arrowSeq?.Kill();
            if(!animate) {
                bodyObj.SetActive(exp);
                bodyLayout.enabled = exp;
                bodyFitter.enabled = exp;
                bodyLE.preferredHeight = exp ? -1f : 0f;
                bodyCg.alpha = exp ? 1f : 0f;
                arrowRect.localRotation = Quaternion.Euler(targetRot);
                arrowImg.color = targetCol;
                LayoutRebuilder.ForceRebuildLayoutImmediate(sectionRect);
                return;
            }
            bodyObj.SetActive(true);
            bodyLayout.enabled = true;
            bodyFitter.enabled = true;
            bodyLE.preferredHeight = -1f;
            LayoutRebuilder.ForceRebuildLayoutImmediate(sectionRect);
            float content = bodyRect.rect.height;
            bodyLayout.enabled = false;
            bodyFitter.enabled = false;
            float to = exp ? content : 0f;
            bodyLE.preferredHeight = exp ? 0f : content;
            openSeq = GTweenSequenceBuilder.New()
                .Join(GTweenExtensions.Tween(
                    () => bodyLE == null ? to : bodyLE.preferredHeight,
                    x => {
                        if(bodyLE == null) return;
                        bodyLE.preferredHeight = Mathf.Max(0f, x);
                        if(sectionRect != null) LayoutRebuilder.ForceRebuildLayoutImmediate(sectionRect);
                    },
                    to,
                    0.16f
                ).SetEasing(exp ? Easing.OutBack : Easing.OutSine))
                .Join(GTweenExtensions.Tween(
                    () => bodyCg == null ? (exp ? 1f : 0f) : bodyCg.alpha,
                    x => { if(bodyCg != null) bodyCg.alpha = x; },
                    exp ? 1f : 0f,
                    0.16f
                ).SetEasing(Easing.OutSine))
                .AppendCallback(() => {
                    if(bodyObj == null || bodyLE == null) return;
                    if(c.Expanded) {
                        if(bodyLayout != null) bodyLayout.enabled = true;
                        if(bodyFitter != null) bodyFitter.enabled = true;
                        bodyLE.preferredHeight = -1f;
                        if(sectionRect != null) LayoutRebuilder.ForceRebuildLayoutImmediate(sectionRect);
                    }
                    else {
                        bodyObj.SetActive(false);
                        bodyLE.preferredHeight = 0f;
                    }
                })
                .Build();
            MainCore.TC.Play(openSeq);
            arrowSeq = GTweenSequenceBuilder.New()
                .Join(arrowRect.GTRotate(targetRot, 0.4f).SetEasing(Easing.OutBack))
                .Join(arrowImg.GTColor(targetCol, 0.2f).SetEasing(Easing.OutSine))
                .Build();
            MainCore.TC.Play(arrowSeq);
        }
        c.applyState = () => Apply(true);
        c.applyInstant = () => Apply(false);
        Apply(false);
        AddButton(barObj, btn => {
            if(btn == InputButton.Left) c.SetExpanded(!c.Expanded, true, true);
        });
        return c;
    }
}

using Quartz.Core;
using Quartz.Localization;
using Quartz.Resource;
using Quartz.UI.Generator;
using Quartz.UI.Transition;
using Quartz.UI.Utility;
using Quartz.Update;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using GTweens.Tweens;
using Quartz.Tween;
using GTweens.Builders;
using GTweens.Easings;
using TMPro;
using Quartz.UI.Nav;
namespace Quartz.UI.Factory;
public static class MenuFactory {
    public static Action<int> OnStateChanged;
    public sealed class MenuItem {
        public int state;
        public GameObject obj;
        public Image bg;
        public GTween hoverSeq;
        public TMP_Text label;
        public string categoryKey;
        public bool isCategory;
    }
    private static readonly List<MenuItem> items = [];
    private static readonly List<MenuItem> subItems = [];
    public static string CategoryFor(int state) => NavRegistry.CategoryKeyFor(state);
    private static string activeCategoryKey;
    private static readonly Dictionary<string, string> lastChildForCategory = new(StringComparer.Ordinal);
    private static GameObject updateBadge;
    private static bool updateHooked;
    public static void CreateMenu(Transform parent) {
        items.Clear();
        subItems.Clear();
        activeCategoryKey = null;
        CorePages.EnsureRegistered();
        PruneStaleLastChildren();
        float iconUnits = 28f * MainCore.Conf.UIScale;
        MenuItem settings = null;
        foreach(NavCategory category in NavRegistry.Categories) {
            IReadOnlyList<NavPage> children = NavRegistry.PagesIn(category.Key);
            if(children.Count == 0) continue;
            float scale = category.IconScale <= 0f ? 1f : category.IconScale;
            Sprite icon = category.IconAsset != null
                ? SpriteRegistry.Get(category.IconAsset)
                : MainCore.Spr.Get(category.Icon, iconUnits * scale);
            MenuItem item = CreateItem(
                parent, category.Title, category.LocaleKey, icon, category.Key, children[0].State, 28f * scale);
            if(category.Key == "settings") settings = item;
        }
        updateBadge = null;
        if(settings != null) {
            CreateUpdateBadge(settings.obj.transform);
            if(!updateHooked) {
                UpdateService.OnChanged += RefreshUpdateBadge;
                updateHooked = true;
            }
            RefreshUpdateBadge();
        }
        RefreshSubMenu(CategoryFor(UICore.CurrentMenuState), animate: false);
        ApplyState(UICore.CurrentMenuState, true);
    }
    private static void CreateUpdateBadge(Transform parent) {
        updateBadge = new GameObject("UpdateBadge");
        updateBadge.transform.SetParent(parent, false);
        RectTransform rect = updateBadge.AddComponent<RectTransform>();
        rect.anchorMin = new(1f, 0.5f);
        rect.anchorMax = new(1f, 0.5f);
        rect.pivot = new(0.5f, 0.5f);
        rect.anchoredPosition = new(-22f, 0f);
        rect.sizeDelta = new(10f, 10f);
        Image img = updateBadge.AddComponent<Image>();
        img.sprite = MainCore.Spr.Get(UISprite.Circle256, 10f * MainCore.Conf.UIScale);
        img.color = UIColors.SoftRed;
        img.raycastTarget = false;
        updateBadge.SetActive(false);
    }
    private static void RefreshUpdateBadge() {
        if(updateBadge == null) return;
        updateBadge.SetActive(UpdateService.Status == UpdateStatus.Available);
    }
    public static void RefreshTheme() {
        ApplyState(UICore.CurrentMenuState, true);
    }
    public static MenuItem CreateItem(Transform parent, string name, string localeKey, Sprite icon, string categoryKey, int state, float iconSize = 28f) {
        GameObject item = new(name);
        item.transform.SetParent(parent, false);
        RectTransform rect = item.AddComponent<RectTransform>();
        rect.anchorMin = new(0, 1);
        rect.anchorMax = new(1, 1);
        rect.pivot = new(0.5f, 1);
        rect.sizeDelta = new(0, 54);
        Image bg = item.AddComponent<Image>();
        bg.color = UIColors.MenuNormal;
        GameObject iconObj = new("Icon");
        iconObj.transform.SetParent(item.transform, false);
        RectTransform iconRect = iconObj.AddComponent<RectTransform>();
        iconRect.anchorMin = new(0, 0.5f);
        iconRect.anchorMax = new(0, 0.5f);
        iconRect.pivot = new(0, 0.5f);
        iconRect.anchoredPosition = new(24f - (iconSize - 28f) * 0.5f, 0);
        iconRect.sizeDelta = new(iconSize, iconSize);
        Image iconImg = iconObj.AddComponent<Image>();
        iconImg.sprite = icon;
        iconImg.raycastTarget = false;
        GameObject textObj = new("Text");
        textObj.transform.SetParent(item.transform, false);
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = new(0, 0);
        textRect.anchorMax = new(1, 1);
        textRect.offsetMin = new(70, 0);
        textRect.offsetMax = Vector2.zero;
        TMP_Text label = textObj.AddComponent<TextMeshProUGUI>();
        label.text = name;
        label.font = FontManager.Current;
        label.fontSize = 18;
        label.color = Color.white;
        label.alignment = TextAlignmentOptions.Left;
        label.verticalAlignment = VerticalAlignmentOptions.Middle;
        label.characterSpacing = -3f;
        label.gameObject.AddComponent<TextLocalization>().Init(localeKey ?? name.ToUpperInvariant(), name);
        MenuItem menuItem = new() {
            obj = item,
            bg = bg,
            state = state,
            categoryKey = categoryKey,
            label = label,
            isCategory = true
        };
        items.Add(menuItem);
        WireItemInteractions(menuItem, item, bg);
        return menuItem;
    }
    private static void CreateSubSeparator(Transform parent) {
        RectTransform rect = GenerateUI.Row(parent, 11f);
        rect.gameObject.name = "Separator";
        GameObject lineObj = new("Line");
        lineObj.transform.SetParent(rect, false);
        RectTransform line = lineObj.AddComponent<RectTransform>();
        line.anchorMin = new(0f, 0.5f);
        line.anchorMax = new(1f, 0.5f);
        line.offsetMin = new(24f, -0.5f);
        line.offsetMax = new(-16f, 0.5f);
        Image image = lineObj.AddComponent<Image>();
        image.color = UIColors.MenuHover;
        image.raycastTarget = false;
    }
    private static void CreateSubItem(Transform parent, string title, string key, int state) {
        RectTransform rect = GenerateUI.Row(parent, 40f);
        rect.gameObject.name = title;
        Image bg = rect.gameObject.AddComponent<Image>();
        bg.color = UIColors.MenuNormal;
        GameObject textObj = new("Text");
        textObj.transform.SetParent(rect, false);
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = new(0, 0);
        textRect.anchorMax = new(1, 1);
        textRect.offsetMin = new(24, 0);
        textRect.offsetMax = Vector2.zero;
        TMP_Text label = textObj.AddComponent<TextMeshProUGUI>();
        label.text = title;
        label.font = FontManager.Current;
        label.fontSize = 16;
        label.color = Color.white;
        label.alignment = TextAlignmentOptions.Left;
        label.verticalAlignment = VerticalAlignmentOptions.Middle;
        label.characterSpacing = -3f;
        label.gameObject.AddComponent<TextLocalization>().Init(key, title);
        MenuItem menuItem = new() { obj = rect.gameObject, bg = bg, state = state, label = label, isCategory = false };
        subItems.Add(menuItem);
        WireItemInteractions(menuItem, rect.gameObject, bg);
    }
    private static void RefreshSubMenu(string categoryKey, bool animate) {
        if(categoryKey == activeCategoryKey) return;
        activeCategoryKey = categoryKey;
        foreach(var it in subItems) it.hoverSeq?.Kill();
        subItems.Clear();
        GenerateUI.ClearChildren(UICore.SubMenuContent);
        NavCategory category = NavRegistry.Category(categoryKey);
        bool has = NavRegistry.ShowsSubmenu(category);
        if(has) {
            bool first = true;
            foreach(NavPage child in NavRegistry.PagesIn(categoryKey)) {
                if(child.SeparatorBefore && !first) CreateSubSeparator(UICore.SubMenuContent);
                CreateSubItem(UICore.SubMenuContent, child.Title, child.LocaleKey, child.State);
                first = false;
            }
        }
        UICore.SetSubMenuVisible(has, animate);
    }
    private static void PruneStaleLastChildren() {
        List<string> stale = [];
        foreach(var kvp in lastChildForCategory)
            if(NavRegistry.ByKey(kvp.Value) == null) stale.Add(kvp.Key);
        foreach(string key in stale) lastChildForCategory.Remove(key);
    }
    private static void WireItemInteractions(MenuItem menuItem, GameObject item, Image bg) {
        var trigger = item.AddComponent<EventTrigger>();
        void Add(EventTriggerType type, Action cb) {
            var e = new EventTrigger.Entry { eventID = type };
            e.callback.AddListener(_ => cb());
            trigger.triggers.Add(e);
        }
        void HoverFade(EventTriggerType type, Func<Color> color, float duration) => Add(type, () => {
            if(IsSelected(menuItem, UICore.CurrentMenuState)) return;
            menuItem.hoverSeq?.Kill();
            menuItem.hoverSeq = GTweenSequenceBuilder.New()
                .Append(bg.GTColor(color(), duration).SetEasing(Easing.OutSine))
                .Build();
            MainCore.TC.Play(menuItem.hoverSeq);
        });
        HoverFade(EventTriggerType.PointerEnter, static () => UIColors.MenuHover, 0.2f);
        HoverFade(EventTriggerType.PointerExit, static () => UIColors.MenuNormal, 0.25f);
        UnityUtils.AddClickEvent(trigger, _ => {
            if(IsSelected(menuItem, UICore.CurrentMenuState)) return;
            SetState(menuItem.isCategory ? LastChildState(menuItem) : menuItem.state);
        });
    }
    private static int LastChildState(MenuItem category) {
        if(category.categoryKey == null) return category.state;
        string lastKey = lastChildForCategory.GetValueOrDefault(category.categoryKey);
        int state = lastKey == null ? -1 : NavRegistry.StateFor(lastKey);
        return state >= 0 ? state : category.state;
    }
    public static void SetState(int to) {
        int from = UICore.CurrentMenuState;
        if(from == to) return;
        UICore.CurrentMenuState = to;
        string cat = CategoryFor(to);
        if(cat != null) lastChildForCategory[cat] = NavRegistry.KeyFor(to);
        RefreshSubMenu(cat, animate: true);
        PageSwicher.SwitchPage(from, to);
        ApplyState(to);
        OnStateChanged?.Invoke(to);
    }
    private static bool IsSelected(MenuItem it, int currentState) =>
        it.isCategory ? CategoryFor(currentState) == it.categoryKey : it.state == currentState;
    private static void ApplyState(int id, bool noAnimate = false) {
        ApplyStateList(items, id, noAnimate);
        ApplyStateList(subItems, id, noAnimate);
    }
    private static void ApplyStateList(List<MenuItem> list, int id, bool noAnimate) {
        for(int i = 0; i < list.Count; i++) {
            var it = list[i];
            it.hoverSeq?.Kill();
            bool selected = IsSelected(it, id);
            if(selected) {
                if(noAnimate) {
                    it.bg.color = UIColors.MenuSelected;
                } else {
                    it.bg.color = UIColors.MenuHighlight;
                    it.hoverSeq = it.bg.GTColor(UIColors.MenuSelected, 0.3f).SetEasing(Easing.OutSine);
                    MainCore.TC.Play(it.hoverSeq);
                }
            } else {
                it.bg.color = UIColors.MenuNormal;
            }
        }
    }
}

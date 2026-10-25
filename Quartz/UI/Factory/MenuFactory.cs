using Quartz.Compat.Game;
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
using Quartz.Features;
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
        public Image underline;
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
        float iconUnits = 32f * MainCore.Conf.UIScale;
        MenuItem settings = null;
        foreach(NavCategory category in NavRegistry.Categories) {
            IReadOnlyList<NavPage> children = NavRegistry.PagesIn(category.Key);
            if(children.Count == 0) continue;
            float scale = category.IconScale <= 0f ? 1f : category.IconScale;
            Sprite icon = category.IconAsset != null
                ? SpriteRegistry.Get(category.IconAsset)
                : MainCore.Spr.Get(category.Icon, iconUnits * scale);
            MenuItem item = CreateItem(
                parent, category.Title, category.LocaleKey, icon, category.Key, children[0].State, 32f * scale);
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
        rect.anchorMin = new(0f, 0.5f);
        rect.anchorMax = new(0f, 0.5f);
        rect.pivot = new(0.5f, 0.5f);
        // Top-right of the icon, so it stays visible on the collapsed rail.
        rect.anchoredPosition = new(UICore.RailIconCenter + 12f, 12f);
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
    public static void SetLabelAlpha(float alpha) {
        foreach(MenuItem it in items) it.label.alpha = alpha;
    }
    public static void RefreshTheme() {
        ApplyState(UICore.CurrentMenuState, true);
    }
    public static MenuItem CreateItem(Transform parent, string name, string localeKey, Sprite icon, string categoryKey, int state, float iconSize = 32f) {
        GameObject item = new(name);
        item.transform.SetParent(parent, false);
        RectTransform rect = item.AddComponent<RectTransform>();
        rect.anchorMin = new(0, 1);
        rect.anchorMax = new(1, 1);
        rect.pivot = new(0.5f, 1);
        rect.sizeDelta = new(0, 54);
        // Inset rounded tile; a child so the icon and label keep the item's coordinates.
        GameObject bgObj = new("Bg");
        bgObj.transform.SetParent(item.transform, false);
        RectTransform bgRect = bgObj.AddComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        // 48x48 square on the collapsed rail; the right edge follows the rail as it widens.
        bgRect.offsetMin = new(UICore.RailIconCenter - 24f, 3f);
        bgRect.offsetMax = new(UICore.RailIconCenter + 24f - UICore.RAIL_WIDTH, -3f);
        Image bg = bgObj.AddComponent<Image>();
        bg.sprite = MainCore.Spr.GetFilled(8f);
        bg.type = Image.Type.Sliced;
        bg.color = UIColors.MenuNormal;
        GameObject iconObj = new("Icon");
        iconObj.transform.SetParent(item.transform, false);
        RectTransform iconRect = iconObj.AddComponent<RectTransform>();
        iconRect.anchorMin = new(0, 0.5f);
        iconRect.anchorMax = new(0, 0.5f);
        iconRect.pivot = new(0.5f, 0.5f);
        iconRect.anchoredPosition = new(UICore.RailIconCenter, 0);
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
        label.font = FontManager.MenuFontAsset;
        label.fontSize = 18;
        label.color = Color.white;
        label.alignment = TextAlignmentOptions.Left;
        label.verticalAlignment = VerticalAlignmentOptions.Middle;
        label.characterSpacing = -3f;
        TextCompat.NoWrap(label);
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
        GameObject obj = new("Separator");
        obj.transform.SetParent(parent, false);
        obj.AddComponent<RectTransform>();
        LayoutElement le = obj.AddComponent<LayoutElement>();
        le.minWidth = 11f;
        le.preferredWidth = 11f;
        GameObject lineObj = new("Line");
        lineObj.transform.SetParent(obj.transform, false);
        RectTransform line = lineObj.AddComponent<RectTransform>();
        line.anchorMin = new(0.5f, 0f);
        line.anchorMax = new(0.5f, 1f);
        line.offsetMin = new(-0.5f, 4f);
        line.offsetMax = new(0.5f, -4f);
        Image image = lineObj.AddComponent<Image>();
        image.color = UIColors.MenuHover;
        image.raycastTarget = false;
    }
    private static void CreateSubItem(Transform parent, NavPage page) {
        string title = page.Title;
        GameObject obj = new(title);
        obj.transform.SetParent(parent, false);
        obj.AddComponent<RectTransform>();
        Image bg = obj.AddComponent<Image>();
        bg.sprite = MainCore.Spr.GetFilled(8f);
        bg.type = Image.Type.Sliced;
        bg.color = UIColors.MenuNormal;
        HorizontalLayoutGroup layout = obj.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(16, 16, 0, 0);
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;
        bool stub = FeaturePages.IsStub(page);
        GameObject textObj = new("Text");
        textObj.transform.SetParent(obj.transform, false);
        textObj.AddComponent<RectTransform>();
        TMP_Text label = textObj.AddComponent<TextMeshProUGUI>();
        label.text = title;
        label.font = FontManager.MenuFontAsset;
        label.fontSize = 17;
        label.color = Color.white;
        label.alignment = TextAlignmentOptions.Center;
        label.verticalAlignment = VerticalAlignmentOptions.Middle;
        label.characterSpacing = -3f;
        label.raycastTarget = false;
        TextCompat.NoWrap(label);
        label.gameObject.AddComponent<TextLocalization>().Init(page.LocaleKey, title);
        if(stub) label.alpha = 0.5f;
        GameObject lineObj = new("Underline");
        lineObj.transform.SetParent(obj.transform, false);
        lineObj.AddComponent<LayoutElement>().ignoreLayout = true;
        RectTransform lineRect = lineObj.GetComponent<RectTransform>();
        lineRect.anchorMin = new(0f, 0f);
        lineRect.anchorMax = new(1f, 0f);
        lineRect.pivot = new(0.5f, 0f);
        lineRect.offsetMin = new(10f, 0f);
        lineRect.offsetMax = new(-10f, 3f);
        Image underline = lineObj.AddComponent<Image>();
        underline.color = UIColors.ObjectActive;
        underline.raycastTarget = false;
        underline.enabled = false;
        MenuItem menuItem = new() { obj = obj, bg = bg, state = page.State, label = label, isCategory = false, underline = underline };
        subItems.Add(menuItem);
        WireItemInteractions(menuItem, obj, bg);
    }
    private static void RefreshSubMenu(string categoryKey, bool animate) {
        if(categoryKey == activeCategoryKey) return;
        activeCategoryKey = categoryKey;
        foreach(var it in subItems) it.hoverSeq?.Kill();
        subItems.Clear();
        GenerateUI.ClearChildren(UICore.SubMenuContent);
        UICore.SubMenuContent.anchoredPosition = Vector2.zero;
        NavCategory category = NavRegistry.Category(categoryKey);
        bool has = NavRegistry.ShowsSubmenu(category);
        if(has) {
            bool first = true;
            foreach(NavPage child in NavRegistry.PagesIn(categoryKey)) {
                if(child.SeparatorBefore && !first) CreateSubSeparator(UICore.SubMenuContent);
                CreateSubItem(UICore.SubMenuContent, child);
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
        GenerateUI.AddOutlineHover(bg.gameObject, trigger);
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
        ApplyState(to, from: from);
        OnStateChanged?.Invoke(to);
    }
    private static bool IsSelected(MenuItem it, int currentState) =>
        it.isCategory ? CategoryFor(currentState) == it.categoryKey : it.state == currentState;
    private static void ApplyState(int id, bool noAnimate = false, int from = -1) {
        ApplyStateList(items, id, noAnimate, from);
        ApplyStateList(subItems, id, noAnimate, from);
    }
    private static void ApplyStateList(List<MenuItem> list, int id, bool noAnimate, int from) {
        for(int i = 0; i < list.Count; i++) {
            var it = list[i];
            it.hoverSeq?.Kill();
            bool selected = IsSelected(it, id);
            if(it.underline != null) it.underline.enabled = selected;
            if(it.label != null) {
                // Keep alpha: the collapsed rail hides its labels through it.
                Color c = selected ? Color.white : UIColors.TextSecondary;
                c.a = it.label.color.a;
                it.label.color = c;
            }
            if(selected) {
                // Only a newly selected item flashes; the rail tile stays put across sub tab switches.
                if(noAnimate || (from >= 0 && IsSelected(it, from))) {
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

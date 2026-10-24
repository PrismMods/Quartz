using Quartz.Async;
using Quartz.Core;
using Quartz.UI.Generator;
using Quartz.UI.Objects.Impl;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace Quartz.UI;
public static partial class UICore {
    private static Image powerBg;
    private static bool powerPending;
    private static CanvasGroup pageCanvasGroup;
    private static GameObject inactiveOverlay;
    private static Color PowerColor(bool on) => on ? new Color(0f, 0f, 0f, 0.1f) : UIColors.SoftRed;
    private static void TogglePower() {
        if(powerPending) return;
        bool enable = MainCore.Conf.Active = !MainCore.Conf.Active;
        if(powerBg != null) powerBg.color = PowerColor(enable);
        powerPending = true;
        ApplyPowerLock();
        // Toggling re-patches every feature and stalls for ~1.5 s, so let the new color
        // render first (the nested Enqueue lands a frame later) and drop clicks meanwhile.
        MainThread.Enqueue(() => MainThread.Enqueue(() => {
            try {
                MainCore.SetModEnabled(enable);
            } finally {
                powerPending = false;
            }
        }));
    }
    // Powered off, pages sit under an "inactive" cover and nothing behind it takes input.
    private static void ApplyPowerLock() {
        bool on = MainCore.Conf.Active;
        if(pageCanvasGroup != null) {
            pageCanvasGroup.interactable = on;
            pageCanvasGroup.blocksRaycasts = on;
        }
        if(bandCanvasGroup != null) {
            bandCanvasGroup.interactable = on;
            bandCanvasGroup.blocksRaycasts = on;
        }
        if(inactiveOverlay != null) inactiveOverlay.SetActive(!on);
    }
    private static void CreateInactiveOverlay(RectTransform page) {
        inactiveOverlay = new GameObject("InactiveOverlay");
        inactiveOverlay.transform.SetParent(page, false);
        RectTransform rect = inactiveOverlay.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        // The page group blocks raycasts while off; the cover's own button must still work.
        inactiveOverlay.AddComponent<CanvasGroup>().ignoreParentGroups = true;
        Image bg = inactiveOverlay.AddComponent<Image>();
        bg.color = UIColors.PanelBG;
        RectTransform title = Centered(rect, 46f, 34f);
        TextMeshProUGUI titleText = GenerateUI.AddTextH1(title);
        titleText.alignment = TextAlignmentOptions.Center;
        GenerateUI.Localize(titleText, "INACTIVE_TITLE", "Quartz is inactive");
        RectTransform hint = Centered(rect, 30f, -8f);
        TextMeshProUGUI hintText = GenerateUI.AddMutedText(hint, 17f, 0.5f, true);
        hintText.alignment = TextAlignmentOptions.Center;
        GenerateUI.Localize(hintText, "INACTIVE_HINT", "Every feature is off. Settings can't be changed until you turn it back on.");
        UIButton turnOn = GenerateUI.Button(rect, TogglePower, "Turn On", "inactive_turn_on");
        RectTransform button = turnOn.Rect;
        button.anchorMin = new Vector2(0.5f, 0.5f);
        button.anchorMax = new Vector2(0.5f, 0.5f);
        button.pivot = new Vector2(0.5f, 0.5f);
        button.sizeDelta = new Vector2(180f, 46f);
        button.anchoredPosition = new Vector2(0f, -64f);
        inactiveOverlay.SetActive(false);
    }
    private static RectTransform Centered(RectTransform parent, float height, float y) {
        GameObject obj = new("Row");
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(1f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(-48f, height);
        rect.anchoredPosition = new Vector2(0f, y);
        return rect;
    }
}

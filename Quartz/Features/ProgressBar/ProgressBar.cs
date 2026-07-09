using Quartz.Core;
using Quartz.Features.Status;
using Quartz.IO;
using Quartz.Resource;
using Quartz.UI;
using Quartz.UI.Utility;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;
namespace Quartz.Features.ProgressBar;
public static class ProgressBarOverlay {
    public static SettingsFile<ProgressBarSettings> ConfMgr { get; private set; }
    public static ProgressBarSettings Conf => ConfMgr.Data;
    private static GameObject canvasObj;
    private static RectTransform bar;
    private static RectTransform border;
    private static Image borderImg;
    private static Image back;
    private static RectTransform fillContainer;
    private static Image fill;
    private static GameObject dragObj;
    private static Updater updater;
    public static void EnsureConf() => ConfMgr ??= SettingsFile<ProgressBarSettings>.Loaded("ProgressBar.json");
    public static void Initialize(GameObject root) {
        if(canvasObj != null) return;
        EnsureConf();
        canvasObj = UnityUtils.CreateOverlayCanvas("QuartzProgressBarCanvas", root.transform, 32755, out _);
        GameObject barObj = new("Bar");
        barObj.transform.SetParent(canvasObj.transform, false);
        bar = barObj.AddComponent<RectTransform>();
        bar.anchorMin = new Vector2(0.5f, 1f);
        bar.anchorMax = new Vector2(0.5f, 1f);
        bar.pivot = new Vector2(0.5f, 1f);
        GameObject borderObj = new("Border");
        borderObj.transform.SetParent(bar, false);
        border = borderObj.AddComponent<RectTransform>();
        border.anchorMin = Vector2.zero;
        border.anchorMax = Vector2.one;
        borderImg = borderObj.AddComponent<Image>();
        borderImg.raycastTarget = false;
        GameObject backObj = new("Back");
        backObj.transform.SetParent(bar, false);
        RectTransform backRect = backObj.AddComponent<RectTransform>();
        backRect.anchorMin = Vector2.zero;
        backRect.anchorMax = Vector2.one;
        backRect.offsetMin = Vector2.zero;
        backRect.offsetMax = Vector2.zero;
        back = backObj.AddComponent<Image>();
        back.raycastTarget = false;
        GameObject containerObj = new("FillContainer");
        containerObj.transform.SetParent(bar, false);
        fillContainer = containerObj.AddComponent<RectTransform>();
        fillContainer.anchorMin = new Vector2(0f, 0f);
        fillContainer.anchorMax = new Vector2(0f, 1f);
        fillContainer.pivot = new Vector2(0f, 0.5f);
        GameObject fillObj = new("Fill");
        fillObj.transform.SetParent(fillContainer, false);
        RectTransform fillRect = fillObj.AddComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        fill = fillObj.AddComponent<Image>();
        fill.raycastTarget = false;
        GameObject drag = new("Drag");
        dragObj = drag;
        drag.transform.SetParent(bar, false);
        RectTransform dragRect = drag.AddComponent<RectTransform>();
        dragRect.anchorMin = Vector2.zero;
        dragRect.anchorMax = Vector2.one;
        dragRect.offsetMin = Vector2.zero;
        dragRect.offsetMax = Vector2.zero;
        drag.AddComponent<EmptyGraphic>().raycastTarget = true;
        drag.AddComponent<DragHandler>();
        drag.SetActive(false);
        updater = canvasObj.AddComponent<Updater>();
        Apply();
    }
    public static void Apply() {
        if(bar == null) return;
        bar.sizeDelta = new Vector2(Conf.Width, Conf.Height);
        bar.anchoredPosition = OverlayCalibration.Scale(new Vector2(Conf.OffsetX, -Conf.TopOffset));
        ApplyRounding(back, Conf.Rounding);
        ApplyRounding(fill, Conf.Rounding);
        if(back != null) back.color = Conf.GetBackColor();
        if(fill != null) {
            fill.color = Conf.GetFillColorForProgress(Mathf.Clamp01(GameStats.Progress));
        }
        ApplyOutline();
    }
    public static void Save() => ConfMgr?.Save();
    public static void ResetPosition() {
        ProgressBarSettings def = new();
        Conf.OffsetX = def.OffsetX;
        Conf.TopOffset = def.TopOffset;
        Apply();
        Save();
    }
    public static void Dispose() {
        if(canvasObj == null) return;
        ConfMgr?.Save();
        Object.Destroy(canvasObj);
        canvasObj = null;
        bar = null;
        border = null;
        borderImg = null;
        back = null;
        fillContainer = null;
        fill = null;
        dragObj = null;
        updater = null;
    }
    private static void ApplyRounding(Image img, float rounding) {
        if(img == null) return;
        if(rounding <= 0.5f) {
            img.sprite = null;
            img.type = Image.Type.Simple;
        } else {
            img.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P1024);
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = Mathf.Max(0.05f, 8f / rounding);
        }
    }
    private static void ApplyOutline() {
        if(border == null || borderImg == null) return;
        float t = Mathf.Max(0f, Conf.OutlineThickness);
        bool on = t > 0.01f;
        if(border.gameObject.activeSelf != on) border.gameObject.SetActive(on);
        if(!on) return;
        border.offsetMin = new Vector2(-t, -t);
        border.offsetMax = new Vector2(t, t);
        borderImg.color = Conf.GetOutlineColor();
        ApplyRounding(borderImg, Conf.Rounding + t);
    }
    private sealed class Updater : MonoBehaviour {
        private float lastStartX = float.NaN;
        private float lastFillW = float.NaN;
        private float lastGradientNow = float.NaN;
        private void Update() {
            if(bar == null) return;
            bool show = Panels.PanelsOverlay.IsEnabled && Conf.Enabled && GameStats.InGame;
            if(bar.gameObject.activeSelf != show) bar.gameObject.SetActive(show);
            if(dragObj != null && dragObj.activeSelf) dragObj.SetActive(false);
            if(!show) return;
            float start = Mathf.Clamp01(GameStats.RunStartProgress);
            float now = Mathf.Clamp01(GameStats.Progress);
            if(now < start) now = start;
            float fillFrom = Conf.PrefillStart ? 0f : start;
            float totalW = Conf.Width;
            float startX = totalW * fillFrom;
            float fillW = Mathf.Clamp(totalW * (now - fillFrom), 0f, totalW);
            if(startX != lastStartX || fillW != lastFillW) {
                fillContainer.anchoredPosition = new Vector2(startX, 0f);
                fillContainer.sizeDelta = new Vector2(fillW, 0f);
                lastStartX = startX;
                lastFillW = fillW;
            }
            if(fill != null && Conf.FillGradient is { Enabled: true } && now != lastGradientNow) {
                fill.color = Conf.FillGradient.Evaluate(now);
                lastGradientNow = now;
            }
        }
    }
}

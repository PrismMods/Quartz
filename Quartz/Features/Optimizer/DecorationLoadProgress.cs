using Quartz.Core;
using Quartz.Resource;
using Quartz.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace Quartz.Features.Optimizer;
// Small bottom-centre card shown while DecorationPreloader is decoding: a label with
// the count and a fill bar. Built once, then only toggled.
public static class DecorationLoadProgress {
    private const float WIDTH = 440f;
    private const float HEIGHT = 62f;
    private const float BAR_HEIGHT = 6f;
    private static GameObject canvasObj;
    private static RectTransform fillRect;
    private static TextMeshProUGUI label;
    private static bool visible;
    private static int lastDone = -1;
    private static int lastTotal = -1;
    private static bool Wanted => Optimizer.Conf == null || Optimizer.Conf.ShowDecorationLoadProgress;
    public static void Begin(int total) {
        if(!Wanted) return;
        try {
            if(canvasObj == null) Build();
            if(canvasObj == null) return;
            lastDone = lastTotal = -1;
            // Ties with the Quartz menu canvas (also 32767, same parent) are drawn in
            // sibling order, so go last to stay in front.
            canvasObj.transform.SetAsLastSibling();
            canvasObj.SetActive(true);
            visible = true;
            Report(0, total);
        } catch(Exception e) {
            Diag.Warn(e, "Optimizer/DecoProgress");
        }
    }
    public static void Report(int done, int total) {
        if(!visible || label == null) return;
        if(done == lastDone && total == lastTotal) return;
        lastDone = done;
        lastTotal = total;
        float t = total <= 0 ? 1f : Mathf.Clamp01(done / (float)total);
        fillRect.anchorMax = new(t, 1f);
        string fmt = MainCore.Tr.Get("OPT_DECO_LOADING", "Loading decorations… {0} / {1}");
        try {
            label.text = string.Format(fmt, done, total);
        } catch(FormatException e) {
            Diag.Ignore(e);
            label.text = $"{done} / {total}";
        }
    }
    public static void End() {
        if(!visible) return;
        visible = false;
        if(canvasObj != null) canvasObj.SetActive(false);
    }
    public static void Dispose() {
        visible = false;
        if(canvasObj != null) UnityEngine.Object.Destroy(canvasObj);
        canvasObj = null;
        fillRect = null;
        label = null;
    }
    private static void Build() {
        if(MainCore.Root == null) return;
        canvasObj = new GameObject("QuartzDecoLoadCanvas");
        canvasObj.transform.SetParent(MainCore.Root.transform, false);
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        // Top of the stack: the TUF launcher's full-screen loading cover sits at
        // 32766 while a chart opens, and this card must show over it.
        canvas.sortingOrder = 32767;
        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = UICore.ReferenceResolution;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        GameObject card = new("Card");
        card.transform.SetParent(canvasObj.transform, false);
        RectTransform cardRect = card.AddComponent<RectTransform>();
        cardRect.anchorMin = cardRect.anchorMax = new(0.5f, 0f);
        cardRect.pivot = new(0.5f, 0f);
        cardRect.anchoredPosition = new(0f, 48f);
        cardRect.sizeDelta = new(WIDTH, HEIGHT);
        Image bg = card.AddComponent<Image>();
        bg.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P1024);
        bg.type = Image.Type.Sliced;
        bg.color = UIColors.TopBar;
        bg.raycastTarget = false;

        GameObject text = new("Label");
        text.transform.SetParent(card.transform, false);
        RectTransform textRect = text.AddComponent<RectTransform>();
        textRect.anchorMin = new(0f, 0f);
        textRect.anchorMax = new(1f, 1f);
        textRect.offsetMin = new(20f, BAR_HEIGHT + 14f);
        textRect.offsetMax = new(-20f, -8f);
        label = text.AddComponent<TextMeshProUGUI>();
        label.font = FontManager.MenuFontAsset;
        label.fontSize = 17f;
        label.color = Color.white;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;

        GameObject track = new("Track");
        track.transform.SetParent(card.transform, false);
        RectTransform trackRect = track.AddComponent<RectTransform>();
        trackRect.anchorMin = new(0f, 0f);
        trackRect.anchorMax = new(1f, 0f);
        trackRect.pivot = new(0.5f, 0f);
        trackRect.offsetMin = new(20f, 12f);
        trackRect.offsetMax = new(-20f, 12f + BAR_HEIGHT);
        Image trackImg = track.AddComponent<Image>();
        trackImg.color = UIColors.MenuHover;
        trackImg.raycastTarget = false;

        GameObject fill = new("Fill");
        fill.transform.SetParent(track.transform, false);
        fillRect = fill.AddComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = new(0f, 1f);
        fillRect.offsetMin = fillRect.offsetMax = Vector2.zero;
        Image fillImg = fill.AddComponent<Image>();
        fillImg.color = UIColors.ObjectActive;
        fillImg.raycastTarget = false;

        canvasObj.SetActive(false);
    }
}

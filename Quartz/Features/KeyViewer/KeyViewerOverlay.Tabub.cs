using Quartz.Core;
using Quartz.Compat.Game;
using Quartz.Resource;
using Quartz.UI.Utility;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;
namespace Quartz.Features.KeyViewer;
public static partial class KeyViewerOverlay {
    private static RectTransform tabubRoot;
    private static RawImage tabubImage;
    private static GameObject tabubDragObj;
    private static Texture2D tabubCustomTex;
    private static string tabubCustomPath = "";
    private static void BuildTabub() {
        if(canvasObj == null || tabubRoot != null) return;
        GameObject obj = new("KeyViewerTabub");
        obj.transform.SetParent(canvasObj.transform, false);
        tabubRoot = obj.AddComponent<RectTransform>();
        tabubRoot.anchorMin = new Vector2(0.5f, 0f);
        tabubRoot.anchorMax = new Vector2(0.5f, 0f);
        tabubRoot.pivot = new Vector2(0.5f, 0.5f);
        tabubImage = obj.AddComponent<RawImage>();
        tabubImage.raycastTarget = false;
        tabubDragObj = ReorganizeHandle.CreateDragSurface(
            tabubRoot,
            static () => MainCore.Tr.Get("KEYVIEWER_TABUB", "This Tabub Is Mine"),
            StoreTabubPosition
        );
        obj.SetActive(false);
        ApplyTabub();
    }
    internal static void ApplyTabub() {
        if(tabubRoot == null || Conf == null) return;
        Texture2D tex = ResolveTabubTexture();
        tabubImage.texture = tex;
        tabubImage.enabled = tex != null;
        if(tex != null) tabubRoot.sizeDelta = new Vector2(tex.width, tex.height);
        tabubRoot.anchoredPosition = OverlayCalibration.Scale(new Vector2(Conf.TabubOffsetX, Conf.TabubOffsetY));
        float scale = Mathf.Clamp(Conf.TabubScale, 0.1f, 4f);
        tabubRoot.localScale = new Vector3(scale, scale, 1f);
        tabubAlpha = -1f;
        tabubTimesValid = false;
    }
    private static Texture2D ResolveTabubTexture() {
        string path = Conf.TabubImagePath ?? "";
        if(!string.Equals(path, tabubCustomPath, StringComparison.Ordinal)) {
            if(tabubCustomTex != null) Object.Destroy(tabubCustomTex);
            tabubCustomTex = null;
            tabubCustomPath = path;
            if(path.Length > 0) LoadTabubCustom(path);
        }
        return tabubCustomTex != null ? tabubCustomTex : MainCore.Res.Get<Texture2D>(Asset.Tabub);
    }
    private static void LoadTabubCustom(string path) {
        try {
            if(!File.Exists(path)) return;
            Texture2D tex = new(2, 2, TextureFormat.RGBA32, false) {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            if(tex.LoadImage(File.ReadAllBytes(path))) tabubCustomTex = tex;
            else Object.Destroy(tex);
        } catch(Exception ex) {
            MainCore.Log.Msg("[KeyViewer] Tabub image load failed: " + ex.Message);
        }
    }
    public static bool ImportTabubImage(out string error) {
        error = null;
        if(Conf == null) return false;
        string picked;
        try {
            picked = FileDialog.PickFile("", "Image", ["png", "jpg", "jpeg"], "Select tabub image");
        } catch(Exception ex) {
            error = "Picker failed: " + ex.Message;
            MainCore.Log.Msg("[KeyViewer] " + error);
            return false;
        }
        if(string.IsNullOrEmpty(picked)) return false;
        Conf.TabubImagePath = picked;
        ApplyTabub();
        Save();
        if(tabubCustomTex == null) {
            error = "Could not read that image";
            return false;
        }
        MainCore.Log.Msg("[KeyViewer] Tabub image set to " + picked);
        return true;
    }
    public static void ClearTabubImage() {
        if(Conf == null) return;
        Conf.TabubImagePath = "";
        ApplyTabub();
        Save();
    }
    public static bool HasTabubImage => tabubImage != null && tabubImage.texture != null;
    public static void ResetTabubPosition() {
        if(Conf == null) return;
        KeyViewerSettings def = new();
        Conf.TabubOffsetX = def.TabubOffsetX;
        Conf.TabubOffsetY = def.TabubOffsetY;
        ApplyTabub();
        Save();
    }
    private static void StoreTabubPosition() {
        if(!CaptureTabubPosition()) return;
        Save();
    }
    private static bool CaptureTabubPosition() {
        if(tabubRoot == null || Conf == null) return false;
        Vector2 stored = OverlayCalibration.Unscale(tabubRoot.anchoredPosition);
        Conf.TabubOffsetX = stored.x;
        Conf.TabubOffsetY = stored.y;
        return true;
    }
    private const float TabubFadeSeconds = 1f;
    private static float tabubAlpha = -1f;
    private static float tabubFadeInAt = -1f;
    private static float tabubFadeOutAt = -1f;
    private static bool tabubTimesValid;
    private static int tabubTimesFloors = -1;
    private static float tabubTimesPercent = -1f;
    private static bool TabubTriggered() {
        if(!tabubEnabled || !Status.GameStats.InGame || Status.GameStats.RunCleared) return false;
        return Status.GameStats.Progress * 100f >= tabubPercent;
    }
    private static void EnsureTabubTimes() {
        int floors = Status.GameStats.MapFloorCount;
        float total = Status.GameStats.MapTotalTimeSeconds;
        if(tabubTimesValid && floors == tabubTimesFloors
            && Mathf.Approximately(tabubTimesPercent, tabubPercent)
            && Mathf.Approximately(tabubFadeOutAt, total)) return;
        tabubTimesFloors = floors;
        tabubTimesPercent = tabubPercent;
        tabubFadeInAt = Status.GameStats.MapTimeAtProgress(tabubPercent / 100f);
        tabubFadeOutAt = total;
        tabubTimesValid = floors > 0 && tabubFadeInAt >= 0f && total > 0f;
    }
    private static float TabubTargetAlpha() {
        if(!tabubEnabled || !Conf.TabubEnabled) return 0f;
        if(!Status.GameStats.InGame || Status.GameStats.RunCleared) return 0f;
        EnsureTabubTimes();
        if(!tabubTimesValid) return tabubActive ? 1f : 0f;
        float now = Status.GameStats.MapTimeSeconds;
        float alpha = Mathf.Clamp01((now - tabubFadeInAt + TabubFadeSeconds) / TabubFadeSeconds);
        if(tabubFadeOutAt > tabubFadeInAt)
            alpha = Mathf.Min(alpha, Mathf.Clamp01((tabubFadeOutAt - now) / TabubFadeSeconds));
        return alpha;
    }
    private static void UpdateTabubState(float now) {
        bool active = TabubTriggered();
        if(active == tabubActive) return;
        tabubActive = active;
        if(!active) return;
        foreach(Box box in boxes) {
            if(box.LastRain != null) {
                box.LastRain.EndTime = now;
                box.LastRain = null;
            }
            if(box.LastGhostRain != null) {
                box.LastGhostRain.EndTime = now;
                box.LastGhostRain = null;
            }
            box.DelayedNotePending = false;
            box.DelayedReleasedBeforeStart = false;
            box.DelayedReleaseTime = -1f;
        }
    }
    private static void UpdateTabubVisibility(bool show, bool isReorganizing) {
        if(tabubRoot == null || Conf == null) return;
        float alpha = isReorganizing ? 1f : TabubTargetAlpha();
        bool want = show && Conf.TabubEnabled && alpha > 0.002f && tabubImage.texture != null;
        if(want && !Mathf.Approximately(alpha, tabubAlpha)) {
            tabubAlpha = alpha;
            Color c = tabubImage.color;
            c.a = alpha;
            tabubImage.color = c;
        }
        if(tabubRoot.gameObject.activeSelf != want) tabubRoot.gameObject.SetActive(want);
        bool dragWanted = want && isReorganizing;
        if(tabubDragObj != null && tabubDragObj.activeSelf != dragWanted) tabubDragObj.SetActive(dragWanted);
        if(dragWanted) CaptureTabubPosition();
    }
    private static void DisposeTabub() {
        if(tabubCustomTex != null) Object.Destroy(tabubCustomTex);
        tabubCustomTex = null;
        tabubCustomPath = "";
        tabubRoot = null;
        tabubImage = null;
        tabubDragObj = null;
        tabubAlpha = -1f;
        tabubTimesValid = false;
        tabubTimesFloors = -1;
        tabubTimesPercent = -1f;
    }
}

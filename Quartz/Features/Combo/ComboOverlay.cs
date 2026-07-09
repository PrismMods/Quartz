using System.Globalization;
using Quartz.Core;
using Quartz.Features.Interop;
using Quartz.Features.ProgressBar;
using Quartz.Features.Status;
using Quartz.IO;
using Quartz.Resource;
using Quartz.UI;
using Quartz.UI.Utility;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using TMPro;
namespace Quartz.Features.Combo;
public static class ComboOverlay {
    public static SettingsFile<ComboSettings> ConfMgr { get; private set; }
    public static ComboSettings Conf => ConfMgr.Data;
    private static GameObject canvasObj;
    private static GraphicRaycaster raycaster;
    private static RectTransform root;
    private static TextMeshProUGUI valueText;
    private static TextMeshProUGUI captionText;
    private static GameObject dragObj;
    private static Updater updater;
    private const float VerticalGap = 32f;
    private const float CaptionGap = 24f;
    public static void EnsureConf() => ConfMgr ??= SettingsFile<ComboSettings>.Loaded("Combo.json");
    public static void Initialize(GameObject rootObject) {
        if(canvasObj != null) return;
        EnsureConf();
        canvasObj = UnityUtils.CreateOverlayCanvas("QuartzComboCanvas", rootObject.transform, 32757, out raycaster);
        GameObject rootObj = new("ComboRoot");
        rootObj.transform.SetParent(canvasObj.transform, false);
        root = rootObj.AddComponent<RectTransform>();
        root.anchorMin = new Vector2(0.5f, 1f);
        root.anchorMax = new Vector2(0.5f, 1f);
        root.pivot = new Vector2(0.5f, 1f);
        valueText = CreateLabel(root, "Value", TextAlignmentOptions.Center);
        captionText = CreateLabel(root, "Caption", TextAlignmentOptions.Center);
        dragObj = ReorganizeHandle.CreateDragSurface(root, () => MainCore.Tr.Get("COMBO", "Combo"), Save);
        updater = canvasObj.AddComponent<Updater>();
        Apply();
    }
    private static TextMeshProUGUI CreateLabel(Transform parent, string name, TextAlignmentOptions align) {
        GameObject obj = new(name);
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        TextMeshProUGUI text = obj.AddComponent<TextMeshProUGUI>();
        text.font = FontManager.Current;
        text.alignment = align;
        text.color = Color.white;
        text.raycastTarget = false;
        text.text = "";
        return text;
    }
    public static void Apply() {
        if(root == null) return;
        ApplyFont();
        root.anchoredPosition = GetDefaultPosition();
        root.localScale = Vector3.one * Mathf.Max(0.01f, Conf.MasterSize);
        ApplyCaption();
        ApplyValueMaterial();
        ApplyCaptionMaterial();
    }
    private static void ApplyFont() {
        TMP_FontAsset font = FontManager.Current;
        if(valueText != null) valueText.font = font;
        if(captionText != null) captionText.font = font;
    }
    private static void ApplyCaption() {
        if(captionText == null) return;
        string caption = Conf.CaptionText ?? "Combo";
        if(Conf.XPerfectComboEnabled && XPerfectBridge.Active) caption = "X" + caption;
        captionText.text = caption;
        captionText.gameObject.SetActive(Conf.ShowCaption);
    }
    private static Vector2 GetDefaultPosition() {
        ProgressBarOverlay.EnsureConf();
        ProgressBarSettings bar = ProgressBarOverlay.Conf;
        float y = -(bar.TopOffset + bar.Height + VerticalGap + Conf.OffsetY);
        return OverlayCalibration.Scale(new Vector2(Conf.OffsetX, y));
    }
    public static void Save() => ConfMgr?.Save();
    public static void ResetPosition() {
        ComboSettings def = new();
        Conf.OffsetX = def.OffsetX;
        Conf.OffsetY = def.OffsetY;
        Apply();
        Save();
    }
    public static void ApplyCountShadow() => ApplyValueMaterial();
    public static void ApplyCaptionShadow() => ApplyCaptionMaterial();
    public static void Dispose() {
        if(canvasObj == null) return;
        if(root != null) {
            Vector2 stored = OverlayCalibration.Unscale(root.anchoredPosition);
            Conf.OffsetX = stored.x;
            Conf.OffsetY = GetOffsetYFromPosition(stored.y);
        }
        ConfMgr?.Save();
        Object.Destroy(canvasObj);
        canvasObj = null;
        raycaster = null;
        root = null;
        valueText = null;
        captionText = null;
        dragObj = null;
        updater = null;
    }
    private static float GetOffsetYFromPosition(float anchoredY) {
        ProgressBarOverlay.EnsureConf();
        ProgressBarSettings bar = ProgressBarOverlay.Conf;
        return -(anchoredY + bar.TopOffset + bar.Height + VerticalGap);
    }
    private static void ApplyValueMaterial() {
        if(valueText == null) return;
        ApplyThickness(valueText, Conf.CountThickness);
        TMPTextShadow.Apply(
            valueText,
            Conf.CountShadowEnabled,
            Conf.CountShadowX,
            Conf.CountShadowY,
            Conf.CountShadowSoftness,
            Conf.GetCountShadowColor()
        );
    }
    private static void ApplyCaptionMaterial() {
        if(captionText == null) return;
        TMPTextShadow.Apply(
            captionText,
            Conf.CaptionShadowEnabled,
            Conf.CaptionShadowX,
            Conf.CaptionShadowY,
            Conf.CaptionShadowSoftness,
            Conf.GetCaptionShadowColor()
        );
    }
    private static void ApplyThickness(TextMeshProUGUI text, float dilate) {
        Material mat = text.fontMaterial;
        if(mat == null) return;
        mat.SetFloat("_FaceDilate", Mathf.Clamp(dilate, -1f, 1f));
    }
    private sealed class Updater : MonoBehaviour {
        private int cachedCount = -1;
        private float lastValueSize = float.NaN;
        private Color lastColor;
        private bool lastCaptionShown;
        private float lastCaptionSize = float.NaN;
        private float lastLabelKick = float.NaN;
        private float lastCaptionOffsetY = float.NaN;
        private float lastBlockH = float.NaN;
        private Vector2 prefPerPoint;
        private Vector2 captionPrefPerPoint;
        private string lastCaptionText;
        private void Update() {
            if(root == null || valueText == null) return;
            bool isReorganizing = UICore.IsReorganizing;
            bool show = (Panels.PanelsOverlay.IsEnabled && Conf.Enabled && GameStats.InGame) || isReorganizing;
            if(raycaster != null && raycaster.enabled != isReorganizing) raycaster.enabled = isReorganizing;
            if(root.gameObject.activeSelf != show) root.gameObject.SetActive(show);
            if(dragObj != null && dragObj.activeSelf != isReorganizing) dragObj.SetActive(isReorganizing);
            if(!show) return;
            if(isReorganizing) {
                Vector2 stored = OverlayCalibration.Unscale(root.anchoredPosition);
                Conf.OffsetX = stored.x;
                Conf.OffsetY = GetOffsetYFromPosition(stored.y);
            }
            TMP_FontAsset font = FontManager.Current;
            bool fontChanged = false;
            if(valueText.font != font) {
                valueText.font = font;
                ApplyValueMaterial();
                fontChanged = true;
            }
            bool captionFontChanged = false;
            if(captionText != null && captionText.font != font) {
                captionText.font = font;
                ApplyCaptionMaterial();
                captionFontChanged = true;
            }
            int count = isReorganizing && Combo.Count <= 0 ? 42 : Combo.Count;
            (float pulse, float pulseIntensity) = Combo.EvaluatePulse(Conf.CountPulseScale, Conf.PulseDuration);
            float valueSize = Conf.FontSize * pulse;
            Color color = Conf.GetComboColor(count);
            bool valueChanged = fontChanged
                || count != cachedCount
                || valueSize != lastValueSize
                || color != lastColor;
            if(valueChanged) {
                bool textChanged = count != cachedCount;
                if(textChanged) {
                    cachedCount = count;
                    UnityUtils.SetCount(valueText, count);
                }
                valueText.fontSize = valueSize;
                valueText.color = color;
                if(textChanged || prefPerPoint == Vector2.zero) {
                    Vector2 pref = valueText.GetPreferredValues(valueText.text);
                    prefPerPoint = valueSize > 0f ? pref / valueSize : Vector2.zero;
                }
                Vector2 scaled = prefPerPoint * valueSize;
                valueText.rectTransform.sizeDelta = new Vector2(Mathf.Max(scaled.x, 200f), scaled.y);
                ApplyValueMaterial();
                lastValueSize = valueSize;
                lastColor = color;
            }
            bool captionShown = captionText != null && Conf.ShowCaption;
            float captionSize = valueSize * Conf.CaptionScale;
            float labelKick = pulseIntensity * Conf.LabelPulseOffsetY;
            if(captionText != null) {
                if(captionShown) {
                    bool captionChanged = captionFontChanged
                        || valueChanged
                        || !lastCaptionShown
                        || captionSize != lastCaptionSize
                        || labelKick != lastLabelKick
                        || Conf.CaptionOffsetY != lastCaptionOffsetY;
                    if(captionChanged) {
                        captionText.fontSize = captionSize;
                        captionText.color = Color.white;
                        if(captionFontChanged
                            || captionText.text != lastCaptionText
                            || captionPrefPerPoint == Vector2.zero
                        ) {
                            Vector2 capMeasured = captionText.GetPreferredValues(captionText.text);
                            captionPrefPerPoint = captionSize > 0f ? capMeasured / captionSize : Vector2.zero;
                            lastCaptionText = captionText.text;
                        }
                        Vector2 capPref = captionPrefPerPoint * captionSize;
                        captionText.rectTransform.sizeDelta = new Vector2(Mathf.Max(capPref.x, 200f), capPref.y);
                        captionText.rectTransform.anchoredPosition = new Vector2(
                            0f,
                            -(valueText.rectTransform.sizeDelta.y + CaptionGap + Conf.CaptionOffsetY) + labelKick
                        );
                        ApplyCaptionMaterial();
                        lastCaptionSize = captionSize;
                        lastLabelKick = labelKick;
                        lastCaptionOffsetY = Conf.CaptionOffsetY;
                    }
                } else if(lastCaptionShown || captionFontChanged) {
                    ApplyCaptionMaterial();
                }
                lastCaptionShown = captionShown;
            }
            float blockH = valueText.rectTransform.sizeDelta.y;
            if(captionShown && captionText != null) {
                blockH += CaptionGap + captionText.rectTransform.sizeDelta.y + Conf.CaptionOffsetY;
            }
            if(blockH != lastBlockH) {
                root.sizeDelta = new Vector2(768f, blockH);
                lastBlockH = blockH;
            }
        }
    }
}

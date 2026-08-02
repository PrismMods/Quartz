using System;
using System.Globalization;
using Quartz.Core;
using Quartz.Resource;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;
namespace Quartz.Features.Countdown;
internal sealed partial class CountdownPanel : IDisposable {
    private const float PanelWidth = 660f;
    private const float PanelHeight = 156f;
    private readonly GameObject root;
    private readonly RectTransform panel;
    private readonly TMP_InputField bpmInput;
    private readonly TMP_Text numeratorLabel;
    private readonly TMP_Text denominatorLabel;
    private readonly Action<MetronomeSettings> settingsChanged;
    private readonly Action disableRequested;
    private readonly double placeholderBpm;
    private readonly GameObject ownedEventSystem;
    private MetronomeSettings settings;
    private int suppressInputThroughFrame = -1;
    internal static CountdownPanel Create(
        MetronomeSettings settings,
        double placeholderBpm,
        Action<MetronomeSettings> settingsChanged,
        Action disableRequested
    ) => new(settings, placeholderBpm, settingsChanged, disableRequested);
    private CountdownPanel(
        MetronomeSettings settings,
        double placeholderBpm,
        Action<MetronomeSettings> settingsChanged,
        Action disableRequested
    ) {
        this.settings = settings;
        this.placeholderBpm = placeholderBpm;
        this.settingsChanged = settingsChanged;
        this.disableRequested = disableRequested;
        ownedEventSystem = EnsureEventSystem();
        root = new GameObject("Quartz Countdown Metronome Panel", typeof(RectTransform));
        root.SetActive(false);
        Object.DontDestroyOnLoad(root);
        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30000;
        CanvasScaler scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 1f;
        root.AddComponent<GraphicRaycaster>();
        panel = MakeRect(root.transform, "Panel", new Vector2(PanelWidth, PanelHeight));
        panel.anchorMin = new Vector2(0.5f, 0f);
        panel.anchorMax = new Vector2(0.5f, 0f);
        panel.pivot = new Vector2(0.5f, 0f);
        panel.anchoredPosition = new Vector2(0f, 48f);
        Image background = panel.gameObject.AddComponent<Image>();
        background.color = new Color(0.06f, 0.07f, 0.09f, 0.92f);
        BuildTitle();
        bpmInput = BuildBpmRow();
        BuildMeterRow(out numeratorLabel, out denominatorLabel);
        Refresh();
        root.SetActive(true);
    }
    internal bool IsConsumingInput {
        get {
            if(root == null || !root.activeInHierarchy) return false;
            if(Time.frameCount <= suppressInputThroughFrame || (bpmInput != null && bpmInput.isFocused)) return true;
            if(PointerIsWorkingThePanel()) return true;
            ClearTransientSelection();
            return false;
        }
    }
    internal void SetSettings(MetronomeSettings value) {
        settings = value;
        Refresh();
    }
    public void Dispose() {
        if(bpmInput != null) bpmInput.onEndEdit.RemoveAllListeners();
        if(root != null) {
            root.SetActive(false);
            Object.Destroy(root);
        }
        if(ownedEventSystem != null) Object.Destroy(ownedEventSystem);
    }
    private void Apply(MetronomeSettings updated) {
        settings = updated;
        Refresh();
        SuppressInput();
        settingsChanged?.Invoke(settings);
    }
    private void ApplyMultiplier(decimal multiplier) {
        CommitBpm(bpmInput.text, notify: false);
        decimal value = (decimal)settings.ClickBpm * multiplier;
        Apply(settings.WithClickBpm((double)value));
        ClearTransientSelection(force: true);
    }
    private void StepNumerator(int delta) {
        Apply(settings.WithNumerator(settings.Numerator + delta));
        ClearTransientSelection(force: true);
    }
    private void StepDenominator(int delta) {
        Apply(settings.WithDenominator(settings.Denominator + delta));
        ClearTransientSelection(force: true);
    }
    private void CommitBpm(string value) {
        CommitBpm(value, notify: true);
        if(!Input.GetMouseButton(0)) ClearTransientSelection(force: true);
    }
    private void CommitBpm(string value, bool notify) {
        if(!decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal parsed)
            && !decimal.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out parsed)) {
            Refresh();
            SuppressInput();
            return;
        }
        MetronomeSettings updated = settings.WithClickBpm((double)parsed);
        if(notify) {
            Apply(updated);
            return;
        }
        settings = updated;
        Refresh();
    }
    private void RequestDisable() {
        SuppressInput();
        ClearTransientSelection(force: true);
        disableRequested?.Invoke();
    }
    private void Refresh() {
        if(bpmInput != null) {
            bpmInput.SetTextWithoutNotify(settings.ClickBpm.ToString("0.0", CultureInfo.InvariantCulture));
            if(bpmInput.placeholder is TMP_Text placeholder)
                placeholder.text = placeholderBpm.ToString("0.0", CultureInfo.InvariantCulture);
        }
        if(numeratorLabel != null)
            numeratorLabel.text = settings.Numerator.ToString(CultureInfo.InvariantCulture);
        if(denominatorLabel != null)
            denominatorLabel.text = settings.Denominator.ToString(CultureInfo.InvariantCulture);
    }
    private bool PointerIsWorkingThePanel() {
        try {
            bool pointerPressed =
                Input.GetMouseButton(0) || Input.GetMouseButtonDown(0) || Input.GetMouseButtonUp(0);
            return pointerPressed
                && RectTransformUtility.RectangleContainsScreenPoint(panel, Input.mousePosition, null);
        } catch(Exception e) {
            Diag.Ignore(e);
            return false;
        }
    }
    private static GameObject EnsureEventSystem() {
        if(EventSystem.current != null) return null;
        GameObject holder = new("Quartz Countdown EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        Object.DontDestroyOnLoad(holder);
        return holder;
    }
    private void SuppressInput() => suppressInputThroughFrame = Time.frameCount + 1;
    private void ClearTransientSelection(bool force = false) {
        EventSystem eventSystem = EventSystem.current;
        if(eventSystem == null || root == null) return;
        GameObject selected = eventSystem.currentSelectedGameObject;
        if(selected == null || !selected.transform.IsChildOf(root.transform)) return;
        if(!force && bpmInput != null && bpmInput.isFocused) return;
        eventSystem.SetSelectedGameObject(null);
    }
    private static string Tr(string key, string fallback) {
        try {
            return MainCore.Tr?.Get(key, fallback) ?? fallback;
        } catch(Exception e) {
            Diag.Ignore(e);
            return fallback;
        }
    }
    private static TMP_FontAsset Font() {
        try {
            return FontManager.Current;
        } catch(Exception e) {
            Diag.Ignore(e);
            return null;
        }
    }
}

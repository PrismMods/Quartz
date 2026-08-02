using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace Quartz.Features.Countdown;
internal sealed partial class CountdownPanel {
    private static readonly Color LabelColor = new(1f, 1f, 1f, 0.62f);
    private static readonly Color ValueColor = new(1f, 1f, 1f, 0.92f);
    private static readonly Color ButtonColor = new(0.16f, 0.18f, 0.22f, 1f);
    private static readonly Color FieldColor = new(0.11f, 0.12f, 0.15f, 1f);
    private static readonly Color OffColor = new(0.42f, 0.16f, 0.18f, 1f);
    private void BuildTitle() {
        TMP_Text title = MakeText(panel, "Title", new Rect(16f, -8f, 300f, 24f), 20f, TextAlignmentOptions.Left);
        title.color = LabelColor;
        title.text = Tr("COUNTDOWN_PANEL_TITLE", "Metronome");
    }
    private TMP_InputField BuildBpmRow() {
        const float y = -42f;
        TMP_Text label = MakeText(panel, "BpmLabel", new Rect(16f, y, 60f, 40f), 22f, TextAlignmentOptions.Left);
        label.color = LabelColor;
        label.text = Tr("COUNTDOWN_PANEL_BPM", "BPM");
        TMP_InputField input = MakeInput(new Rect(84f, y, 140f, 40f));
        input.onEndEdit.AddListener(CommitBpm);
        MakeButton(new Rect(240f, y, 74f, 40f), "÷2", () => ApplyMultiplier(0.5m), ButtonColor);
        MakeButton(new Rect(322f, y, 74f, 40f), "×2", () => ApplyMultiplier(2m), ButtonColor);
        MakeButton(new Rect(404f, y, 74f, 40f), "÷3", () => ApplyMultiplier(1m / 3m), ButtonColor);
        MakeButton(new Rect(486f, y, 74f, 40f), "×3", () => ApplyMultiplier(3m), ButtonColor);
        return input;
    }
    private void BuildMeterRow(out TMP_Text numerator, out TMP_Text denominator) {
        const float y = -98f;
        TMP_Text label = MakeText(panel, "MeterLabel", new Rect(16f, y, 64f, 40f), 22f, TextAlignmentOptions.Left);
        label.color = LabelColor;
        label.text = Tr("COUNTDOWN_PANEL_METER", "Meter");
        MakeButton(new Rect(88f, y, 36f, 40f), "−", () => StepNumerator(-1), ButtonColor);
        numerator = MakeText(panel, "Numerator", new Rect(128f, y, 44f, 40f), 24f, TextAlignmentOptions.Center);
        numerator.color = ValueColor;
        MakeButton(new Rect(176f, y, 36f, 40f), "+", () => StepNumerator(1), ButtonColor);
        TMP_Text slash = MakeText(panel, "Slash", new Rect(216f, y, 20f, 40f), 24f, TextAlignmentOptions.Center);
        slash.color = LabelColor;
        slash.text = "/";
        MakeButton(new Rect(240f, y, 36f, 40f), "−", () => StepDenominator(-1), ButtonColor);
        denominator = MakeText(panel, "Denominator", new Rect(280f, y, 44f, 40f), 24f, TextAlignmentOptions.Center);
        denominator.color = ValueColor;
        MakeButton(new Rect(328f, y, 36f, 40f), "+", () => StepDenominator(1), ButtonColor);
        MakeButton(
            new Rect(406f, y, 154f, 40f),
            Tr("COUNTDOWN_PANEL_OFF", "Use game countdown"),
            RequestDisable,
            OffColor
        );
    }
    private static RectTransform MakeRect(Transform parent, string name, Vector2 size) {
        GameObject obj = new(name, typeof(RectTransform));
        RectTransform rect = (RectTransform)obj.transform;
        rect.SetParent(parent, worldPositionStays: false);
        rect.sizeDelta = size;
        return rect;
    }
    private RectTransform MakeChild(string name, Rect layout) {
        RectTransform rect = MakeRect(panel, name, new Vector2(layout.width, layout.height));
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(layout.x, layout.y);
        return rect;
    }
    private TMP_Text MakeText(Transform parent, string name, Rect layout, float size, TextAlignmentOptions alignment) {
        RectTransform rect = parent == panel
            ? MakeChild(name, layout)
            : MakeRect(parent, name, new Vector2(layout.width, layout.height));
        TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        TMP_FontAsset font = Font();
        if(font != null) text.font = font;
        text.fontSize = size;
        text.alignment = alignment;
        text.color = ValueColor;
        text.raycastTarget = false;
        Quartz.Compat.Game.TextCompat.NoWrap(text);
        text.overflowMode = TextOverflowModes.Overflow;
        return text;
    }
    private void MakeButton(Rect layout, string caption, Action onClick, Color color) {
        RectTransform rect = MakeChild(caption + "Button", layout);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.25f, 1.25f, 1.25f, 1f);
        colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        colors.selectedColor = Color.white;
        button.colors = colors;
        button.onClick.AddListener(() => onClick?.Invoke());
        TMP_Text label = MakeText(rect, "Label", new Rect(0f, 0f, layout.width, layout.height), 21f,
            TextAlignmentOptions.Center);
        label.rectTransform.anchorMin = Vector2.zero;
        label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = new Vector2(4f, 0f);
        label.rectTransform.offsetMax = new Vector2(-4f, 0f);
        label.text = caption;
    }
    private TMP_InputField MakeInput(Rect layout) {
        RectTransform rect = MakeChild("BpmInput", layout);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = FieldColor;
        RectTransform viewport = MakeRect(rect, "TextArea", Vector2.zero);
        viewport.anchorMin = Vector2.zero;
        viewport.anchorMax = Vector2.one;
        viewport.offsetMin = new Vector2(10f, 4f);
        viewport.offsetMax = new Vector2(-10f, -4f);
        viewport.gameObject.AddComponent<RectMask2D>();
        TMP_Text text = MakeText(viewport, "Text", new Rect(0f, 0f, layout.width, layout.height), 22f,
            TextAlignmentOptions.Left);
        Stretch(text.rectTransform);
        TMP_Text placeholder = MakeText(viewport, "Placeholder", new Rect(0f, 0f, layout.width, layout.height), 22f,
            TextAlignmentOptions.Left);
        Stretch(placeholder.rectTransform);
        placeholder.color = new Color(1f, 1f, 1f, 0.25f);
        TMP_InputField input = rect.gameObject.AddComponent<TMP_InputField>();
        input.textViewport = viewport;
        input.textComponent = text;
        input.placeholder = placeholder;
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.contentType = TMP_InputField.ContentType.DecimalNumber;
        input.richText = false;
        input.targetGraphic = image;
        return input;
    }
    private static void Stretch(RectTransform rect) {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}

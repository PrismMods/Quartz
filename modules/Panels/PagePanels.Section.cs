using Quartz.Overlay;
using Quartz.Core;
using Quartz.Features.Panels;
using Quartz.Resource;
using Quartz.Tween;
using Quartz.UI;
using Quartz.UI.Generator;
using Quartz.UI.Objects.Impl;
using Quartz.UI.Utility;
using GTweens.Builders;
using GTweens.Easings;
using GTweens.Extensions;
using GTweens.Tweens;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using GTweenExtensions = GTweens.Extensions.GTweenExtensions;
using static UnityEngine.EventSystems.PointerEventData;
using TMPro;
namespace Quartz.UI.Factory.Page;
internal static partial class PagePanels {
    private static void CreatePanelSection(Transform parent, PanelConfig panel, int index) {
        PanelConfig def = new();
        string idp = "panel" + index;
        var sec = GenerateUI.Collapsible(parent, panel.Name, startExpanded: false);
        TMP_Text header = sec.Section.Find("Header/Bar/Label")?.GetComponent<TMP_Text>();
        sec.Section.gameObject.AddComponent<PanelSectionMarker>().Config = panel;
        AddPanelLayerHandle(sec, panel);
        void Save() => PanelsOverlay.Save();
        UIInput name = GenerateUI.Input(
            GenerateUI.Row(sec.Body),
            def.Name,
            panel.Name,
            v => {
                panel.Name = string.IsNullOrWhiteSpace(v) ? "Panel" : v;
                if(header != null) header.text = panel.Name;
                Save();
            },
            "Panel Name",
            MainCore.Spr.Get(UISprite.Text128),
            idp + "_name"
        );
        name.InputField.characterLimit = 24;
        name.Rect.AddToolTip("DESC_PANEL_NAME", "Shown on the panel while reorganizing, and as this section's title.");
        GenerateUI.Localize(GenerateUI.AddTextH1(GenerateUI.Row(sec.Body)), "HEADING_STATS", "Stats");
        UIButton addBtn = null;
        GameObject picker = null;
        GameObject rows = null;
        RectTransform pickerRect = null;
        VerticalLayoutGroup pickerLayout = null;
        ContentSizeFitter pickerFitter = null;
        LayoutElement pickerLE = null;
        CanvasGroup pickerCg = null;
        GTween pickerSeq = null;
        StatEntry replaceTarget = null;
        bool pickerOpen = false;
        void AnimatePicker(bool open, Action onClosed = null) {
            pickerSeq?.Kill();
            if(picker == null || pickerLayout == null || pickerFitter == null
                || pickerLE == null || pickerRect == null || pickerCg == null) return;
            pickerSeq = AnimateBody(
                sec.Section, pickerRect, pickerLayout, pickerFitter, pickerLE, pickerCg,
                open, onClosed);
        }
        void CommitOrder() {
            List<StatEntry> order = [];
            for(int i = 0; i < rows.transform.childCount; i++) {
                StatRowMarker marker = rows.transform.GetChild(i).GetComponent<StatRowMarker>();
                if(marker != null) order.Add(marker.Entry);
            }
            panel.Stats.Clear();
            panel.Stats.AddRange(order);
            Save();
        }
        void ClosePicker(bool animate = true) {
            pickerOpen = false;
            replaceTarget = null;
            if(addBtn != null) addBtn.Label.text = GenerateUI.Tr("PANEL_ADDSTAT", "+ Add Stat");
            if(animate) {
                AnimatePicker(false);
            } else {
                pickerSeq?.Kill();
                if(picker != null && pickerLE != null) {
                    GenerateUI.ClearChildren(picker.transform);
                    pickerLE.preferredHeight = 0f;
                }
            }
        }
        void OpenPickerAnimated() {
            pickerOpen = true;
            if(addBtn != null) addBtn.Label.text = GenerateUI.Tr("CLOSE", "Close");
            BuildPicker();
            AnimatePicker(true);
        }
        HashSet<StatEntry> colorExpanded = [];
        Dictionary<StatEntry, StatColorBody> colorBodies = [];
        void AnimateColorBody(StatColorBody body, bool open) {
            body.Seq?.Kill();
            body.Seq = AnimateBody(
                sec.Section, body.Rect, body.Layout, body.Fitter, body.LE, body.CG, open);
        }
        void RebuildColorBody(StatEntry entry) {
            if(!colorBodies.TryGetValue(entry, out StatColorBody body)) return;
            GenerateUI.ClearChildren(body.Rect);
            BuildStatColorSettings(body.Rect, entry, Save, () => RebuildColorBody(entry), idp);
            body.Layout.enabled = true;
            body.Fitter.enabled = true;
            body.LE.preferredHeight = -1f;
            body.CG.alpha = 1f;
            LayoutRebuilder.ForceRebuildLayoutImmediate(sec.Section);
        }
        void ToggleColorBody(StatEntry entry) {
            if(!colorBodies.TryGetValue(entry, out StatColorBody body)) return;
            if(colorExpanded.Remove(entry)) {
                AnimateColorBody(body, false);
                return;
            }
            colorExpanded.Add(entry);
            GenerateUI.ClearChildren(body.Rect);
            BuildStatColorSettings(body.Rect, entry, Save, () => RebuildColorBody(entry), idp);
            AnimateColorBody(body, true);
        }
        void RebuildRows() {
            if(rows == null) return;
            GenerateUI.ClearChildren(rows.transform);
            colorBodies.Clear();
            if(panel.Stats.Count == 0) {
                GenerateUI.AddLocalizedMutedText(
                    GenerateUI.Row(rows.transform), "PANEL_NO_STATS", "No stats on this panel.", 19f);
                return;
            }
            foreach(StatEntry entry in panel.Stats) {
                BuildStatRow(rows.transform, entry, () => {
                    CommitOrder();
                    if(colorExpanded.Count > 0) RebuildRows();
                }, () => {
                    panel.Stats.Remove(entry);
                    colorExpanded.Remove(entry);
                    Save();
                    RebuildRows();
                }, () => {
                    replaceTarget = entry;
                    OpenPickerAnimated();
                }, () => ToggleColorBody(entry), Save, idp);
                StatColorBody body = CreateColorBody(rows.transform);
                colorBodies[entry] = body;
                if(colorExpanded.Contains(entry)) {
                    BuildStatColorSettings(body.Rect, entry, Save, () => RebuildColorBody(entry), idp);
                    body.LE.preferredHeight = -1f;
                    body.CG.alpha = 1f;
                }
            }
        }
        void BuildPicker() {
            if(picker == null) return;
            GenerateUI.ClearChildren(picker.transform);
            BuildStatPickerCategories(picker.transform, panel, idp, statId => {
                if(replaceTarget != null) {
                    replaceTarget.Id = statId;
                } else {
                    StatEntry added = new(statId);
                    if(statId == "text") added.ShowLabel = false;
                    panel.Stats.Add(added);
                }
                Save();
                ClosePicker();
                RebuildRows();
            });
        }
        addBtn = GenerateUI.Button(
            GenerateUI.Row(sec.Body),
            () => {
                if(pickerOpen) {
                    ClosePicker();
                    return;
                }
                replaceTarget = null;
                OpenPickerAnimated();
            },
            "+ Add Stat",
            idp + "_addstat"
        );
        addBtn.Rect.AddToolTip("DESC_PANEL_ADDSTAT", "Pick a stat to add to this panel.");
        GenerateUI.Button(
            GenerateUI.Row(sec.Body),
            () => {
                panel.Stats.Clear();
                Save();
                ClosePicker();
                RebuildRows();
            },
            "Clear All Stats",
            idp + "_clearstats"
        ).SetSecondary();
        picker = MakeListContainer("StatPicker", sec.Body, 6f);
        pickerRect = picker.GetComponent<RectTransform>();
        pickerLayout = picker.GetComponent<VerticalLayoutGroup>();
        pickerFitter = picker.GetComponent<ContentSizeFitter>();
        pickerLE = picker.AddComponent<LayoutElement>();
        pickerCg = picker.AddComponent<CanvasGroup>();
        picker.AddComponent<RectMask2D>();
        rows = MakeListContainer("StatRows", sec.Body, 6f);
        RebuildRows();
        GenerateUI.Localize(GenerateUI.AddTextH1(GenerateUI.Row(sec.Body)), "HEADING_APPEARANCE", "Appearance");
        PanelAnchor[] anchors = (PanelAnchor[])Enum.GetValues(typeof(PanelAnchor));
        GenerateUI.DropDown(
            GenerateUI.Row(sec.Body),
            PanelAnchor.TopLeft,
            (PanelAnchor)panel.Anchor,
            anchors,
            AnchorName,
            v => PanelsOverlay.SetAnchor(panel, v),
            idp + "_anchor",
            260f,
            "Anchor"
        );
        UIInput prefix = GenerateUI.Input(
            GenerateUI.Row(sec.Body),
            def.Prefix,
            panel.Prefix,
            v => { panel.Prefix = v; Save(); },
            "Prefix",
            MainCore.Spr.Get(UISprite.Text128),
            idp + "_prefix"
        );
        prefix.InputField.characterLimit = 32;
        prefix.Rect.AddToolTip("DESC_PANEL_PREFIX", "Extra line shown at the top of the panel.");
        UIInput sep = GenerateUI.Input(
            GenerateUI.Row(sec.Body),
            def.LabelSeparator,
            panel.LabelSeparator,
            v => { panel.LabelSeparator = v; Save(); },
            "Label Separator",
            MainCore.Spr.Get(UISprite.Text128),
            idp + "_separator"
        );
        sep.InputField.characterLimit = 8;
        static float fontFilter(float v) => Mathf.Clamp(Mathf.Round(v), 12f, 48f);
        UISlider font = GenerateUI.Slider(
            GenerateUI.Row(sec.Body),
            def.FontSize, 12f, 48f, panel.FontSize,
            fontFilter, null, null,
            "Font Size", idp + "_fontsize"
        );
        font.Format = "0 px";
        font.OnChanged = v => { panel.FontSize = v; PanelsOverlay.Apply(); };
        font.OnComplete = v => { panel.FontSize = v; PanelsOverlay.Apply(); Save(); };
        static float lineFilter(float v) => Mathf.Clamp(Mathf.Round(v * 2f) * 0.5f, -50f, 50f);
        UISlider line = GenerateUI.Slider(
            GenerateUI.Row(sec.Body),
            def.LineSpacing, -50f, 50f, panel.LineSpacing,
            lineFilter, null, null,
            "Line Spacing", idp + "_linespacing"
        );
        line.Format = "0.#";
        line.OnChanged = v => { panel.LineSpacing = v; PanelsOverlay.Apply(); };
        line.OnComplete = v => { panel.LineSpacing = v; PanelsOverlay.Apply(); Save(); };
        UISlider decimals = GenerateUI.Slider(
            GenerateUI.Row(sec.Body),
            def.Decimals, 0f, 6f, panel.Decimals,
            v => Mathf.Round(v), null, null,
            "Percent Decimals", idp + "_decimals"
        );
        decimals.Format = "0";
        decimals.OnChanged = v => panel.Decimals = (int)v;
        decimals.OnComplete = v => { panel.Decimals = (int)v; Save(); };
        GenerateUI.ColorPicker(
            GenerateUI.Row(sec.Body),
            def.GetTextColor(),
            panel.GetTextColor(),
            c => { panel.SetTextColor(c); PanelsOverlay.Apply(); },
            c => { panel.SetTextColor(c); PanelsOverlay.Apply(); Save(); },
            "Text Color",
            idp + "_textcolor"
        );
        GenerateUI.Toggle(
            GenerateUI.Row(sec.Body),
            def.BackgroundEnabled,
            panel.BackgroundEnabled,
            v => { panel.BackgroundEnabled = v; PanelsOverlay.Apply(); Save(); },
            "Background Panel",
            idp + "_background"
        );
        GenerateUI.ColorPicker(
            GenerateUI.Row(sec.Body),
            def.GetBackgroundColor(),
            panel.GetBackgroundColor(),
            c => { panel.SetBackgroundColor(c); PanelsOverlay.Apply(); },
            c => { panel.SetBackgroundColor(c); PanelsOverlay.Apply(); Save(); },
            "Background Color",
            idp + "_bgcolor"
        ).Rect.AddToolTip(
            "DESC_PANEL_BGCOLOR",
            "Fill color of the panel background. The A (alpha) slider sets its opacity. Needs Background Panel on."
        );
        GenerateUI.Toggle(
            GenerateUI.Row(sec.Body),
            def.LocalizeStatLabels,
            panel.LocalizeStatLabels,
            v => { panel.LocalizeStatLabels = v; PanelsOverlay.Apply(); Save(); },
            "Localize Stat Labels",
            idp + "_localizestats"
        ).Rect.AddToolTip(
            "DESC_PANEL_LOCALIZESTATS",
            "Off: this panel's stat labels stay English (X-Acc, Max X-Acc…). On: they follow the UI language."
        );
        GenerateUI.Localize(GenerateUI.AddTextH1(GenerateUI.Row(sec.Body)), "HEADING_SHADOW", "Shadow");
        GenerateUI.Toggle(
            GenerateUI.Row(sec.Body),
            def.TextShadowEnabled,
            panel.TextShadowEnabled,
            v => { panel.TextShadowEnabled = v; PanelsOverlay.Apply(); Save(); },
            "Text Shadow",
            idp + "_textshadow"
        );
        GenerateUI.SnapSlider(sec.Body, "Shadow X", idp + "_shadow_x",
            def.TextShadowX, -20f, 20f, panel.TextShadowX, "0.0 px", 0.1f,
            v => panel.TextShadowX = v, PanelsOverlay.Apply, Save);
        GenerateUI.SnapSlider(sec.Body, "Shadow Y", idp + "_shadow_y",
            def.TextShadowY, -20f, 20f, panel.TextShadowY, "0.0 px", 0.1f,
            v => panel.TextShadowY = v, PanelsOverlay.Apply, Save);
        GenerateUI.SnapSlider(sec.Body, "Shadow Softness", idp + "_shadow_softness",
            def.TextShadowSoftness, 0f, 20f, panel.TextShadowSoftness, "0.0 px", 0.1f,
            v => panel.TextShadowSoftness = v, PanelsOverlay.Apply, Save);
        GenerateUI.ColorPicker(
            GenerateUI.Row(sec.Body),
            def.GetTextShadowColor(),
            panel.GetTextShadowColor(),
            c => { panel.SetTextShadowColor(c); PanelsOverlay.Apply(); },
            c => { panel.SetTextShadowColor(c); PanelsOverlay.Apply(); Save(); },
            "Shadow Color",
            idp + "_shadow_color"
        );
        GenerateUI.Localize(GenerateUI.AddTextH1(GenerateUI.Row(sec.Body)), "HEADING_ACTIONS", "Actions");
        GenerateUI.Button(
            GenerateUI.Row(sec.Body),
            () => PanelsOverlay.ResetPosition(panel),
            "Reset Position",
            idp + "_resetpos"
        ).SetSecondary();
        GenerateUI.Button(
            GenerateUI.Row(sec.Body),
            () => {
                PanelsOverlay.Conf.Panels.Remove(panel);
                PanelsOverlay.Save();
                PanelsOverlay.Rebuild();
                RebuildPanelsList();
            },
            "Delete Panel",
            idp + "_delete"
        ).SetSecondary();
    }
}

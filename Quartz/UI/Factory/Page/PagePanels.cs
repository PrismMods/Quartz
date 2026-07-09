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

// Panels page — one of the children of the collapsible "Overlay" sidebar
// group. User-created, named panels that any catalog stat can be placed on;
// extracted from the old monolithic PageOverlay.cs, which also used to host
// the other overlay features (now their own separate pages) and the master
// Enable Overlays toggle / Reorganize button (now on PageOverlayGeneral).
internal static partial class PagePanels {
    private static GameObject panelsList;

    public static void Create(RectTransform parent) {
        PanelsOverlay.EnsureConf();
        RectTransform content = Quartz.UI.Factory.PageFactory.CreateScrollablePage(parent);

        var headerRow = GenerateUI.Row(content.transform);
        var headerText = GenerateUI.AddTextH1(headerRow);
        GenerateUI.Localize(headerText, "SECTION_PANELS", "Panels");

        GenerateUI.Button(
            GenerateUI.Row(content.transform),
            () => {
                PanelConfig p = new() {
                    Name = "Panel " + (PanelsOverlay.Conf.Panels.Count + 1),
                };
                // Stagger new panels so they don't stack exactly on top of
                // each other.
                p.PosX += 24f * PanelsOverlay.Conf.Panels.Count;
                p.PosY -= 24f * PanelsOverlay.Conf.Panels.Count;
                PanelsOverlay.Conf.Panels.Add(p);
                PanelsOverlay.Save();
                PanelsOverlay.Rebuild();
                RebuildPanelsList();
            },
            "Create Panel",
            "panels_create"
        ).Rect.AddToolTip(
            "DESC_PANELS_CREATE",
            "Adds a new empty panel. Name it, put stats on it, then drag it into place with Reorganize."
        );

        panelsList = new GameObject("PanelsList");
        panelsList.transform.SetParent(content.transform, false);
        panelsList.AddComponent<RectTransform>();
        GenerateUI.FitVertical(panelsList);

        RebuildPanelsList();
    }

    // Soft cap: past this many panels, a warning is shown, but creation is
    // never blocked (mirrors PageProgressBar's gradient-stop cap style).
    private const int PANEL_SOFT_CAP = 10;

    // Rebuilds the per-panel sections (create/delete change the set).
    private static void RebuildPanelsList() {
        if(panelsList == null) return;

        GenerateUI.ClearChildren(panelsList.transform);

        List<PanelConfig> panels = PanelsOverlay.Conf.Panels;

        if(panels.Count == 0) {
            GenerateUI.AddLocalizedMutedText(
                GenerateUI.Row(panelsList.transform), "PANEL_NO_PANELS", "No panels. Create one above.", 19f);
            return;
        }

        if(panels.Count > PANEL_SOFT_CAP) {
            GenerateUI.AddMutedText(GenerateUI.Row(panelsList.transform), 19f).text = string.Format(
                GenerateUI.Tr("PANEL_TOO_MANY", "{0} panels — that's a lot; performance may suffer."),
                panels.Count
            );
        }

        for(int i = 0; i < panels.Count; i++)
            CreatePanelSection(panelsList.transform, panels[i], i);
    }

    private static void CreatePanelSection(Transform parent, PanelConfig panel, int index) {
        PanelConfig def = new();
        string idp = "panel" + index;

        var sec = GenerateUI.Collapsible(parent, panel.Name, startExpanded: false);
        TMP_Text header = sec.Section.Find("Header/Bar/Label")?.GetComponent<TMP_Text>();

        // Drag the header's 6-dot handle to reorder this panel's layer. List
        // order = draw order: the top section renders on top where panels
        // overlap. Mirrors the stat rows' reorder handle below.
        sec.Section.gameObject.AddComponent<PanelSectionMarker>().Config = panel;
        AddPanelLayerHandle(sec, panel);

        void Save() => PanelsOverlay.Save();

        // === Panel settings ===

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

        // === Stats on this panel: an ordered, editable list ===
        //
        // "+ Add Stat" opens a picker; each entry row has a 6-dot drag handle
        // (reorder = display order on the panel), an enable dot, a Swap
        // button (replace with another stat in place) and a delete X.

        GenerateUI.Localize(GenerateUI.AddTextH1(GenerateUI.Row(sec.Body)), "HEADING_STATS", "Stats");

        UIButton addBtn = null;
        GameObject picker = null;
        GameObject rows = null;

        // Picker slide animation plumbing (assigned after the container is
        // created below; the local functions only run after that).
        RectTransform pickerRect = null;
        VerticalLayoutGroup pickerLayout = null;
        ContentSizeFitter pickerFitter = null;
        LayoutElement pickerLE = null;
        CanvasGroup pickerCg = null;
        GTween pickerSeq = null;

        // null = picker adds to the end; set = picker replaces this entry.
        StatEntry replaceTarget = null;
        bool pickerOpen = false;

        // Slides the picker open/closed like a dropdown (shared slide engine
        // with the per-stat color bodies below).
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

        // Per-stat color settings live in a collapsible body under each row,
        // slid open/closed by the row's Color button (same animation idiom as
        // the stat picker above). Content is built on open and torn down
        // after the close animation.
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
                    // Reordering moves only the marker rows; rebuild so any
                    // expanded color settings follow their row.
                    if(colorExpanded.Count > 0) RebuildRows();
                }, () => {
                    panel.Stats.Remove(entry);
                    colorExpanded.Remove(entry);
                    Save();
                    RebuildRows();
                }, () => {
                    // Swap: open the picker targeting this entry.
                    replaceTarget = entry;
                    OpenPickerAnimated();
                }, () => ToggleColorBody(entry), Save, idp);

                StatColorBody body = CreateColorBody(rows.transform);
                colorBodies[entry] = body;

                // Entries already expanded (rebuild after add/delete/reorder)
                // come back open without re-animating.
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
                    // A custom-text line has no meaningful "Text" label
                    // prefix, so default it to value-only.
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

        // === Appearance ===

        GenerateUI.Localize(GenerateUI.AddTextH1(GenerateUI.Row(sec.Body)), "HEADING_APPEARANCE", "Appearance");

        PanelAnchor[] anchors = (PanelAnchor[])Enum.GetValues(typeof(PanelAnchor));
        GenerateUI.DropDown(
            GenerateUI.Row(sec.Body),
            PanelAnchor.TopLeft,
            (PanelAnchor)panel.Anchor,
            anchors,
            AnchorName,
            // Snaps the offset to the new corner's default inset and rebuilds
            // without re-syncing this panel's stale live position.
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

        // Background fill color; the picker's A channel is the panel's opacity.
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

        // === Shadow ===

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

        // === Panel actions ===

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

    // Builds the picker's category-grouped list of catalog stats not already
    // on the panel (the "text" stat is exempt — each carries its own custom
    // string, so any number can sit on one panel). `onPick` is invoked with
    // the chosen stat's id; the caller decides whether that's an add or a
    // swap-in-place.
    private static void BuildStatPickerCategories(
        Transform picker, PanelConfig panel, string idp, Action<string> onPick
    ) {
        bool any = false;
        // Built-in categories in their fixed order, then any category an
        // addon stat introduced, in registration order.
        List<string> categories = ["Accuracy", "Time", "BPM", "Map Stats", "Other"];
        IReadOnlyList<PanelsOverlay.StatDef> allStats = PanelsOverlay.AllStats;
        foreach(PanelsOverlay.StatDef stat in allStats)
            if(!categories.Contains(stat.Category)) categories.Add(stat.Category);
        foreach(string category in categories) {
            bool headerAdded = false;

            foreach(PanelsOverlay.StatDef stat in allStats) {
                if(stat.Category != category) continue;
                if(stat.Id != "text" && panel.Stats.Exists(e => e.Id == stat.Id)) continue;

                if(!headerAdded) {
                    headerAdded = true;
                    GenerateUI.AddLocalizedMutedText(
                        GenerateUI.Row(picker, 32f),
                        GenerateUI.LocaleKeyFromText("PANEL_CATEGORY", category),
                        category
                    );
                }

                any = true;
                string statId = stat.Id;
                GenerateUI.Button(
                    GenerateUI.Row(picker),
                    () => onPick(statId),
                    stat.Label,
                    idp + "_pick_" + statId
                ).SetSecondary();
            }
        }

        if(!any) {
            GenerateUI.AddLocalizedMutedText(
                GenerateUI.Row(picker), "PANEL_ALL_STATS_ADDED",
                "All stats are already on this panel.", 19f);
        }
    }

    // ===== stat-list row plumbing =====

    private static GameObject MakeListContainer(string name, Transform parent, float spacing) {
        GameObject obj = new(name);
        obj.transform.SetParent(parent, false);
        obj.AddComponent<RectTransform>();

        GenerateUI.FitVertical(obj, spacing);

        return obj;
    }

    // Slides a collapsible sub-body open/closed like a dropdown: lay the rows
    // out once, freeze the layout group, then drive a LayoutElement height +
    // fade (same idiom as the collapsibles). On open, sizing is handed back
    // to the layout group so the rows stay laid out; on close, the content is
    // torn down. Returns the sequence so the caller can Kill a rerun.
    private static GTween AnimateBody(
        RectTransform section, RectTransform rect,
        VerticalLayoutGroup layout, ContentSizeFitter fitter,
        LayoutElement le, CanvasGroup cg,
        bool open, Action onClosed = null
    ) {
        layout.enabled = true;
        fitter.enabled = true;
        le.preferredHeight = -1f;
        LayoutRebuilder.ForceRebuildLayoutImmediate(section);
        float content = rect.rect.height;

        layout.enabled = false;
        fitter.enabled = false;

        le.preferredHeight = open ? 0f : content;
        cg.alpha = open ? 0f : 1f;

        GTween seq = GTweenSequenceBuilder.New()
            .Join(GTweenExtensions.Tween(
                () => le.preferredHeight,
                x => {
                    le.preferredHeight = Mathf.Max(0f, x);
                    LayoutRebuilder.ForceRebuildLayoutImmediate(section);
                },
                open ? content : 0f,
                0.16f
            ).SetEasing(open ? Easing.OutBack : Easing.OutSine))
            .Join(GTweenExtensions.Tween(
                () => cg.alpha,
                x => cg.alpha = x,
                open ? 1f : 0f,
                0.16f
            ).SetEasing(Easing.OutSine))
            .AppendCallback(() => {
                if(open) {
                    // Hand sizing back so the rows stay laid out.
                    layout.enabled = true;
                    fitter.enabled = true;
                    le.preferredHeight = -1f;
                    LayoutRebuilder.ForceRebuildLayoutImmediate(section);
                } else {
                    GenerateUI.ClearChildren(rect);
                    le.preferredHeight = 0f;
                    onClosed?.Invoke();
                }
            })
            .Build();
        MainCore.TC.Play(seq);
        return seq;
    }

    // Builds the 6-dot drag-handle visual pinned to the left edge of `parent`
    // (rect stretched to full height + 2x3 dot grid); the caller attaches the
    // matching drag behaviour.
    private static GameObject MakeDragHandle(Transform parent, string name, float width) {
        GameObject handle = new(name);
        handle.transform.SetParent(parent, false);

        RectTransform handleRect = handle.AddComponent<RectTransform>();
        handleRect.anchorMin = new Vector2(0f, 0f);
        handleRect.anchorMax = new Vector2(0f, 1f);
        handleRect.pivot = new Vector2(0f, 0.5f);
        handleRect.anchoredPosition = Vector2.zero;
        handleRect.sizeDelta = new Vector2(width, 0f);

        handle.AddComponent<EmptyGraphic>().raycastTarget = true;

        for(int col = 0; col < 2; col++) {
            for(int dotRow = 0; dotRow < 3; dotRow++) {
                GameObject dot = new("Dot");
                dot.transform.SetParent(handle.transform, false);

                RectTransform dotRect = dot.AddComponent<RectTransform>();
                dotRect.anchorMin = new Vector2(0.5f, 0.5f);
                dotRect.anchorMax = new Vector2(0.5f, 0.5f);
                dotRect.pivot = new Vector2(0.5f, 0.5f);
                dotRect.anchoredPosition = new Vector2(col * 8f - 4f, dotRow * 8f - 8f);
                dotRect.sizeDelta = new Vector2(4f, 4f);

                Image dotImg = dot.AddComponent<Image>();
                dotImg.sprite = MainCore.Spr.Get(UISprite.Circle256);
                dotImg.color = new Color(1f, 1f, 1f, 0.4f);
                dotImg.raycastTarget = false;
            }
        }

        return handle;
    }

    // One stat entry row: [⠿ drag] [label] ... [enable dot] [Color] [Swap] [X]
    private static void BuildStatRow(
        Transform parent, StatEntry entry,
        Action commitOrder, Action onDelete, Action onSwap, Action onColor, Action save,
        string idp
    ) {
        RectTransform row = GenerateUI.Row(parent);
        row.gameObject.AddComponent<StatRowMarker>().Entry = entry;

        RectTransform bg = GenerateUI.BackGround();
        bg.SetParent(row, false);

        // 6-dot drag handle. Dragging reorders the row among its siblings;
        // dropping commits the new order to the panel config.
        GameObject handle = MakeDragHandle(bg, "DragHandle", 40f);

        StatRowDrag drag = handle.AddComponent<StatRowDrag>();
        drag.Row = row;
        drag.OnReordered = commitOrder;

        // The "text" stat edits its custom string right here in the row; every
        // other stat shows its (localized) name as a static label.
        if(entry.Id == "text") {
            BuildTextEntryInput(bg, entry, save);
        } else {
            var label = GenerateUI.AddText(bg, true);
            GenerateUI.Localize(
                label,
                GenerateUI.LocaleKeyFromText("PANEL_STAT", entry.Id),
                StatDefaultLabel(entry.Id)
            );
            RectTransform labelRect = label.rectTransform;
            labelRect.offsetMin = new Vector2(48f, 0f);
            labelRect.offsetMax = new Vector2(-300f, 0f);
        }

        // Enable/disable dot: accent = shown, dim = hidden (kept in the list).
        GameObject toggleObj = new("EnableDot");
        toggleObj.transform.SetParent(bg, false);

        RectTransform toggleRect = toggleObj.AddComponent<RectTransform>();
        toggleRect.anchorMin = new Vector2(1f, 0.5f);
        toggleRect.anchorMax = new Vector2(1f, 0.5f);
        toggleRect.pivot = new Vector2(1f, 0.5f);
        toggleRect.anchoredPosition = new Vector2(-240f, 0f);
        toggleRect.sizeDelta = new Vector2(26f, 26f);

        Image toggleImg = toggleObj.AddComponent<Image>();
        toggleImg.sprite = MainCore.Spr.Get(UISprite.Circle256);

        void ApplyToggleColor() {
            toggleImg.color = entry.Enabled
                ? UIColors.ObjectActive
                : new Color(1f, 1f, 1f, 0.18f);
        }
        ApplyToggleColor();

        GenerateUI.AddButton(toggleObj, btn => {
            if(btn != InputButton.Left) return;
            entry.Enabled = !entry.Enabled;
            ApplyToggleColor();
            save();
        });

        // Show-label dot ("T"): accent = label shown, dim = value only. Sits just
        // left of the enable dot.
        GameObject labelDot = new("LabelDot");
        labelDot.transform.SetParent(bg, false);

        RectTransform labelDotRect = labelDot.AddComponent<RectTransform>();
        labelDotRect.anchorMin = new Vector2(1f, 0.5f);
        labelDotRect.anchorMax = new Vector2(1f, 0.5f);
        labelDotRect.pivot = new Vector2(1f, 0.5f);
        labelDotRect.anchoredPosition = new Vector2(-270f, 0f);
        labelDotRect.sizeDelta = new Vector2(26f, 26f);

        Image labelDotImg = labelDot.AddComponent<Image>();
        labelDotImg.sprite = MainCore.Spr.Get(UISprite.Circle256);

        var labelDotText = GenerateUI.AddText(labelDot.transform, true);
        labelDotText.text = "T";
        labelDotText.fontSize = 15f;
        labelDotText.alignment = TextAlignmentOptions.Center;
        labelDotText.raycastTarget = false;

        void ApplyLabelDotColor() {
            labelDotImg.color = entry.ShowLabel
                ? UIColors.ObjectActive
                : new Color(1f, 1f, 1f, 0.18f);
        }
        ApplyLabelDotColor();

        GenerateUI.AddButton(labelDot, btn => {
            if(btn != InputButton.Left) return;
            entry.ShowLabel = !entry.ShowLabel;
            ApplyLabelDotColor();
            save();
        });

        GenerateUI.MiniButton(bg, "Color", "COLOR_SHORT", -144f, 88f, onColor);
        GenerateUI.MiniButton(bg, "Swap", "SWAP", -56f, 84f, onSwap);
        GenerateUI.MiniButton(bg, "X", "DELETE_SHORT", -8f, 44f, onDelete);
    }

    // Collapsible body that hosts a stat's color settings. Starts collapsed
    // and empty; the Color button slides it open/closed.
}

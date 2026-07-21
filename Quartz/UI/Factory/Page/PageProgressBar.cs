using Quartz.Core;
using Quartz.Features.Panels;
using Quartz.Features.ProgressBar;
using Quartz.UI.Generator;
using Quartz.UI.Objects.Impl;
using TMPro;
using UnityEngine;
namespace Quartz.UI.Factory.Page;
internal static class PageProgressBar {
    public static void AppendTo(Transform content) {
        ProgressBarOverlay.EnsureConf();
        ProgressBarSettings conf = ProgressBarOverlay.Conf;
        ProgressBarSettings def = new();
        void Save() => ProgressBarOverlay.Save();
        GenerateUI.CollapsibleSection sec = GenerateUI.FlatSection(
            content, "Progress Bar",
            v => { conf.Enabled = v; ProgressBarOverlay.Apply(); Save(); },
            conf.Enabled,
            "Enable Progress Bar", "progressbar_enable"
        );
        GenerateUI.Toggle(
            GenerateUI.Row(sec.Body),
            def.PrefillStart,
            conf.PrefillStart,
            v => { conf.PrefillStart = v; Save(); },
            "Pre-fill to Start Position",
            "progressbar_prefillstart"
        ).Rect.AddToolTip(
            "DESC_PROGRESSBAR_PREFILLSTART",
            "When a run starts mid-chart (checkpoint or editor play), the bar starts already filled up to that point instead of starting empty."
        );
        GenerateUI.Toggle(
            GenerateUI.Row(sec.Body),
            def.UseMapTime,
            conf.UseMapTime,
            v => { conf.UseMapTime = v; ProgressBarOverlay.Apply(); Save(); },
            "Smooth Fill (Map Time)",
            "progressbar_usemaptime"
        ).Rect.AddToolTip(
            "DESC_PROGRESSBAR_USEMAPTIME",
            "Advance the bar by the chart's elapsed time instead of tiles completed. The fill moves continuously every frame instead of stepping forward on each tile."
        );
        GenerateUI.DropDown(
            GenerateUI.Row(sec.Body),
            def.Style,
            conf.Style,
            new[] { ProgressBarStyle.Modern, ProgressBarStyle.Bar, ProgressBarStyle.Line },
            s => s switch {
                ProgressBarStyle.Bar => MainCore.Tr.Get("PROGRESSBAR_STYLE_BAR", "Bar"),
                ProgressBarStyle.Line => MainCore.Tr.Get("PROGRESSBAR_STYLE_LINE", "Line"),
                _ => MainCore.Tr.Get("PROGRESSBAR_STYLE_MODERN", "Modern"),
            },
            v => {
                conf.Style = v;
                ProgressBarOverlay.Apply();
                Save();
                UICore.Rebuild();
            },
            "progressbar_style",
            260f,
            "Style"
        ).Rect.AddToolTip(
            "DESC_PROGRESSBAR_STYLE",
            "Modern is a floating rounded bar. Bar splits that bar into lit segments. Line is a thin strip stretched edge to edge along the top or bottom of the screen."
        );
        GenerateUI.Localize(GenerateUI.AddTextH1(GenerateUI.Row(sec.Body)), "HEADING_SIZE", "Size");
        if(conf.Style == ProgressBarStyle.Line) AppendLineSize(sec.Body, conf, def, Save);
        else AppendBoxSize(sec.Body, conf, def, Save);
        AppendColors(sec.Body, conf, def, Save);
        if(conf.Style != ProgressBarStyle.Line) {
            GenerateUI.Button(
                GenerateUI.Row(sec.Body),
                () => ProgressBarOverlay.ResetPosition(),
                "Reset Position",
                "progressbar_resetpos"
            ).SetSecondary();
        }
    }
    private static void AppendLineSize(
        RectTransform body,
        ProgressBarSettings conf,
        ProgressBarSettings def,
        Action save
    ) {
        static float thicknessFilter(float v) => Mathf.Clamp(Mathf.Round(v), 1f, 40f);
        UISlider thickness = GenerateUI.Slider(
            GenerateUI.Row(body),
            def.LineThickness,
            1f, 40f, conf.LineThickness, thicknessFilter, null, null,
            "Line Thickness", "progressbar_linethickness"
        );
        thickness.Format = "0 px";
        thickness.OnChanged = v => { conf.LineThickness = v; ProgressBarOverlay.Apply(); };
        thickness.OnComplete = v => { conf.LineThickness = v; ProgressBarOverlay.Apply(); save(); };
        GenerateUI.DropDown(
            GenerateUI.Row(body),
            def.LineAtBottom,
            conf.LineAtBottom,
            new[] { false, true },
            b => b
                ? MainCore.Tr.Get("PROGRESSBAR_LINEEDGE_BOTTOM", "Bottom")
                : MainCore.Tr.Get("PROGRESSBAR_LINEEDGE_TOP", "Top"),
            v => { conf.LineAtBottom = v; ProgressBarOverlay.Apply(); save(); },
            "progressbar_lineedge",
            260f,
            "Screen Edge"
        ).Rect.AddToolTip(
            "DESC_PROGRESSBAR_LINEEDGE",
            "Which screen edge the line is pinned to. It always spans the full width, flush against that edge."
        );
    }
    private static void AppendBoxSize(
        RectTransform body,
        ProgressBarSettings conf,
        ProgressBarSettings def,
        Action save
    ) {
        static float widthFilter(float v) => Mathf.Clamp(Mathf.Round(v), 200f, 1800f);
        UISlider width = GenerateUI.Slider(
            GenerateUI.Row(body),
            def.Width,
            200f, 1800f, conf.Width, widthFilter, null, null,
            "Width", "progressbar_width"
        );
        width.Format = "0 px";
        width.OnChanged = v => { conf.Width = v; ProgressBarOverlay.Apply(); };
        width.OnComplete = v => { conf.Width = v; ProgressBarOverlay.Apply(); save(); };
        static float heightFilter(float v) => Mathf.Clamp(Mathf.Round(v), 2f, 60f);
        UISlider height = GenerateUI.Slider(
            GenerateUI.Row(body),
            def.Height,
            2f, 60f, conf.Height, heightFilter, null, null,
            "Height", "progressbar_height"
        );
        height.Format = "0 px";
        height.OnChanged = v => { conf.Height = v; ProgressBarOverlay.Apply(); };
        height.OnComplete = v => { conf.Height = v; ProgressBarOverlay.Apply(); save(); };
        static float offsetFilter(float v) => Mathf.Clamp(Mathf.Round(v), 0f, 200f);
        UISlider offset = GenerateUI.Slider(
            GenerateUI.Row(body),
            def.TopOffset,
            0f, 200f, conf.TopOffset, offsetFilter, null, null,
            "Top Offset", "progressbar_topoffset"
        );
        offset.Format = "0 px";
        offset.OnChanged = v => { conf.TopOffset = v; ProgressBarOverlay.Apply(); };
        offset.OnComplete = v => { conf.TopOffset = v; ProgressBarOverlay.Apply(); save(); };
        if(conf.Style == ProgressBarStyle.Bar) {
            static float segmentFilter(float v) => Mathf.Clamp(Mathf.Round(v), 4f, 256f);
            UISlider segmentCount = GenerateUI.Slider(
                GenerateUI.Row(body),
                def.SegmentCount,
                4f, 256f, conf.SegmentCount, segmentFilter, null, null,
                "Segment Count", "progressbar_segmentcount"
            );
            segmentCount.Format = "0";
            segmentCount.OnChanged = v => { conf.SegmentCount = Mathf.RoundToInt(v); ProgressBarOverlay.Apply(); };
            segmentCount.OnComplete = v => {
                conf.SegmentCount = Mathf.RoundToInt(v);
                ProgressBarOverlay.Apply();
                save();
            };
            static float gapFilter(float v) => Mathf.Clamp(Mathf.Round(v * 4f) * 0.25f, 0f, 20f);
            UISlider segmentGap = GenerateUI.Slider(
                GenerateUI.Row(body),
                def.SegmentGap,
                0f, 20f, conf.SegmentGap, gapFilter, null, null,
                "Segment Gap", "progressbar_segmentgap"
            );
            segmentGap.Format = "0.## px";
            segmentGap.OnChanged = v => { conf.SegmentGap = v; ProgressBarOverlay.Apply(); };
            segmentGap.OnComplete = v => { conf.SegmentGap = v; ProgressBarOverlay.Apply(); save(); };
        } else {
            static float roundingFilter(float v) => Mathf.Clamp(Mathf.Round(v), 0f, 30f);
            UISlider rounding = GenerateUI.Slider(
                GenerateUI.Row(body),
                def.Rounding,
                0f, 30f, conf.Rounding, roundingFilter, null, null,
                "Corner Rounding", "progressbar_rounding"
            );
            rounding.Format = "0 px";
            rounding.OnChanged = v => { conf.Rounding = v; ProgressBarOverlay.Apply(); };
            rounding.OnComplete = v => { conf.Rounding = v; ProgressBarOverlay.Apply(); save(); };
        }
        static float outlineFilter(float v) => Mathf.Clamp(Mathf.Round(v * 4f) * 0.25f, 0f, 8f);
        UISlider outlineThick = GenerateUI.Slider(
            GenerateUI.Row(body),
            def.OutlineThickness,
            0f, 8f, conf.OutlineThickness, outlineFilter, null, null,
            "Outline Thickness", "progressbar_outlinethickness"
        );
        outlineThick.Format = "0.## px";
        outlineThick.OnChanged = v => { conf.OutlineThickness = v; ProgressBarOverlay.Apply(); };
        outlineThick.OnComplete = v => { conf.OutlineThickness = v; ProgressBarOverlay.Apply(); save(); };
    }
    private static void AppendColors(
        RectTransform body,
        ProgressBarSettings conf,
        ProgressBarSettings def,
        Action save
    ) {
        GenerateUI.Localize(GenerateUI.AddTextH1(GenerateUI.Row(body)), "HEADING_COLOR", "Color");
        StatColor grad = conf.FillGradient;
        Action rebuildFill = null;
        GenerateUI.Toggle(
            GenerateUI.Row(body),
            false,
            grad.Enabled,
            v => { grad.Enabled = v; ProgressBarOverlay.Apply(); save(); rebuildFill?.Invoke(); },
            "Fill Color Gradient",
            "progressbar_fillgradient"
        ).Rect.AddToolTip(
            "DESC_PROGRESSBAR_FILLGRADIENT",
            "Shift the fill colour as the run progresses (0% to 100%) instead of using one flat colour."
        );
        RectTransform fillBody = GenerateUI.MakeBody(body, "FillColorBody");
        rebuildFill = () => {
            GenerateUI.ClearChildren(fillBody);
            if(!grad.Enabled) {
                GenerateUI.ColorPicker(
                    GenerateUI.Row(fillBody),
                    def.GetFillColor(),
                    conf.GetFillColor(),
                    c => { conf.SetFillColor(c); ProgressBarOverlay.Apply(); },
                    c => { conf.SetFillColor(c); ProgressBarOverlay.Apply(); save(); },
                    "Fill Color",
                    "progressbar_fillcolor"
                );
                return;
            }
            for(int i = 0; i < grad.Points.Count; i++) {
                ColorPoint point = grad.Points[i];
                int index = i + 1;
                GenerateUI.ColorPicker(
                    GenerateUI.Row(fillBody),
                    point.GetColor(),
                    point.GetColor(),
                    c => { point.SetColor(c); ProgressBarOverlay.Apply(); },
                    c => { point.SetColor(c); ProgressBarOverlay.Apply(); save(); },
                    string.Format(MainCore.Tr.Get("PROGRESSBAR_STOP_COLOR", "Stop {0} Color"), index),
                    "progressbar_stopcolor_" + i
                );
                UISlider pos = GenerateUI.Slider(
                    GenerateUI.Row(fillBody),
                    point.Pos * 100f, 0f, 100f, point.Pos * 100f,
                    v => Mathf.Clamp(Mathf.Round(v), 0f, 100f), null, null,
                    string.Format(MainCore.Tr.Get("PROGRESSBAR_STOP_POS", "Stop {0} Position"), index),
                    "progressbar_stoppos_" + i
                );
                pos.Format = "0' %'";
                pos.OnChanged = v => { point.Pos = v * 0.01f; ProgressBarOverlay.Apply(); };
                pos.OnComplete = v => {
                    point.Pos = v * 0.01f;
                    grad.SortPoints();
                    ProgressBarOverlay.Apply();
                    save();
                    rebuildFill?.Invoke();
                };
                if(grad.Points.Count > 1) {
                    GenerateUI.Button(
                        GenerateUI.Row(fillBody),
                        () => {
                            grad.Points.Remove(point);
                            grad.SortPoints();
                            ProgressBarOverlay.Apply();
                            save();
                            rebuildFill?.Invoke();
                        },
                        string.Format(MainCore.Tr.Get("PROGRESSBAR_STOP_REMOVE", "Remove Stop {0}"), index),
                        "progressbar_stopremove_" + i
                    ).SetSecondary();
                }
            }
            if(grad.Points.Count < 8) {
                GenerateUI.Button(
                    GenerateUI.Row(fillBody),
                    () => {
                        grad.Points.Add(new ColorPoint(0.5f, grad.Evaluate(0.5f)));
                        grad.SortPoints();
                        ProgressBarOverlay.Apply();
                        save();
                        rebuildFill?.Invoke();
                    },
                    "Add Stop",
                    "progressbar_stopadd"
                ).SetSecondary();
            }
        };
        rebuildFill?.Invoke();
        GenerateUI.ColorPicker(
            GenerateUI.Row(body),
            def.GetBackColor(),
            conf.GetBackColor(),
            c => { conf.SetBackColor(c); ProgressBarOverlay.Apply(); },
            c => { conf.SetBackColor(c); ProgressBarOverlay.Apply(); save(); },
            conf.Style == ProgressBarStyle.Bar ? "Unlit Segment Color" : "Background Color",
            conf.Style == ProgressBarStyle.Bar ? "progressbar_unlitcolor" : "progressbar_backcolor"
        );
        if(conf.Style == ProgressBarStyle.Line) return;
        GenerateUI.ColorPicker(
            GenerateUI.Row(body),
            def.GetOutlineColor(),
            conf.GetOutlineColor(),
            c => { conf.SetOutlineColor(c); ProgressBarOverlay.Apply(); },
            c => { conf.SetOutlineColor(c); ProgressBarOverlay.Apply(); save(); },
            "Outline Color",
            "progressbar_outlinecolor"
        );
    }
    public static void Create(RectTransform parent) =>
        AppendTo(Quartz.UI.Factory.PageFactory.CreateScrollablePage(parent));
}

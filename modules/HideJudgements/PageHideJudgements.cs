using Quartz.Compat.Game;
using Quartz.Core;
using Quartz.Features.Interop;
using Quartz.Features.HideJudgements;
using Quartz.Resource;
using Quartz.UI.Generator;
using Quartz.UI.Objects.Impl;
using Quartz.UI.Utility;
using UnityEngine;
namespace Quartz.UI.Factory.Page;
public static class PageHideJudgements {
    public static void Create(RectTransform parent) =>
        CreateHideJudgements(Quartz.UI.Factory.PageFactory.CreateScrollablePage(parent));
    private static void CreateHideJudgements(Transform content) {
        JudgementPopupHider.EnsureConf();
        JudgementPopupHiderSettings conf = JudgementPopupHider.Conf;
        JudgementPopupHiderSettings def = new();
        (HitKind Margin, string Label, string Id)[] entries = [
            (HitKind.TooEarly, "Too Early", "jpop_tooearly"),
            (HitKind.VeryEarly, "Very Early", "jpop_veryearly"),
            (HitKind.EarlyPerfect, "Early Perfect", "jpop_earlyperfect"),
            (HitKind.Perfect, "Perfect", "jpop_perfect"),
            (HitKind.LatePerfect, "Late Perfect", "jpop_lateperfect"),
            (HitKind.VeryLate, "Very Late", "jpop_verylate"),
            (HitKind.TooLate, "Too Late", "jpop_toolate"),
            (HitKind.Multipress, "Multipress", "jpop_multipress"),
            (HitKind.FailMiss, "Miss", "jpop_miss"),
            (HitKind.FailOverload, "Overload (No Fail)", "jpop_overload_nofail"),
            (HitKind.Auto, "Auto", "jpop_auto"),
            (HitKind.OverPress, "Overload (Fail)", "jpop_overload_fail"),
        ];
        List<RectTransform> maskRows = [];
        void RefreshMaskRows() {
            foreach(RectTransform row in maskRows) row?.gameObject.SetActive(conf.Enabled);
        }
        var sec = GenerateUI.FlatSection(
            content, "Hide Judgements",
            v => {
                conf.Enabled = v;
                RefreshMaskRows();
                JudgementPopupHider.Save();
            },
            conf.Enabled,
            "Enable Hide Judgements", "hidejudgements_enable", def.Enabled
        );
        void AddMaskToggle(int maskBit, string label, string id) {
            RectTransform row = GenerateUI.Row(sec.Body);
            maskRows.Add(row);
            GenerateUI.Toggle(
                row,
                (def.HiddenMask & maskBit) != 0,
                (conf.HiddenMask & maskBit) != 0,
                v => {
                    if(v) conf.HiddenMask |= maskBit;
                    else conf.HiddenMask &= ~maskBit;
                    JudgementPopupHider.Save();
                },
                label,
                id
            );
        }
        bool xperfect = XPerfectBridge.Installed;
        foreach(var entry in entries) {
            if(entry.Margin == HitKind.Perfect && xperfect) {
                AddMaskToggle(1 << JudgementPopupHider.XPerfectPerfectBit, "X Perfect", "jpop_xperfect");
                AddMaskToggle(1 << JudgementPopupHider.PlusPerfectBit, "+ Perfect", "jpop_plusperfect");
                AddMaskToggle(1 << JudgementPopupHider.MinusPerfectBit, "- Perfect", "jpop_minusperfect");
            } else {
                AddMaskToggle(1 << (int)entry.Margin, entry.Label, entry.Id);
            }
        }
        RefreshMaskRows();
    }
}

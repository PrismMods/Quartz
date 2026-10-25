using Quartz.UI.Generator;
using UnityEngine;
namespace Quartz.Features.SpecialDays;
public static class PageSpecialDays {
    private static readonly SpecialDayMode[] Modes = [SpecialDayMode.Auto, SpecialDayMode.On, SpecialDayMode.Off];
    public static void Create(RectTransform parent) =>
        Build(Quartz.UI.Factory.PageFactory.CreateScrollablePage(parent));
    private static void Build(Transform content) {
        SpecialDays.EnsureConf();
        SpecialDaysSettings conf = SpecialDays.Conf;
        SpecialDaysSettings def = new();
        var sec = GenerateUI.FlatSection(content, "Special Days");
        GenerateUI.WrapGrow(GenerateUI.AddLocalizedMutedText(
            GenerateUI.Row(sec.Body, 46f),
            "SPECIALDAY_HINT",
            "Turn the game's seasonal events on or off regardless of the date. Changes show after the next scene load.",
            18f
        ));
        GenerateUI.DropDown(GenerateUI.Row(sec.Body), def.Halloween, conf.Halloween, Modes, Name,
            v => { conf.Halloween = v; SpecialDays.Save(); }, "specialday_halloween", 260f, "Halloween");
        GenerateUI.DropDown(GenerateUI.Row(sec.Body), def.AprilFools, conf.AprilFools, Modes, Name,
            v => { conf.AprilFools = v; SpecialDays.Save(); }, "specialday_aprilfools", 260f, "April Fools");
    }
    private static string Name(SpecialDayMode mode) => mode switch {
        SpecialDayMode.On => GenerateUI.Tr("SPECIALDAY_ON", "Always on"),
        SpecialDayMode.Off => GenerateUI.Tr("SPECIALDAY_OFF", "Always off"),
        _ => GenerateUI.Tr("SPECIALDAY_AUTO", "By date"),
    };
}

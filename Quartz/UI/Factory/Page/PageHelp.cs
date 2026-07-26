using Quartz.Compat.Game;
using Quartz.IO;
using Quartz.Localization;
using Quartz.Resource;
using Quartz.UI.Generator;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
namespace Quartz.UI.Factory.Page;
internal static class PageHelp {
    private static RectTransform listContainer;
    public static void FaqPage(RectTransform parent) {
        listContainer = null;
        RectTransform content = Quartz.UI.Factory.PageFactory.CreateScrollablePage(parent);
        GenerateUI.Localize(GenerateUI.AddTextH1(GenerateUI.Row(content.transform)), "FAQ", "FAQ");
        GameObject list = new("FaqList");
        list.transform.SetParent(content.transform, false);
        listContainer = list.AddComponent<RectTransform>();
        GenerateUI.FitVertical(list, 6f);
        TextLocalization.Refreshed -= Rebuild;
        TextLocalization.Refreshed += Rebuild;
        Rebuild();
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
    }
    private static void Rebuild() {
        if(listContainer == null) return;
        GenerateUI.ClearChildren(listContainer);
        GenerateUI.PruneSections();
        List<FaqEntry> entries = FaqFile.Load();
        if(FaqFile.Error is { } error) {
            TextMeshProUGUI status = GenerateUI.AddMutedText(GenerateUI.Row(listContainer, 34f), 16f, 0.45f, true);
            status.color = UIColors.SoftRed;
            status.overflowMode = TextOverflowModes.Ellipsis;
            status.text = string.Format(
                GenerateUI.Tr("FAQ_LOAD_ERROR", "The built-in questions couldn't be read — {0}"),
                error
            );
        }
        if(entries.Count == 0) {
            GenerateUI.AddLocalizedMutedText(
                GenerateUI.Row(listContainer, 54f),
                "FAQ_NONE",
                "There are no questions to show.",
                18f,
                0.6f,
                true
            );
            return;
        }
        string category = null;
        foreach(FaqEntry entry in entries) {
            if(!string.IsNullOrEmpty(entry.Category) && entry.Category != category) {
                category = entry.Category;
                TextMeshProUGUI header = GenerateUI.AddMutedText(GenerateUI.Row(listContainer, 46f), 20f, 0.55f, true);
                header.verticalAlignment = VerticalAlignmentOptions.Bottom;
                header.text = category;
            }
            GenerateUI.CollapsibleSection section = GenerateUI.Collapsible(listContainer, entry.Question, false);
            AddAnswer(section.Body, entry.Answer);
        }
    }
    private static void AddAnswer(Transform parent, string answer) {
        GameObject obj = new("Answer");
        obj.transform.SetParent(parent, false);
        obj.AddComponent<RectTransform>();
        TextMeshProUGUI text = obj.AddComponent<TextMeshProUGUI>();
        text.font = FontManager.Current;
        text.fontSize = 19f;
        text.color = new Color(1f, 1f, 1f, 0.75f);
        text.alignment = TextAlignmentOptions.TopLeft;
        text.characterSpacing = -3f;
        text.margin = new Vector4(16f, 4f, 250f, 12f);
        TextCompat.Wrap(text);
        text.text = string.IsNullOrWhiteSpace(answer) ? "—" : answer;
    }
}

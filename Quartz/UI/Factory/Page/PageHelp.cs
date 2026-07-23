using Quartz.Compat.Game;
using Quartz.IO;
using Quartz.Localization;
using Quartz.Resource;
using Quartz.UI.Generator;
using Quartz.UI.Objects.Impl;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
namespace Quartz.UI.Factory.Page;
internal static class PageHelp {
    private static RectTransform listContainer;
    private static TextMeshProUGUI statusText;
    public static void FaqPage(RectTransform parent) {
        listContainer = null;
        statusText = null;
        RectTransform content = Quartz.UI.Factory.PageFactory.CreateScrollablePage(parent);
        GenerateUI.Localize(GenerateUI.AddTextH1(GenerateUI.Row(content.transform)), "FAQ", "FAQ");
        var hintRow = GenerateUI.Row(content.transform, 76f);
        var hintText = GenerateUI.AddMutedText(hintRow, 17f, 0.45f, true);
        TextCompat.Wrap(hintText);
        hintText.rectTransform.offsetMax = new Vector2(-250f, 0f);
        GenerateUI.Localize(
            hintText,
            "FAQ_HINT",
            "These questions live in FAQ.json in your Quartz folder. Edit that file to reword an answer, "
            + "add your own questions or drop the ones you don't need, then press Reload. Delete the file and reload to get the defaults back."
        );
        var actionsRow = GenerateUI.Row(content.transform);
        GenerateUI.ButtonRow(actionsRow);
        UIButton openFileBtn = GenerateUI.Button(actionsRow, FaqFile.OpenFile, "Open File", "faq_open_file");
        GenerateUI.FixWidth(openFileBtn, 200f);
        openFileBtn.Rect.AddToolTip(
            "DESC_FAQ_OPEN_FILE",
            "Opens FAQ.json in whatever your system uses for .json files, writing the default file first if it isn't there yet."
        );
        UIButton folderBtn = GenerateUI.Button(actionsRow, FaqFile.OpenFolder, "Open Folder", "faq_open_folder");
        GenerateUI.FixWidth(folderBtn, 200f);
        folderBtn.Rect.AddToolTip("DESC_FAQ_OPEN_FOLDER", "Opens the Quartz data folder that holds FAQ.json and your settings.");
        UIButton reloadBtn = GenerateUI.Button(actionsRow, Rebuild, "Reload", "faq_reload").SetSecondary();
        GenerateUI.FixWidth(reloadBtn, 200f);
        reloadBtn.Rect.AddToolTip("DESC_FAQ_RELOAD", "Re-reads FAQ.json from disk and rebuilds the list below.");
        var statusRow = GenerateUI.Row(content.transform, 34f);
        statusText = GenerateUI.AddMutedText(statusRow, 16f, 0.45f, true);
        statusText.overflowMode = TextOverflowModes.Ellipsis;
        statusText.text = "";
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
        if(statusText != null) {
            string error = FaqFile.Error;
            statusText.color = error == null ? new Color(1f, 1f, 1f, 0.45f) : UIColors.SoftRed;
            statusText.text = error == null
                ? ""
                : string.Format(
                    GenerateUI.Tr("FAQ_ERROR", "FAQ.json couldn't be read, showing the built-in questions instead — {0}"),
                    error
                );
        }
        if(entries.Count == 0) {
            GenerateUI.AddLocalizedMutedText(
                GenerateUI.Row(listContainer, 54f),
                "FAQ_EMPTY",
                "FAQ.json has no questions in it yet.",
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

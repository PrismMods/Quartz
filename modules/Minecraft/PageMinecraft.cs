using Quartz.UI.Generator;
using UnityEngine;
namespace Quartz.UI.Factory.Page;
public static class PageMinecraft {
    public static void Create(RectTransform parent) {
        Transform content = Quartz.UI.Factory.PageFactory.CreateScrollablePage(parent);
        GenerateUI.Localize(GenerateUI.AddTextH1(GenerateUI.Row(content)), "SECTION_MINECRAFT", "Minecraft");
        GenerateUI.AddLocalizedMutedText(
            GenerateUI.Row(content, 40f),
            "MINECRAFT_EMPTY",
            "Nothing here yet — this tab is a placeholder."
        );
    }
}

#nullable enable
using Quartz.Core;
using Quartz.Features.Minecraft;
using Quartz.UI.Generator;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace Quartz.UI.Factory.Page;
public static class PageMinecraft {
    public static void Create(RectTransform parent) {
        Transform content = Quartz.UI.Factory.PageFactory.CreateScrollablePage(parent);
        GenerateUI.Localize(GenerateUI.AddTextH1(GenerateUI.Row(content)), "SECTION_MINECRAFT", "Minecraft");
        if(McPaths.PackageId() == null) {
            GenerateUI.AddLocalizedMutedText(
                GenerateUI.Row(content, 40f),
                "MINECRAFT_UNSUPPORTED",
                "No browser engine build exists for this platform and architecture."
            );
            return;
        }
        if(McPaths.IsInstalled(MainCore.Paths.RootPath)) BuildBrowser(content);
        else BuildInstaller(content);
    }
    private static void BuildInstaller(Transform content) {
        GenerateUI.AddLocalizedMutedText(
            GenerateUI.Row(content, 60f),
            "MINECRAFT_INSTALL_INFO",
            "Playing Minecraft Classic in-game needs a browser engine (Chromium). It downloads once, is about 133 MB, and is stored beside your Quartz settings."
        );
        RectTransform statusRow = GenerateUI.Row(content, 40f);
        TextMeshProUGUI status = GenerateUI.AddMutedText(statusRow);
        status.text = string.Empty;
        McInstallPanel panel = statusRow.gameObject.AddComponent<McInstallPanel>();
        panel.Label = status;
        panel.OnInstalled = () => MainCore.Log.Msg("[Minecraft] engine installed; reopen the tab to play.");
        GenerateUI.Button(
            GenerateUI.Row(content, 40f),
            () => panel.Begin(),
            "Download browser engine",
            "MINECRAFT_INSTALL_BUTTON"
        );
        AddDisclaimer(content);
    }
    private static void BuildBrowser(Transform content) {
        RectTransform host = GenerateUI.Row(content, 720f);
        GameObject surfaceObj = new("McBrowserSurface");
        // Inactive until fully configured: AddComponent on an ACTIVE object runs Awake
        // and OnEnable synchronously, so the view would start before DataRoot is
        // assigned below, take its early return, and never start again.
        surfaceObj.SetActive(false);
        surfaceObj.transform.SetParent(host, false);
        RectTransform surface = surfaceObj.AddComponent<RectTransform>();
        surface.anchorMin = Vector2.zero;
        surface.anchorMax = Vector2.one;
        surface.offsetMin = Vector2.zero;
        surface.offsetMax = Vector2.zero;
        RawImage image = surfaceObj.AddComponent<RawImage>();
        image.color = Color.white;
        McBrowserView view = surfaceObj.AddComponent<McBrowserView>();
        view.DataRoot = MainCore.Paths.RootPath;
        surfaceObj.SetActive(true);
        AddDisclaimer(content);
    }
    private static void AddDisclaimer(Transform content) {
        GenerateUI.AddLocalizedMutedText(
            GenerateUI.Row(content, 60f),
            "MINECRAFT_DISCLAIMER",
            "Minecraft Classic is played at classic.minecraft.net, a service of Mojang Synergies AB. Minecraft is a trademark of Mojang Synergies AB. Quartz is not affiliated with, endorsed by, or sponsored by Mojang or Microsoft, and redistributes no game code or assets."
        );
    }
}

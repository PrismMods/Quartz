using System.Globalization;
using Quartz.Core;
using Quartz.Localization;
using Quartz.Modules;
using Quartz.UI.Generator;
using Quartz.UI.Nav;
using TMPro;
using UnityEngine;
namespace Quartz.UI.Factory.Page;
internal static class PageModuleCategory {
    public static void Create(RectTransform parent, string categoryKey) {
        ModuleCatalogService.EnsureLoaded();
        RectTransform content = Quartz.UI.Factory.PageFactory.CreateScrollablePage(parent);
        NavCategory category = NavRegistry.Category(categoryKey);
        string title = category == null
            ? categoryKey
            : MainCore.Tr.Get(category.LocaleKey, category.Title);
        GenerateUI.AddTextH1(GenerateUI.Row(content.transform)).text = title;
        IReadOnlyList<ModuleService.Handle> installed = ModuleCategories.Installed(categoryKey);
        IReadOnlyList<ModuleCatalogEntry> available = ModuleCategories.Available(categoryKey);
        IReadOnlyList<ModuleManifest> bundled = ModuleCategories.Bundled(categoryKey);
        GenerateUI.AddMutedText(GenerateUI.Row(content.transform, 34f), 16f, 0.45f, true).text =
            string.Format(
                CultureInfo.InvariantCulture,
                MainCore.Tr.Get("MODULES_CATEGORY_SUMMARY", "{0} installed · {1} available"),
                installed.Count, available.Count + bundled.Count
            );
        if(installed.Count == 0 && available.Count == 0 && bundled.Count == 0) {
            GenerateUI.AddMutedText(GenerateUI.Row(content.transform, 48f), 17f, 0.45f, true)
                .gameObject.AddComponent<TextLocalization>()
                .Init("MODULES_CATEGORY_EMPTY", "Nothing for this tab yet.");
            return;
        }
        foreach(ModuleService.Handle handle in installed)
            PageModuleRows.Installed(content.transform, handle, PageModules.UpdateFor(handle));
        if(available.Count == 0 && bundled.Count == 0) return;
        GenerateUI.AddTextH1(GenerateUI.Row(content.transform))
            .gameObject.AddComponent<TextLocalization>().Init("MODULES_BROWSE", "Get more features");
        foreach(ModuleManifest manifest in bundled) PageModuleRows.Bundled(content.transform, manifest);
        foreach(ModuleCatalogEntry entry in available) PageModuleRows.Available(content.transform, entry);
    }
}

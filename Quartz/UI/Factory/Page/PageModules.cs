using System.Globalization;
using Quartz.Core;
using Quartz.Localization;
using Quartz.Modules;
using Quartz.UI.Generator;
using Quartz.UI.Nav;
using Quartz.UI.Objects.Impl;
using TMPro;
using UnityEngine;
namespace Quartz.UI.Factory.Page;
internal static class PageModules {
    private static bool hooked;
    public static void Create(RectTransform parent) {
        Hook();
        ModuleCatalogService.EnsureLoaded();
        RectTransform content = Quartz.UI.Factory.PageFactory.CreateScrollablePage(parent);
        GenerateUI.AddTextH1(GenerateUI.Row(content.transform))
            .gameObject.AddComponent<TextLocalization>().Init("MODULES", "Modules");
        GenerateUI.AddMutedText(GenerateUI.Row(content.transform, 74f), 17f, 0.45f, true)
            .gameObject.AddComponent<TextLocalization>().Init(
                "MODULES_HINT",
                "Every feature ships as a module. Turn a tab off to hide it from the sidebar; open its page here to install, remove, or switch individual modules on and off."
            );
        Status(content);
        Actions(content);
        Tabs(content);
        Orphans(content);
    }
    private static void Hook() {
        if(hooked) return;
        hooked = true;
        ModuleCatalogService.OnChanged += RefreshIfShowing;
        ModuleInstallService.OnChanged += RefreshIfShowing;
    }
    private static void RefreshIfShowing() {
        if(UICore.Pages.Count == 0) return;
        if(NavRegistry.CategoryKeyFor(UICore.CurrentMenuState) != CorePages.ModulesCategoryKey) return;
        UICore.Rebuild();
    }
    private static void Status(RectTransform content) {
        IReadOnlyList<ModuleService.Handle> modules = ModuleService.Modules;
        int active = 0;
        foreach(ModuleService.Handle handle in modules)
            if(handle.Loaded) active++;
        TextMeshProUGUI summary = GenerateUI.AddMutedText(GenerateUI.Row(content.transform, 30f), 16f, 0.45f, true);
        summary.text = string.Format(
            CultureInfo.InvariantCulture,
            MainCore.Tr.Get("MODULES_SUMMARY", "{0} installed · {1} active · catalog {2}"),
            modules.Count, active, CatalogWord()
        );
        string note = Note();
        if(note == null) return;
        TextMeshProUGUI status = GenerateUI.AddMutedText(GenerateUI.Row(content.transform, 34f), 15f, 0.45f, true);
        status.overflowMode = TextOverflowModes.Ellipsis;
        if(ModuleInstallService.Error != null || ModuleCatalogService.Error != null) status.color = UIColors.SoftRed;
        status.text = note;
    }
    private static string CatalogWord() => ModuleCatalogService.Source switch {
        CatalogSource.Network => MainCore.Tr.Get("MODULES_CATALOG_LIVE", "up to date"),
        CatalogSource.Cache => MainCore.Tr.Get("MODULES_CATALOG_CACHED", "cached"),
        CatalogSource.Embedded => MainCore.Tr.Get("MODULES_CATALOG_BUILTIN", "built-in"),
        _ => MainCore.Tr.Get("MODULES_CATALOG_NONE", "unavailable"),
    };
    private static string Note() {
        if(ModuleInstallService.Busy) {
            return string.Format(
                CultureInfo.InvariantCulture,
                MainCore.Tr.Get("MODULES_INSTALLING", "Installing {0}… {1}%"),
                ModuleInstallService.ActiveId,
                Mathf.RoundToInt(Mathf.Clamp01(ModuleInstallService.Progress) * 100f)
            );
        }
        if(ModuleCatalogService.Busy) return MainCore.Tr.Get("MODULES_REFRESHING", "Refreshing the catalog…");
        return ModuleInstallService.Error ?? ModuleCatalogService.Error;
    }
    private static void Actions(RectTransform content) {
        RectTransform actions = GenerateUI.Row(content.transform);
        GenerateUI.ButtonRow(actions);
        UIButton refresh = GenerateUI.Button(actions, ModuleCatalogService.Refresh, "Refresh", "modules_refresh");
        GenerateUI.FixWidth(refresh, 170f);
        refresh.Rect.AddToolTip("DESC_MODULES_REFRESH", "Re-downloads the module catalog from GitHub.");
        UIButton reload = GenerateUI.Button(actions, ModuleService.ReloadAll, "Reload Modules", "modules_reload").SetSecondary();
        GenerateUI.FixWidth(reload, 200f);
        reload.Rect.AddToolTip(
            "DESC_MODULES_RELOAD",
            "Unloads every module, re-scans the Module folder, and rebuilds this window. A module's code is only read from disk once per session — restart the game to pick up an updated .qmod."
        );
        UIButton folder = GenerateUI.Button(actions, ModuleService.OpenModuleFolder, "Open Folder", "modules_open_folder").SetSecondary();
        GenerateUI.FixWidth(folder, 200f);
        folder.Rect.AddToolTip("DESC_MODULES_OPEN_FOLDER", "Opens the Module folder in your file browser.");
    }
    private static void Tabs(RectTransform content) {
        GenerateUI.AddTextH1(GenerateUI.Row(content.transform))
            .gameObject.AddComponent<TextLocalization>().Init("MODULES_TABS", "Tabs");
        foreach(NavCategory category in ModuleCategories.Togglable()) {
            string key = category.Key;
            IReadOnlyList<ModuleService.Handle> installed = ModuleCategories.Installed(key);
            IReadOnlyList<ModuleCatalogEntry> available = ModuleCategories.Available(key);
            IReadOnlyList<ModuleManifest> bundled = ModuleCategories.Bundled(key);
            RectTransform tabRow = GenerateUI.Row(content.transform, 64f);
            GenerateUI.Toggle(
                tabRow,
                true,
                ModuleCategories.IsEnabled(key),
                v => ModuleCategories.SetEnabled(key, v),
                MainCore.Tr.Get(category.LocaleKey, category.Title),
                "modules_tab_" + key,
                installed.Count > 0 ? 168f : 52f
            );
            if(installed.Count > 0) {
                List<string> ids = [];
                foreach(ModuleService.Handle handle in installed) ids.Add(handle.Id);
                PageModuleRows.RemoveButton(
                    tabRow,
                    () => {
                        foreach(string moduleId in ids) ModuleService.Remove(moduleId);
                    },
                    "DESC_MODULES_REMOVE_TAB",
                    "Deletes every module in this tab from disk. Their settings are kept, and anything "
                        + "in another tab that depends on them stops working until you re-install."
                );
            }
            TextMeshProUGUI meta = GenerateUI.AddMutedText(GenerateUI.Row(content.transform, 30f), 15f, 0.45f, true);
            meta.overflowMode = TextOverflowModes.Ellipsis;
            meta.text = string.Format(
                CultureInfo.InvariantCulture,
                MainCore.Tr.Get("MODULES_TAB_SUMMARY", "{0} installed · {1} available"),
                installed.Count, available.Count + bundled.Count
            );
            if(installed.Count == 0 && available.Count == 0 && bundled.Count == 0) continue;
            GenerateUI.CollapsibleSection section = GenerateUI.Collapsible(
                content.transform,
                MainCore.Tr.Get(category.LocaleKey, category.Title) + $"  ({installed.Count + available.Count + bundled.Count})",
                startExpanded: false
            );
            foreach(ModuleService.Handle handle in installed) PageModuleRows.Installed(section.Body, handle);
            foreach(ModuleManifest manifest in bundled) PageModuleRows.Bundled(section.Body, manifest);
            foreach(ModuleCatalogEntry entry in available) PageModuleRows.Available(section.Body, entry);
        }
    }
    private static void Orphans(RectTransform content) {
        List<ModuleService.Handle> orphans = [];
        foreach(ModuleService.Handle handle in ModuleService.Modules) {
            string group = ModuleCategories.Group(handle);
            if(NavRegistry.Category(group) is { Togglable: true }) continue;
            orphans.Add(handle);
        }
        if(orphans.Count == 0) return;
        GenerateUI.AddTextH1(GenerateUI.Row(content.transform))
            .gameObject.AddComponent<TextLocalization>().Init("MODULES_OTHER", "Other modules");
        foreach(ModuleService.Handle handle in orphans) PageModuleRows.Installed(content.transform, handle);
    }
}

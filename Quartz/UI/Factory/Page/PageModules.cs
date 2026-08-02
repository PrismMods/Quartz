using System.Globalization;
using Quartz.Core;
using Quartz.Localization;
using Quartz.Modules;
using Quartz.Resource;
using Quartz.UI.Generator;
using Quartz.UI.Nav;
using Quartz.UI.Objects.Impl;
using Quartz.Utility;
using TMPro;
using UnityEngine;
namespace Quartz.UI.Factory.Page;
internal static class PageModules {
    private sealed class FilterRow {
        public GameObject[] Objects;
        public string Haystack;
    }
    private sealed class FilterSection {
        public GenerateUI.CollapsibleSection Section;
        public readonly List<FilterRow> Rows = [];
        public bool RestoreExpanded;
    }
    private static bool hooked;
    private static bool filtering;
    private static string filterQuery = "";
    private static readonly List<FilterSection> filterSections = [];
    private static readonly HashSet<string> pendingUpdates = new(StringComparer.Ordinal);
    private static readonly HashSet<string> updatedIds = new(StringComparer.Ordinal);
    private static TextMeshProUGUI summaryText;
    private static TextMeshProUGUI noteText;
    private static Color noteColor;
    private static GameObject barRow;
    private static RectTransform barFill;
    internal static bool WasUpdated(string id) => updatedIds.Contains(id);
    internal static void MarkPendingUpdate(string id) => pendingUpdates.Add(id);
    public static void Create(RectTransform parent) {
        Hook();
        ModuleCatalogService.EnsureLoaded();
        summaryText = null;
        noteText = null;
        barRow = null;
        barFill = null;
        filterSections.Clear();
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
        Filter(content);
        Tabs(content);
        Orphans(content);
        if(!string.IsNullOrEmpty(filterQuery)) ApplyFilter(filterQuery);
    }
    private static void Hook() {
        if(hooked) return;
        hooked = true;
        ModuleCatalogService.OnChanged += RefreshIfShowing;
        ModuleInstallService.OnChanged += RefreshIfShowing;
    }
    private static void RefreshIfShowing() {
        if(!ModuleInstallService.Busy) {
            if(ModuleInstallService.Error == null) updatedIds.UnionWith(pendingUpdates);
            pendingUpdates.Clear();
        }
        if(UICore.Pages.Count == 0) return;
        if(NavRegistry.CategoryKeyFor(UICore.CurrentMenuState) != CorePages.ModulesCategoryKey) return;
        if(ModuleInstallService.Busy || ModuleCatalogService.Busy) {
            UpdateStatus();
            return;
        }
        UICore.Rebuild();
    }
    internal static ModuleCatalogEntry UpdateFor(ModuleService.Handle handle) {
        if(handle == null || updatedIds.Contains(handle.Id)) return null;
        ModuleCatalogEntry entry = ModuleCatalogService.Catalog?.Find(handle.Id);
        if(entry == null || entry.CoreAbi != Info.ModuleAbi) return null;
        return ModuleCatalog.IsNewer(entry.Version, handle.Version) ? entry : null;
    }
    private static void Status(RectTransform content) {
        summaryText = GenerateUI.AddMutedText(GenerateUI.Row(content.transform, 30f), 16f, 0.45f, true);
        summaryText.overflowMode = TextOverflowModes.Ellipsis;
        noteText = GenerateUI.AddMutedText(GenerateUI.Row(content.transform, 34f), 15f, 0.45f, true);
        noteText.overflowMode = TextOverflowModes.Ellipsis;
        noteColor = noteText.color;
        RectTransform bar = GenerateUI.Row(content.transform, 10f);
        barRow = bar.gameObject;
        RectTransform track = GenerateUI.BackGround(250f);
        track.SetParent(bar, false);
        GameObject fillObj = new("Fill");
        fillObj.transform.SetParent(track, false);
        barFill = fillObj.AddComponent<RectTransform>();
        barFill.anchorMin = Vector2.zero;
        barFill.anchorMax = new Vector2(0f, 1f);
        barFill.offsetMin = Vector2.zero;
        barFill.offsetMax = Vector2.zero;
        UnityEngine.UI.Image fill = fillObj.AddComponent<UnityEngine.UI.Image>();
        fill.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P2048);
        fill.type = UnityEngine.UI.Image.Type.Sliced;
        fill.color = UIColors.ObjectActive;
        fill.raycastTarget = false;
        UpdateStatus();
    }
    private static void UpdateStatus() {
        if(summaryText == null || noteText == null) return;
        IReadOnlyList<ModuleService.Handle> modules = ModuleService.Modules;
        int active = 0;
        int updates = 0;
        foreach(ModuleService.Handle handle in modules) {
            if(handle.Loaded && handle.Enabled) active++;
            if(UpdateFor(handle) != null) updates++;
        }
        string summary = string.Format(
            CultureInfo.InvariantCulture,
            MainCore.Tr.Get("MODULES_SUMMARY", "{0} installed · {1} active · catalog {2}"),
            modules.Count, active, CatalogWord()
        );
        if(updates > 0) {
            summary += " · " + string.Format(
                CultureInfo.InvariantCulture,
                MainCore.Tr.Get("MODULES_SUMMARY_UPDATES", "{0} update(s) available"),
                updates
            );
        }
        summaryText.text = summary;
        string note = Note();
        bool failed = ModuleInstallService.Error != null || ModuleCatalogService.Error != null;
        noteText.color = failed && note != null ? UIColors.SoftRed : noteColor;
        noteText.text = note ?? "";
        if(barRow == null) return;
        bool downloading = ModuleInstallService.Busy && ModuleInstallService.Progress >= 0f;
        barRow.SetActive(downloading);
        if(downloading && barFill != null)
            barFill.anchorMax = new Vector2(Mathf.Clamp01(ModuleInstallService.Progress), 1f);
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
    private static void Filter(RectTransform content) {
        UIInput input = GenerateUI.Input(
            GenerateUI.Row(content.transform),
            "",
            filterQuery,
            ApplyFilter,
            "Filter modules",
            MainCore.Spr.Get(UISprite.MagnifyingGlass128),
            "modules_filter"
        );
        input.Placeholder.gameObject.AddComponent<TextLocalization>().Init("MODULES_FILTER", "Filter modules");
        input.InputField.characterLimit = 40;
    }
    private static string Norm(string text) {
        string normalized = StringUtils.Normalize(text);
        if(MainCore.Conf.Language == "ko-KR" && !string.IsNullOrEmpty(normalized))
            normalized = StringUtils.NormalizeToHangulChosung(normalized);
        return normalized;
    }
    private static void ApplyFilter(string raw) {
        filterQuery = raw ?? "";
        string query = Norm(filterQuery);
        bool active = !string.IsNullOrWhiteSpace(query);
        if(active && !filtering)
            foreach(FilterSection fs in filterSections)
                if(fs.Section != null) fs.RestoreExpanded = fs.Section.Expanded;
        filtering = active;
        foreach(FilterSection fs in filterSections) {
            int matches = 0;
            foreach(FilterRow row in fs.Rows) {
                bool match = !active || row.Haystack.Contains(query);
                if(match) matches++;
                foreach(GameObject go in row.Objects)
                    if(go != null) go.SetActive(match);
            }
            if(fs.Section?.Body == null) continue;
            bool show = !active || matches > 0;
            fs.Section.Section.gameObject.SetActive(show);
            if(active && show) fs.Section.SetExpanded(true, false, false);
            else if(!active) fs.Section.SetExpanded(fs.RestoreExpanded, false, false);
        }
    }
    private static void Tabs(RectTransform content) {
        H2(content, "MODULES_TABS", "Tabs");
        foreach(NavCategory category in ModuleCategories.Togglable()) {
            string key = category.Key;
            IReadOnlyList<ModuleService.Handle> installed = ModuleCategories.Installed(key);
            IReadOnlyList<ModuleCatalogEntry> available = ModuleCategories.Available(key);
            IReadOnlyList<ModuleManifest> bundled = ModuleCategories.Bundled(key);
            int total = installed.Count + available.Count + bundled.Count;
            string tabName = MainCore.Tr.Get(category.LocaleKey, category.Title);
            if(total == 0) {
                GenerateUI.Toggle(
                    GenerateUI.Row(content.transform, 64f),
                    true,
                    ModuleCategories.IsEnabled(key),
                    v => ModuleCategories.SetEnabled(key, v),
                    tabName,
                    "modules_tab_" + key,
                    52f
                );
                continue;
            }
            GenerateUI.CollapsibleSection section = GenerateUI.Collapsible(
                content.transform,
                category.Title,
                false,
                v => ModuleCategories.SetEnabled(key, v),
                ModuleCategories.IsEnabled(key)
            );
            Retitle(section, category, installed.Count, total);
            FilterSection fs = new() { Section = section, RestoreExpanded = section.Expanded };
            filterSections.Add(fs);
            SectionActions(section.Body, installed, available, bundled);
            foreach(ModuleService.Handle handle in installed) {
                string name = handle.Manifest?.NameKey == null
                    ? handle.Name
                    : MainCore.Tr.Get(handle.Manifest.NameKey, handle.Name);
                fs.Rows.Add(new FilterRow {
                    Objects = PageModuleRows.Installed(section.Body, handle, UpdateFor(handle)),
                    Haystack = Norm(tabName + " " + name + " " + handle.Id),
                });
            }
            foreach(ModuleManifest manifest in bundled) {
                string name = manifest.NameKey == null ? manifest.Name : MainCore.Tr.Get(manifest.NameKey, manifest.Name);
                fs.Rows.Add(new FilterRow {
                    Objects = PageModuleRows.Bundled(section.Body, manifest),
                    Haystack = Norm(tabName + " " + name + " " + manifest.Id),
                });
            }
            foreach(ModuleCatalogEntry entry in available) {
                string name = entry.NameKey == null ? entry.Name : MainCore.Tr.Get(entry.NameKey, entry.Name);
                string desc = entry.DescKey == null ? entry.Desc : MainCore.Tr.Get(entry.DescKey, entry.Desc);
                fs.Rows.Add(new FilterRow {
                    Objects = PageModuleRows.Available(section.Body, entry),
                    Haystack = Norm(tabName + " " + name + " " + entry.Id + " " + desc),
                });
            }
        }
    }
    private static void H2(RectTransform content, string key, string fallback) {
        TextMeshProUGUI header = GenerateUI.AddTextH1(GenerateUI.Row(content.transform, 42f));
        header.fontSize = 24f;
        header.gameObject.AddComponent<TextLocalization>().Init(key, fallback);
    }
    private static void Retitle(GenerateUI.CollapsibleSection section, NavCategory category, int installed, int total) {
        Transform bar = section.HeaderObj?.transform.Find("Bar");
        if(bar == null) return;
        TextMeshProUGUI label = bar.Find("Label")?.GetComponent<TextMeshProUGUI>();
        if(label != null) {
            label.GetComponent<TextLocalization>()?.Init(category.LocaleKey, category.Title);
            label.rectTransform.offsetMax = new Vector2(-170f, 0f);
        }
        GameObject countObj = new("Count");
        countObj.transform.SetParent(bar, false);
        RectTransform rect = countObj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 0f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 0.5f);
        rect.anchoredPosition = new Vector2(-92f, 0f);
        rect.sizeDelta = new Vector2(70f, 0f);
        TextMeshProUGUI count = countObj.AddComponent<TextMeshProUGUI>();
        count.font = FontManager.Current;
        count.fontSize = 16f;
        count.color = new Color(1f, 1f, 1f, 0.45f);
        count.alignment = TextAlignmentOptions.Right;
        count.verticalAlignment = VerticalAlignmentOptions.Middle;
        count.raycastTarget = false;
        count.text = installed.ToString(CultureInfo.InvariantCulture)
            + "/" + total.ToString(CultureInfo.InvariantCulture);
    }
    private static void SectionActions(
        RectTransform body,
        IReadOnlyList<ModuleService.Handle> installed,
        IReadOnlyList<ModuleCatalogEntry> available,
        IReadOnlyList<ModuleManifest> bundled
    ) {
        List<string> catalogIds = [];
        foreach(ModuleCatalogEntry entry in available)
            if(entry.CoreAbi == Info.ModuleAbi) catalogIds.Add(entry.Id);
        List<string> bundledIds = [];
        foreach(ModuleManifest manifest in bundled) bundledIds.Add(manifest.Id);
        List<string> updateIds = [];
        foreach(ModuleService.Handle handle in installed)
            if(UpdateFor(handle) != null) updateIds.Add(handle.Id);
        if(catalogIds.Count + bundledIds.Count == 0 && installed.Count == 0) return;
        RectTransform row = GenerateUI.Row(body, 50f);
        GenerateUI.ButtonRow(row);
        if(catalogIds.Count + bundledIds.Count > 0) {
            UIButton install = GenerateUI.Button(row, () => {
                if(ModuleInstallService.Busy) return;
                foreach(string moduleId in bundledIds) ModuleBundle.Install(moduleId);
                ModuleInstallService.InstallAll(catalogIds);
            }, "Install All", "modules_install_tab");
            GenerateUI.FixWidth(install, 170f);
            install.Rect.AddToolTip(
                "DESC_MODULES_INSTALL_TAB",
                "Installs every module this tab offers — the ones bundled with Quartz go in instantly, "
                    + "the rest download from GitHub."
            );
        }
        if(updateIds.Count > 1) {
            UIButton update = GenerateUI.Button(row, () => {
                if(ModuleInstallService.Busy) return;
                foreach(string moduleId in updateIds) MarkPendingUpdate(moduleId);
                ModuleInstallService.InstallAll(updateIds);
            }, "Update All", "modules_update_tab");
            GenerateUI.FixWidth(update, 170f);
            update.Rect.AddToolTip(
                "DESC_MODULES_UPDATE_TAB",
                "Downloads the newer version of every module in this tab that has one. Restart the game "
                    + "afterwards to use them."
            );
        }
        if(installed.Count > 0) {
            List<string> ids = [];
            foreach(ModuleService.Handle handle in installed) ids.Add(handle.Id);
            UIButton remove = PageModuleRows.Armed(row, () => {
                foreach(string moduleId in ids) ModuleService.Remove(moduleId);
            }, "Remove All", "modules_remove_tab");
            GenerateUI.FixWidth(remove, 170f);
            remove.Rect.AddToolTip(
                "DESC_MODULES_REMOVE_TAB",
                "Deletes every module in this tab from disk. Their settings are kept, and anything "
                    + "in another tab that depends on them stops working until you re-install."
            );
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
        H2(content, "MODULES_OTHER", "Other modules");
        FilterSection fs = new();
        filterSections.Add(fs);
        foreach(ModuleService.Handle handle in orphans) {
            fs.Rows.Add(new FilterRow {
                Objects = PageModuleRows.Installed(content.transform, handle, UpdateFor(handle)),
                Haystack = Norm(handle.Name + " " + handle.Id),
            });
        }
    }
}

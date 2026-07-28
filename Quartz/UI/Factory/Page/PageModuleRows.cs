using System.Globalization;
using Quartz.Compat.Game;
using Quartz.Core;
using Quartz.Modules;
using Quartz.UI.Generator;
using Quartz.UI.Objects.Impl;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace Quartz.UI.Factory.Page;
internal static class PageModuleRows {
    public static void Installed(Transform parent, ModuleService.Handle handle) {
        string id = handle.Id;
        string nameKey = handle.Manifest?.NameKey;
        RectTransform row = GenerateUI.Row(parent, 64f);
        UIToggle toggle = GenerateUI.Toggle(
            row,
            true,
            handle.Enabled,
            v => ModuleService.SetEnabled(id, v),
            nameKey == null ? handle.Name : MainCore.Tr.Get(nameKey, handle.Name),
            "module_" + id,
            168f
        );
        if(nameKey != null)
            toggle.Label?.GetComponent<Quartz.Localization.TextLocalization>()?.Init(nameKey, handle.Name);
        Remove(row, id);
        string error = handle.Error;
        bool hasError = error != null;
        RectTransform statusRow = GenerateUI.Row(parent, hasError ? 96f : 34f);
        TextMeshProUGUI status = GenerateUI.AddMutedText(statusRow, 15f, 0.45f, true);
        TextCompat.SetWrap(status, hasError);
        status.overflowMode = TextOverflowModes.Ellipsis;
        status.verticalAlignment = hasError ? VerticalAlignmentOptions.Top : VerticalAlignmentOptions.Middle;
        if(error != null) {
            status.color = UIColors.SoftRed;
            status.text = error;
            statusRow.AddToolTip(error.Length > 900 ? error[..900] + "…" : error);
        } else if(!handle.Enabled) {
            status.text = MainCore.Tr.Get("MODULES_STATUS_DISABLED", "Disabled");
        } else {
            status.text = "v" + handle.Version;
        }
    }
    private static void Remove(RectTransform row, string id) =>
        RemoveButton(row, () => ModuleService.Remove(id),
            "DESC_MODULES_REMOVE",
            "Deletes this module's files from disk. Its settings are kept, so re-installing restores them.");
    public static void RemoveButton(
        RectTransform row,
        Action confirmed,
        string tipKey,
        string tipText,
        float rightOffset = 8f
    ) {
        bool armed = false;
        UIButton remove = null;
        remove = GenerateUI.Button(row, () => {
            if(remove == null) return;
            if(!armed) {
                armed = true;
                remove.Label.text = MainCore.Tr.Get("MODULES_REMOVE_CONFIRM", "Sure?");
                remove.RestColor = static () => UIColors.SoftRed;
                remove.Background.color = UIColors.SoftRed;
                return;
            }
            confirmed();
        }, "Remove", "modules_remove").SetSecondary();
        Anchor(remove, rightOffset);
        remove.Rect.AddToolTip(tipKey, tipText);
    }
    public static void InstallAllButton(
        RectTransform row,
        Action install,
        string tipKey,
        string tipText,
        float rightOffset = 8f
    ) {
        UIButton button = GenerateUI.Button(row, () => {
            if(ModuleInstallService.Busy) return;
            install();
        }, "Install All", "modules_install_tab");
        Anchor(button, rightOffset);
        button.Rect.AddToolTip(tipKey, tipText);
    }
    private static void Anchor(UIButton button, float rightOffset) {
        RectTransform rect = button.Rect;
        rect.anchorMin = new Vector2(1f, 0.5f);
        rect.anchorMax = new Vector2(1f, 0.5f);
        rect.pivot = new Vector2(1f, 0.5f);
        rect.sizeDelta = new Vector2(140f, 46f);
        rect.anchoredPosition = new Vector2(-rightOffset, 0f);
    }
    public static void Bundled(Transform parent, ModuleManifest manifest) {
        string id = manifest.Id;
        string name = manifest.NameKey == null ? manifest.Name : MainCore.Tr.Get(manifest.NameKey, manifest.Name);
        RectTransform row = GenerateUI.Row(parent, 60f);
        GenerateUI.ButtonRow(row);
        UIButton install = GenerateUI.Button(row, () => ModuleBundle.Install(id), name, "modules_install_" + id);
        install.Rect.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
        install.Rect.AddToolTip(
            "DESC_MODULES_INSTALL_BUNDLED",
            "Installs this module from the copy that shipped with Quartz — no download needed."
        );
        TextMeshProUGUI meta = GenerateUI.AddMutedText(GenerateUI.Row(parent, 28f), 15f, 0.4f, true);
        meta.overflowMode = TextOverflowModes.Ellipsis;
        meta.text = MainCore.Tr.Get("MODULES_BUNDLED", "Included with Quartz") + " · v" + manifest.Version;
    }
    public static void Available(Transform parent, ModuleCatalogEntry entry) {
        string name = entry.NameKey == null ? entry.Name : MainCore.Tr.Get(entry.NameKey, entry.Name);
        bool blocked = entry.CoreAbi != Info.ModuleAbi;
        bool offline = ModuleCatalogService.Source != CatalogSource.Network;
        RectTransform row = GenerateUI.Row(parent, 52f);
        GenerateUI.ButtonRow(row);
        UIButton install = GenerateUI.Button(row, () => {
            if(blocked || ModuleInstallService.Busy) return;
            ModuleInstallService.Install(entry.Id);
        }, name, "modules_install_" + entry.Id);
        install.Rect.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
        install.Rect.AddToolTip(Describe(entry, blocked, offline));
        TextMeshProUGUI meta = GenerateUI.AddMutedText(GenerateUI.Row(parent, 30f), 15f, 0.4f, true);
        meta.overflowMode = TextOverflowModes.Ellipsis;
        meta.text = Describe(entry, blocked, offline);
    }
    public static string Describe(ModuleCatalogEntry entry, bool blocked, bool offline) {
        if(blocked) {
            return MainCore.Tr.Get("MODULES_ABI_BLOCKED", "Built for a different Quartz — update Quartz to install this.");
        }
        if(offline) return MainCore.Tr.Get("MODULES_OFFLINE_INSTALL", "Offline — press Refresh before installing.");
        string desc = entry.DescKey == null ? entry.Desc : MainCore.Tr.Get(entry.DescKey, entry.Desc);
        string size = entry.Size > 0
            ? " · " + (entry.Size / 1024f).ToString("0", CultureInfo.InvariantCulture) + " KB"
            : "";
        string deps = entry.Deps.Length == 0
            ? ""
            : " · " + string.Format(
                CultureInfo.InvariantCulture,
                MainCore.Tr.Get("MODULES_ALSO_INSTALLS", "also installs {0}"),
                string.Join(", ", entry.Deps)
            );
        return string.IsNullOrEmpty(desc) ? "v" + entry.Version + size + deps : desc + " · v" + entry.Version + size + deps;
    }
}

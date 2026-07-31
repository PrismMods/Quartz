using System.Globalization;
using Quartz.Compat.Game;
using Quartz.Core;
using Quartz.Localization;
using Quartz.Modules;
using Quartz.UI.Generator;
using Quartz.UI.Objects.Impl;
using Quartz.UI.Utility;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
namespace Quartz.UI.Factory.Page;
internal static class PageModuleRows {
    public static GameObject[] Installed(Transform parent, ModuleService.Handle handle, ModuleCatalogEntry update) {
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
            update != null ? 316f : 168f
        );
        if(nameKey != null)
            toggle.Label?.GetComponent<TextLocalization>()?.Init(nameKey, handle.Name);
        AppendVersion(toggle.Label, handle.Version);
        if(toggle.Label != null) toggle.Label.rectTransform.offsetMax = new Vector2(-52f, 0f);
        if(update != null) {
            UIButton up = GenerateUI.Button(row, () => {
                if(ModuleInstallService.Busy) return;
                PageModules.MarkPendingUpdate(id);
                ModuleInstallService.Install(id);
            }, "Update", "modules_update");
            Anchor(up, 156f);
            up.Rect.AddToolTip(
                "DESC_MODULES_UPDATE",
                "Downloads the newer version from the catalog. Restart the game afterwards to use it."
            );
        }
        RemoveButton(row, () => ModuleService.Remove(id),
            "DESC_MODULES_REMOVE",
            "Deletes this module's files from disk. Its settings are kept, so re-installing restores them.");
        string error = handle.Error;
        string note = null;
        if(error == null) {
            if(PageModules.WasUpdated(id)) {
                note = MainCore.Tr.Get("MODULES_UPDATED_RESTART", "Updated — restart the game to apply.");
            } else if(update != null) {
                note = string.Format(
                    CultureInfo.InvariantCulture,
                    MainCore.Tr.Get("MODULES_UPDATE_AVAILABLE", "update {0} available"),
                    "v" + ModuleCatalog.TrimV(update.Version)
                );
            }
            if(note == null) return [row.gameObject];
        }
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
        } else {
            status.text = note;
        }
        return [row.gameObject, statusRow.gameObject];
    }
    public static UIButton Armed(Transform parent, Action confirmed, string label, string id) {
        bool armed = false;
        string restLabel = null;
        UIButton button = null;
        button = GenerateUI.Button(parent, () => {
            if(button == null) return;
            if(!armed) {
                armed = true;
                restLabel ??= button.Label.text;
                button.Label.text = MainCore.Tr.Get("MODULES_REMOVE_CONFIRM", "Sure?");
                button.RestColor = static () => UIColors.SoftRed;
                button.Background.color = UIColors.SoftRed;
                return;
            }
            confirmed();
        }, label, id).SetSecondary();
        EventTrigger trigger = button.Rect.gameObject.GetComponent<EventTrigger>();
        if(trigger != null) {
            UnityUtils.AddEvent(EventTriggerType.PointerExit, _ => {
                if(!armed || button == null) return;
                armed = false;
                if(restLabel != null) button.Label.text = restLabel;
                button.RestColor = static () => UIColors.ObjectBG;
                button.UpdateVisual(true);
            }, trigger);
        }
        return button;
    }
    public static void RemoveButton(
        RectTransform row,
        Action confirmed,
        string tipKey,
        string tipText,
        float rightOffset = 8f
    ) {
        UIButton remove = Armed(row, confirmed, "Remove", "modules_remove");
        Anchor(remove, rightOffset);
        remove.Rect.AddToolTip(tipKey, tipText);
    }
    private static void Anchor(UIButton button, float rightOffset) {
        RectTransform rect = button.Rect;
        rect.anchorMin = new Vector2(1f, 0.5f);
        rect.anchorMax = new Vector2(1f, 0.5f);
        rect.pivot = new Vector2(1f, 0.5f);
        rect.sizeDelta = new Vector2(140f, 46f);
        rect.anchoredPosition = new Vector2(-rightOffset, 0f);
    }
    private static TextMeshProUGUI NameLabel(RectTransform row, string name, string nameKey, string version) {
        RectTransform bg = GenerateUI.BackGround(168f);
        bg.SetParent(row, false);
        TextMeshProUGUI label = GenerateUI.AddText(bg);
        label.text = name;
        label.overflowMode = TextOverflowModes.Ellipsis;
        if(nameKey != null) label.gameObject.AddComponent<TextLocalization>().Init(nameKey, name);
        AppendVersion(label, version);
        return label;
    }
    private static void AppendVersion(TextMeshProUGUI label, string version) {
        if(label == null || string.IsNullOrWhiteSpace(version)) return;
        string suffix = "<alpha=#66>  |  v" + ModuleCatalog.TrimV(version);
        TextLocalization loc = label.GetComponent<TextLocalization>();
        if(loc != null && !string.IsNullOrEmpty(loc.Key)) loc.WithSuffix(suffix);
        else label.text += suffix;
    }
    public static GameObject[] Bundled(Transform parent, ModuleManifest manifest) {
        string id = manifest.Id;
        string name = manifest.NameKey == null ? manifest.Name : MainCore.Tr.Get(manifest.NameKey, manifest.Name);
        RectTransform row = GenerateUI.Row(parent, 64f);
        NameLabel(row, name, manifest.NameKey, manifest.Version);
        UIButton install = GenerateUI.Button(row, () => ModuleBundle.Install(id), "Install", "modules_install");
        Anchor(install, 8f);
        install.Rect.AddToolTip(
            "DESC_MODULES_INSTALL_BUNDLED",
            "Installs this module from the copy that shipped with Quartz — no download needed."
        );
        RectTransform metaRow = GenerateUI.Row(parent, 28f);
        TextMeshProUGUI meta = GenerateUI.AddMutedText(metaRow, 15f, 0.4f, true);
        meta.overflowMode = TextOverflowModes.Ellipsis;
        meta.gameObject.AddComponent<TextLocalization>().Init("MODULES_BUNDLED", "Included with Quartz");
        return [row.gameObject, metaRow.gameObject];
    }
    public static GameObject[] Available(Transform parent, ModuleCatalogEntry entry) {
        string name = entry.NameKey == null ? entry.Name : MainCore.Tr.Get(entry.NameKey, entry.Name);
        bool blocked = entry.CoreAbi != Info.ModuleAbi;
        bool offline = ModuleCatalogService.Source != CatalogSource.Network;
        RectTransform row = GenerateUI.Row(parent, 64f);
        NameLabel(row, name, entry.NameKey, entry.Version);
        UIButton install = GenerateUI.Button(row, () => {
            if(blocked || ModuleInstallService.Busy) return;
            ModuleInstallService.Install(entry.Id);
        }, "Install", "modules_install");
        Anchor(install, 8f);
        if(blocked || offline) {
            install.SetSecondary();
            install.Label.color = new Color(1f, 1f, 1f, 0.45f);
        }
        install.Rect.AddToolTip(Describe(entry, blocked, offline));
        RectTransform metaRow = GenerateUI.Row(parent, 30f);
        TextMeshProUGUI meta = GenerateUI.AddMutedText(metaRow, 15f, 0.4f, true);
        meta.overflowMode = TextOverflowModes.Ellipsis;
        meta.text = Describe(entry, blocked, offline);
        return [row.gameObject, metaRow.gameObject];
    }
    public static string Describe(ModuleCatalogEntry entry, bool blocked, bool offline) {
        if(blocked) {
            return MainCore.Tr.Get("MODULES_ABI_BLOCKED", "Built for a different Quartz — update Quartz to install this.");
        }
        if(offline) return MainCore.Tr.Get("MODULES_OFFLINE_INSTALL", "Offline — press Refresh before installing.");
        string desc = entry.DescKey == null ? entry.Desc : MainCore.Tr.Get(entry.DescKey, entry.Desc);
        string size = entry.Size > 0
            ? (entry.Size / 1024f).ToString("0", CultureInfo.InvariantCulture) + " KB"
            : "";
        string deps = entry.Deps.Length == 0
            ? ""
            : string.Format(
                CultureInfo.InvariantCulture,
                MainCore.Tr.Get("MODULES_ALSO_INSTALLS", "also installs {0}"),
                string.Join(", ", entry.Deps)
            );
        List<string> parts = [];
        if(!string.IsNullOrEmpty(desc)) parts.Add(desc);
        if(size.Length > 0) parts.Add(size);
        if(deps.Length > 0) parts.Add(deps);
        return parts.Count == 0 ? "v" + ModuleCatalog.TrimV(entry.Version) : string.Join(" · ", parts);
    }
}

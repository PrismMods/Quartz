using Quartz.Addons;
using Quartz.Async;
using Quartz.Core;
using Quartz.Localization;
using Quartz.UI.Generator;
using Quartz.UI.Objects.Impl;
using UnityEngine;
using UnityFileDialog;

using TMPro;

namespace Quartz.UI.Factory.Page;

// Addons tab. Lists every discovered addon (loaded, disabled or broken) with
// an enable toggle and its status/error, plus reload + open-folder actions.
// Addon-registered pages appear as this category's column-2 children — this
// page only manages the addons themselves.
internal static class PageAddons {
    public static void Create(RectTransform parent) {
        RectTransform content = Quartz.UI.Factory.PageFactory.CreateScrollablePage(parent);

        var headerRow = GenerateUI.Row(content.transform);
        var headerText = GenerateUI.AddTextH1(headerRow);
        headerText.gameObject.AddComponent<TextLocalization>().Init("ADDONS", "Addons");

        var hintRow = GenerateUI.Row(content.transform, 54f);
        var hintText = GenerateUI.AddMutedText(hintRow, 17f, 0.45f, true);
        hintText.gameObject.AddComponent<TextLocalization>().Init(
            "ADDONS_HINT",
            "Build a .qaddon against the QuartzAddon SDK and drop it into UserData/Quartz/Addons (or use Add Addon). Quartz loads addons at launch; Reload re-reads the folder from disk."
        );

        var actionsRow = GenerateUI.Row(content.transform);
        GenerateUI.ButtonRow(actionsRow);

        // Copy a .cs/.qaddon/.dll into the Addons folder via a file picker.
        UIButton addBtn = GenerateUI.Button(
            actionsRow,
            AddAddon,
            "Add Addon",
            "addons_add"
        );
        GenerateUI.FixWidth(addBtn, 200f);
        addBtn.Rect.AddToolTip(
            "DESC_ADDONS_ADD",
            "Pick a .cs, .qaddon or .dll file to copy into the Addons folder, then reload."
        );

        // Reload rebuilds this very panel — defer past the click callback so
        // the button isn't destroyed under its own handler (same pattern as
        // the accent-commit rebuild).
        UIButton reloadBtn = GenerateUI.Button(
            actionsRow,
            () => MainThread.Enqueue(AddonService.Reload),
            "Reload Addons",
            "addons_reload"
        );
        GenerateUI.FixWidth(reloadBtn, 200f);
        reloadBtn.Rect.AddToolTip(
            "DESC_ADDONS_RELOAD",
            "Unloads every addon, re-scans the Addons folder, and reloads and rebuilds this window."
        );

        UIButton folderBtn = GenerateUI.Button(
            actionsRow,
            AddonService.OpenAddonsFolder,
            "Open Folder",
            "addons_open_folder"
        );
        GenerateUI.FixWidth(folderBtn, 200f);
        folderBtn.Rect.AddToolTip(
            "DESC_ADDONS_OPEN_FOLDER",
            "Opens UserData/Quartz/Addons in your file browser."
        );

        if(AddonService.Addons.Count == 0) {
            var emptyRow = GenerateUI.Row(content.transform, 54f);
            var emptyText = GenerateUI.AddMutedText(emptyRow, 17f, 0.45f, true);
            emptyText.gameObject.AddComponent<TextLocalization>().Init(
                "ADDONS_EMPTY",
                "No addons installed yet."
            );
            return;
        }

        foreach(AddonService.Handle handle in AddonService.Addons) {
            AddonService.Handle h = handle;

            // The label is user content (the addon's own name), so no
            // localization lookup — pass an id that resolves to nothing and
            // falls back to the name itself.
            // Wrap in a Row so the toggle pill gets a real height (like every
            // other page's toggles — added bare to the layout group it had no
            // height and rendered as a thin sliver). 64f is a bit taller than
            // the default 50 so an addon row reads as a substantial entry.
            GenerateUI.Toggle(
                GenerateUI.Row(content.transform, 64f),
                true,
                h.Enabled,
                v => AddonService.SetAddonEnabled(h, v),
                h.Name,
                "addon_" + h.UnitId,
                // Near-full-width pill: the Addons page has no value-editor
                // gutter to align with (unlike sliders/inputs), so the default
                // 250px right inset just left the toggle looking stunted. Leave
                // only enough room for the toggle circle at the right edge.
                52f
            );

            // Errors get a taller, wrapping row so a long compiler message
            // reads as intentional instead of a full-width single-line wall;
            // status lines stay compact single-line.
            string error = h.Error;
            bool hasError = error != null;
            var statusRow = GenerateUI.Row(content.transform, hasError ? 96f : 34f);
            TextMeshProUGUI status = GenerateUI.AddMutedText(statusRow, 15f, 0.45f, true);
            status.textWrappingMode = hasError ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            status.overflowMode = TextOverflowModes.Ellipsis;
            status.verticalAlignment = hasError ? VerticalAlignmentOptions.Top : VerticalAlignmentOptions.Middle;

            if(error != null) {
                status.color = UIColors.SoftRed;
                status.text = error;
                // Full (often multi-line) compiler output on hover.
                statusRow.AddToolTip(error.Length > 900 ? error[..900] + "…" : error);
            } else if(!h.Enabled) {
                status.text = MainCore.Tr.Get("ADDONS_STATUS_DISABLED", "Disabled");
            } else if(h.Loaded) {
                string src = Path.GetFileName(h.SourcePath);
                string by = string.IsNullOrEmpty(h.Author) ? "" : $" · {h.Author}";
                status.text = $"v{h.Version}{by} · {src}";
            }

            // Two-step remove: the first click arms the button (red "Sure?"),
            // the second deletes the addon's file + settings from disk and
            // reloads. Same guarded pattern as the profile delete.
            var removeRow = GenerateUI.Row(content.transform, 44f);
            GenerateUI.ButtonRow(removeRow);
            bool armed = false;
            UIButton removeBtn = null;
            removeBtn = GenerateUI.Button(
                removeRow,
                () => {
                    if(removeBtn == null) return;
                    if(!armed) {
                        armed = true;
                        removeBtn.Label.text = MainCore.Tr.Get("ADDONS_REMOVE_CONFIRM", "Sure?");
                        removeBtn.RestColor = static () => UIColors.SoftRed;
                        removeBtn.Background.color = UIColors.SoftRed;
                        return;
                    }
                    AddonService.RemoveAddon(h);
                },
                "Remove",
                "addons_remove"
            ).SetSecondary();
            GenerateUI.FixWidth(removeBtn, 130f);
            removeBtn.Rect.AddToolTip(
                "DESC_ADDONS_REMOVE",
                "Deletes this addon's file and settings from disk. This can't be undone."
            );
        }
    }

    // "Add Addon": pick a file and copy it into the Addons folder. ImportAddon
    // logs the result and reloads (which rebuilds this page with the new entry).
    private static void AddAddon() {
        string path;
        try {
            path = FileBrowser.PickFile(
                null,
                "Quartz Addon",
                AddonService.ImportExtensions,
                GenerateUI.Tr("ADDONS_ADD_TITLE", "Add Quartz Addon")
            );
        } catch(Exception e) {
            MainCore.Log.Err($"[{nameof(PageAddons)}] PickFile failed: {e}");
            return;
        }

        if(string.IsNullOrEmpty(path)) return;
        AddonService.ImportAddon(path);
    }
}

#if !IL2CPP
using Facet;
using Quartz.Async;
using Quartz.Compat.Game;
using Quartz.Core;
using Quartz.IO;
using Quartz.Overlay;
using UnityEngine;
using UnityEngine.UIElements;
namespace Quartz.UI.FacetUi.CoreMenuPages;
/// <summary>Profiles in the new menu, row for row as PageProfiles. The page holds the profile and preset lists, read on build,
/// on attach and after each action, so a refresh pass reads no disk. Every button that writes, opens a dialog or opens Finder
/// goes through QuartzShell.Do.</summary>
internal static class ProfilesPage {
    private sealed class Item {
        public string Name, Key;
        public int N;
        public bool Active;
    }

    internal static void Build(VisualElement p) {
        QuartzShell.Header(p, "PROFILES", null);
        Item[] profiles = [];
        ProfileManager.PresetInfo[] presets = [];
        string pending = "", armed = null;
        StatusLine status = null;
        void Read() {
            string active = ProfileManager.Active;
            profiles = [.. ProfileManager.List().Select((name, i) => new Item { Name = name, N = i + 1, Active = name == active, Key = $"{i}|{name}|{name == active}" })];
            presets = [.. ProfileManager.ListPresets()];
            armed = null;   // a list refresh disarms
        }
        void Reread() {
            Read();
            QuartzShell.Surface?.Refresh();
        }
        void Say(string text) => status!.Show(text);
        void Rebuilt() {
            Reread();
            UICore.Rebuild(true);
            QuartzShell.Changed();
        }
        Read();

        VisualElement card = Controls.Card(p, null);
        Label hint = Ui.Muted(card, "PROFILE_HINT");
        hint.name = "PROFILE_HINT";
        hint.style.whiteSpace = WhiteSpace.Normal;
        Control<string> field = Controls.Text(card, "", Bind.To(() => pending, v => pending = v ?? ""), o => {
            o.Id = "PROFILE_NAME";
            o.Placeholder = "PROFILE_NAME";
            o.MaxLength = 32;
            o.Live = v => pending = v ?? "";
        });
        Button add = Ui.Btn(field.Input.parent, "PROFILE_ADD", () => {
            string name = ProfileManager.Sanitize(pending);
            if(name == null) Say("PROFILE_STATUS_NAME_INVALID");
            else if(ProfileManager.Exists(name)) Say("PROFILE_STATUS_NAME_TAKEN");
            else {
                pending = "";
                (field.Input as TextField)?.SetValueWithoutNotify("");
                status!.Clear();
                QuartzShell.Do("profiles.create:" + name, () => MainThread.Enqueue(() => {
                    if(ProfileManager.Create(name)) Rebuilt();
                    else {
                        Say("PROFILE_STATUS_CREATE_FAILED");
                        Reread();
                    }
                }));
            }
            QuartzShell.Surface?.Refresh();
        });
        add.name = "PROFILE_ADD-input";
        add.style.marginLeft = Theme.For(add).Sizes.Gap;
        Words.Tip(add, "DESC_PROFILE_ADD");

        Controls.Action(card, "", "PROFILE_IMPORT", () => QuartzShell.Do("profiles.import", () => {
            string name = Import();
            if(name == null) return;
            Say(name.Length == 0 ? "PROFILE_STATUS_IMPORT_FAILED"
                : string.Format(QuartzHost.Text("PROFILE_STATUS_IMPORTED", "Imported as '{0}'."), name));
            if(name.Length > 0) Reread();
        }), o => {
            o.Id = "PROFILE_IMPORT";
            o.Tooltip = "DESC_PROFILE_IMPORT";
        });
        Controls.Action(card, "", "PROFILE_OPEN_FOLDER", () => QuartzShell.Do("profiles.folder", () => {
            try {
                Directory.CreateDirectory(ProfileManager.ProfilesPath);
                FileDialog.Reveal(ProfileManager.ProfilesPath);
            } catch(Exception e) {
                MainCore.Log.Err($"[{nameof(ProfilesPage)}] Reveal failed: {e}");
            }
        }), o => o.Id = "PROFILE_OPEN_FOLDER");
        Controls.Action(card, "", "PROFILE_RECALIBRATE", () => QuartzShell.Do("profiles.recalibrate", () => {
            Recalibrate();
            Say("PROFILE_RECALIBRATED");
        }), o => {
            o.Id = "PROFILE_RECALIBRATE";
            o.Tooltip = "DESC_PROFILE_RECALIBRATE";
        });

        VisualElement presetCard = Controls.Card(p, "PRESETS", o => {
            o.Id = "PRESETS";
            o.VisibleWhen = () => presets.Length > 0;
        });
        Controls.Repeat<ProfileManager.PresetInfo>(presetCard, () => presets, preset => preset.Path, (box, preset) => {
            int n = Array.IndexOf(presets, preset) + 1;
            string key = "PRESET_" + preset.Name.ToUpperInvariant();
            Button apply = null;
            apply = Controls.Action(box, QuartzShell.Label(key, preset.Name), "PRESET_APPLY", () => QuartzShell.Do("profiles.preset:" + preset.Name, () => {
                apply!.SetEnabled(false);
                MainThread.Enqueue(() => {
                    if(ProfileManager.ApplyPreset(preset.Path) != null) Rebuilt();
                    else {
                        Say("PROFILE_STATUS_PRESET_FAILED");
                        Reread();
                    }
                });
            }), o => o.Id = "PRESET_APPLY#" + n);
            if(apply.parent?.parent?.Q<Label>() is Label label) label.name = key;
        });

        status = Controls.Status(p);
        status.Root.name = "profiles-status";

        VisualElement list = Controls.Card(p, null);
        // No rows while detached: the search index builds this page detached, and a profile's name is no row of it.
        VisualElement rows = Controls.Repeat<Item>(list, () => list.panel == null ? [] : profiles, item => item.Key, (box, item) => {
            string name = item.Name;
            Button first;
            if(item.Active) first = Controls.Action(box, name, "PROFILE_EXPORT", () => Export(name, Say), o => o.Id = "PROFILE_EXPORT#" + item.N);
            else {
                Button select = null;
                select = Controls.Action(box, name, "PROFILE_SELECT", () => QuartzShell.Do("profiles.select:" + name, () => {
                    select!.SetEnabled(false);
                    MainThread.Enqueue(() => {
                        if(ProfileManager.Apply(name)) Rebuilt();
                        else {
                            select.SetEnabled(true);   // the row keeps its key, so it is not rebuilt: a retry needs the button back
                            Reread();
                        }
                    });
                }), o => o.Id = "PROFILE_SELECT#" + item.N);
                first = select;
                Extra(select, "PROFILE_EXPORT", item.N, () => Export(name, Say));
                Button delete = null;
                bool shownArmed = false;
                delete = Extra(select, "PROFILE_DELETE", item.N, () => {
                    if(armed != name) {
                        armed = name;
                        return;
                    }
                    QuartzShell.Do("profiles.delete:" + name, () => {
                        if(!ProfileManager.Delete(name)) return;   // stays armed, as the classic page does: one more click retries
                        status!.Clear();
                        Reread();
                    });
                });
                Paint.Look(delete, Theme.For(delete).Looks.DangerButton);
                // The armed face follows the page's armed name on every pass, so a list refresh disarms it too.
                Controls.Hook(delete, o => o.VisibleWhen = () => {
                    bool on = armed == name;
                    if(on != shownArmed) {
                        shownArmed = on;
                        Words.Label(delete, on ? "PROFILE_DELETE_CONFIRM" : "PROFILE_DELETE");
                        Paint.Select(delete, on);
                    }
                    return true;
                });
            }
            if(first.parent?.parent?.Q<Label>() is Label label) {
                // The player's own text, not a Lang key: shown as typed, and a relabel leaves it alone.
                Words.Label(label, "");
                label.text = name;
                if(item.Active) Paint.Fg(label, Role.Accent);
            }
        });
        rows.name = "profiles-list";

        // Only while shown: the search index builds this page detached.
        Ui.WhileAttached(card, Reread, () => { });
    }

    // A second button in a row's control cell, Id key#n, beside the row's own.
    private static Button Extra(Button beside, string key, int n, Action click) {
        Button b = Ui.Btn(beside.parent, key, () => {
            click();
            QuartzShell.Surface?.Refresh();
        });
        b.name = key + "#" + n + "-input";
        b.style.marginLeft = Theme.For(b).Sizes.Gap;
        return b;
    }

    // The classic page's import: null when the dialog was cancelled, "" when the file was no profile, else the new name.
    private static string Import() {
        string path = FileDialog.PickFile(null, Info.DisplayName + " Profile", [.. ProfileManager.ImportExtensions, "json"],
            QuartzHost.Text("PROFILE_IMPORT_TITLE", "Import Quartz Profile"));
        if(string.IsNullOrEmpty(path)) return null;
        return ProfileManager.Import(path) ?? "";
    }

    private static void Export(string name, Action<string> say) => QuartzShell.Do("profiles.export:" + name, () => {
        string path = FileDialog.SaveFile(null, $"{name}.{ProfileManager.EXPORT_EXTENSION}", Info.DisplayName + " Profile",
            [ProfileManager.EXPORT_EXTENSION], QuartzHost.Text("PROFILE_EXPORT_TITLE", "Export Quartz Profile"));
        if(string.IsNullOrEmpty(path)) return;
        if(!path.EndsWith($".{ProfileManager.EXPORT_EXTENSION}", StringComparison.OrdinalIgnoreCase)
            && !path.EndsWith($".{ProfileManager.LEGACY_EXTENSION}", StringComparison.OrdinalIgnoreCase)
            && !path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            path += $".{ProfileManager.EXPORT_EXTENSION}";
        say(ProfileManager.Export(name, path)
            ? string.Format(QuartzHost.Text("PROFILE_STATUS_EXPORTED", "Exported '{0}'."), Path.GetFileName(path))
            : "PROFILE_STATUS_EXPORT_FAILED");
    });

    // The classic page's Recalibrate Display: overlays keep their place on this screen, which becomes the baseline.
    private static void Recalibrate() {
        CoreSettings conf = MainCore.Conf;
        OverlayCalibration.EnsureCaptured();
        float fx = conf.CalibWidth > 0f ? Screen.width / conf.CalibWidth : 1f;
        float fy = conf.CalibHeight > 0f ? Screen.height / conf.CalibHeight : 1f;
        OverlayRescale.ApplyAll(fx, fy);
        conf.CalibWidth = Screen.width;
        conf.CalibHeight = Screen.height;
        MainCore.ConfMgr?.RequestSave();
    }
}
#endif

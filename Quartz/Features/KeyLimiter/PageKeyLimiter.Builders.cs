using Quartz.Core;
using Quartz.Features.ChatterBlocker;
using Quartz.Features.Interop;
using Quartz.Features.KeyLimiter;
using Quartz.Resource;
using Quartz.UI.Generator;
using Quartz.UI.Objects.Impl;
using Quartz.UI.Utility;
using UnityEngine;
using Quartz.Utility;
namespace Quartz.UI.Factory.Page;
public static partial class PageKeyLimiter {
    private static void CreateProfileControls(Transform body) {
        int count = KeyLimiter.Profiles.Count;
        int active = KeyLimiter.ActiveProfileIndex;
        int[] indices = new int[count];
        for(int i = 0; i < count; i++) indices[i] = i;
        GenerateUI.DropDown(
            GenerateUI.Row(body),
            0,
            active,
            indices,
            ProfileName,
            v => {
                KeyLimiter.SwitchProfile(v);
                UICore.Rebuild();
            },
            "kl_profile",
            260f,
            "Profile"
        );
        var nameInput = GenerateUI.Input(
            GenerateUI.Row(body),
            "",
            ProfileName(active),
            v => KeyLimiter.RenameActiveProfile(v),
            "Profile Name",
            MainCore.Spr.Get(UISprite.Text128),
            "kl_profile_name"
        );
        nameInput.Rect.AddToolTip(
            "DESC_KL_PROFILE_NAME",
            "Rename the current profile, e.g. \"12 Keys\"."
        );
        GenerateUI.Button(
            GenerateUI.Row(body),
            () => {
                KeyLimiter.AddProfile();
                UICore.Rebuild();
            },
            "Add Profile",
            "kl_add_profile"
        ).SetSecondary();
        UIButton removeBtn = GenerateUI.Button(
            GenerateUI.Row(body),
            () => {
                KeyLimiter.RemoveActiveProfile();
                UICore.Rebuild();
            },
            "Remove Profile",
            "kl_remove_profile"
        ).SetSecondary();
        removeBtn.SetBlocked(count <= 1, true);
    }
    private static string ProfileName(int index) {
        var profiles = KeyLimiter.Profiles;
        if(index < 0 || index >= profiles.Count) return "Profile " + (index + 1);
        string name = profiles[index].Name;
        return string.IsNullOrEmpty(name) ? "Profile " + (index + 1) : name;
    }
    private static void CreateKeyLimiter(Transform content) {
        KeyLimiter.EnsureConf();
        KeyLimiterSettings conf = KeyLimiter.Conf;
        KeyLimiterSettings def = new();
        var sec = GenerateUI.FlatSection(
            content, "Key Limiter",
            v => {
                conf.Enabled = v;
                KeyLimiter.Save();
            },
            conf.Enabled,
            "Enable Key Limiter", "keylimiter_enable", def.Enabled
        );
        UIToggle syncToggle = GenerateUI.Toggle(
            GenerateUI.Row(sec.Body),
            Quartz.Game.KeyBindSync.Default,
            Quartz.Game.KeyBindSync.IsSyncing,
            v => Quartz.Game.KeyBindSync.SetSyncing(v),
            "Sync Keys to Key Limiter",
            "keyviewer_synclimiter"
        );
        syncToggle.Rect.AddToolTip(
            "DESC_KEYVIEWER_SYNCLIMITER",
            "Overwrites the Key Limiter's allowed keys with the keys shown here, and keeps them matched when you rebind keys or switch styles."
        );
        CreateProfileControls(sec.Body);
        UIButton captureBtn = null;
        captureBtn = GenerateUI.Button(
            GenerateUI.Row(sec.Body),
            () => {
                if(KeyLimiter.IsCapturing) {
                    KeyLimiter.CancelCapture();
                    return;
                }
                if(captureBtn?.Label != null) captureBtn.Label.text = MainCore.Tr.Get("PRESS_A_KEY", "Press a key...");
                KeyLimiter.StartCapture(
                    key => KeyLimiter.ToggleAllowedKey(key),
                    () => {
                        if(captureBtn?.Label != null) captureBtn.Label.text = MainCore.Tr.Get("KL_CAPTURE", "Add / Remove Key");
                    }
                );
            },
            "Add / Remove Key",
            "kl_capture"
        );
        captureBtn.Rect.AddToolTip(
            "DESC_KL_CAPTURE",
            "Press any key to add/remove it from the allowed list. Escape cancels."
        );
        UIButton clearBtn = GenerateUI.Button(
            GenerateUI.Row(sec.Body),
            () => KeyLimiter.ClearAllowedKeys(),
            "Clear All",
            "kl_clear"
        ).SetSecondary();
        var syncNote = GenerateUI.AddLocalizedMutedText(
            GenerateUI.Row(sec.Body, 30f),
            "KL_SYNC_LOCKED",
            "Keys are managed by the Key Viewer (Sync Keys to Key Limiter is on)."
        );
        GameObject list = new("AllowedKeysList");
        list.transform.SetParent(sec.Body, false);
        list.AddComponent<RectTransform>();
        GenerateUI.FitVertical(list, 6f);
        void RebuildKeysList() {
            if(list == null) return;
            GenerateUI.ClearChildren(list.transform);
            int[] keys = KeyLimiter.Conf?.AllowedKeys ?? [];
            if(keys.Length == 0) {
                GenerateUI.AddLocalizedMutedText(
                    GenerateUI.Row(list.transform), "KL_NO_ALLOWED_KEYS", "No allowed keys.", 19f);
                return;
            }
            bool locked = Quartz.Game.KeyBindSync.IsSyncing;
            GenerateUI.Localize(GenerateUI.AddTextH1(GenerateUI.Row(list.transform)), "KL_ALLOWED_KEYS", "Allowed Keys");
            for(int i = 0; i < keys.Length; i++) {
                CreateKeyRow(list.transform, KeyCodes.Normalize((KeyCode)keys[i]), locked);
            }
        }
        void ApplySyncLock() {
            bool locked = Quartz.Game.KeyBindSync.IsSyncing;
            if(locked && KeyLimiter.IsCapturing) KeyLimiter.CancelCapture();
            syncToggle.Set(locked, false);
            captureBtn.SetBlocked(locked, true);
            clearBtn.SetBlocked(locked, true);
            syncNote.gameObject.SetActive(locked);
            RebuildKeysList();
        }
        if(keysChangedHandler != null) KeyLimiter.Changed -= keysChangedHandler;
        keysChangedHandler = RebuildKeysList;
        KeyLimiter.Changed += keysChangedHandler;
        if(syncLockChangedHandler != null) Quartz.Game.KeyBindSync.Changed -= syncLockChangedHandler;
        syncLockChangedHandler = ApplySyncLock;
        Quartz.Game.KeyBindSync.Changed += syncLockChangedHandler;
        ApplySyncLock();
        CreateChartKeyLimiter(sec.Body);
    }
    private static KeyCode setCaptureKey = KeyCode.None;
    private static void CreateKeyRow(Transform parent, KeyCode key, bool locked) {
        RectTransform row = GenerateUI.Row(parent);
        RectTransform bg = GenerateUI.BackGround();
        bg.SetParent(row, false);
        var label = GenerateUI.AddText(bg);
        label.text = Keybind.KeyName(key);
        if(locked) return;
        bool settingThis = setCaptureKey == key && KeyLimiter.IsCapturing;
        GenerateUI.MiniButton(bg, settingThis ? "..." : "Set", settingThis ? null : "SET", -106f, 90f, () => {
            if(KeyLimiter.IsCapturing) {
                KeyLimiter.CancelCapture();
                return;
            }
            setCaptureKey = key;
            KeyLimiter.StartCapture(
                newKey => KeyLimiter.ReplaceAllowedKey(key, newKey),
                () => setCaptureKey = KeyCode.None
            );
        });
        GenerateUI.MiniButton(bg, "Remove", "REMOVE", -8f, 90f, () => KeyLimiter.ToggleAllowedKey(key));
    }
    private static void CreateChatterBlocker(Transform content) {
        ChatterBlocker.EnsureConf();
        ChatterBlockerSettings conf = ChatterBlocker.Conf;
        ChatterBlockerSettings def = new();
        var sec = GenerateUI.FlatSection(
            content, "Keyboard Chatter Blocker",
            v => {
                conf.Enabled = v;
                ChatterBlocker.Save();
            },
            conf.Enabled,
            "Enable Keyboard Chatter Blocker", "chatterblocker_enable", def.Enabled
        );
        UISlider threshold = GenerateUI.Slider(
            GenerateUI.Row(sec.Body),
            def.ThresholdMs, 0f, 100f, conf.ThresholdMs,
            v => Mathf.Round(v), null, null,
            "Threshold (ms)",
            "kcb_ms"
        );
        threshold.Format = "0 ms";
        threshold.OnChanged = v => conf.ThresholdMs = v;
        threshold.OnComplete = v => {
            conf.ThresholdMs = v;
            ChatterBlocker.Save();
        };
    }
}

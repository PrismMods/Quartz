using Quartz.Core;
using Quartz.Features.SongTitle;
using Quartz.Resource;
using Quartz.UI.Generator;
using Quartz.UI.Objects.Impl;
using TMPro;
using UnityEngine;

namespace Quartz.UI.Factory.Page;

// Song Title settings section for the Overlay tab. Replaces the game's own
// in-game title/artist HUD with a customizable {artist}/{title} template.
internal static class PageSongTitle {
    public static void AppendTo(Transform content) {
        SongTitleOverlay.EnsureConf();
        SongTitleSettings conf = SongTitleOverlay.Conf;
        SongTitleSettings def = new();

        void Save() => SongTitleOverlay.Save();
        void Apply() => SongTitleOverlay.Apply();
        void ApplyShadow() => SongTitleOverlay.ApplyShadow();

        void SetHeaderEnabled(bool enabled, GenerateUI.CollapsibleSection section) {
            if(section.HeaderObj.transform.Find("Bar/Label") is Transform labelTr
                && labelTr.TryGetComponent(out TextMeshProUGUI label))
                label.alpha = enabled ? 1f : 0.5f;
        }

        GenerateUI.CollapsibleSection sec = null;
        sec = GenerateUI.Collapsible(
            content, "Song Title", startExpanded: false,
            v => { conf.Enabled = v; Apply(); Save(); SetHeaderEnabled(v, sec); },
            conf.Enabled
        );
        SetHeaderEnabled(conf.Enabled, sec);

        UIInput fmt = GenerateUI.Input(
            GenerateUI.Row(sec.Body),
            def.Format,
            conf.Format,
            v => { conf.Format = v; Save(); },
            "Format — use {artist} and {title}",
            MainCore.Spr.Get(UISprite.Text128),
            "songtitle_format"
        );
        fmt.InputField.characterLimit = 80;

        GenerateUI.SnapSlider(sec.Body, "Font Size", "songtitle_fontsize",
            def.FontSize, 12f, 120f, conf.FontSize, "0 px", 1f,
            v => conf.FontSize = v, Apply, Save);

        GenerateUI.SnapSlider(sec.Body, "Master Size", "songtitle_master",
            def.MasterSize, 0.25f, 3f, conf.MasterSize, "0.00 x", 0.01f,
            v => conf.MasterSize = v, Apply, Save);

        GenerateUI.ColorPicker(
            GenerateUI.Row(sec.Body),
            def.GetColor(),
            conf.GetColor(),
            c => { conf.SetColor(c); Apply(); },
            c => { conf.SetColor(c); Apply(); Save(); },
            "Text Color",
            "songtitle_color"
        );

        GenerateUI.Toggle(
            GenerateUI.Row(sec.Body),
            def.ShadowEnabled,
            conf.ShadowEnabled,
            v => { conf.ShadowEnabled = v; ApplyShadow(); Save(); },
            "Shadow",
            "songtitle_shadow"
        );

        GenerateUI.SnapSlider(sec.Body, "Shadow X", "songtitle_shadow_x",
            def.ShadowX, -10f, 10f, conf.ShadowX, "0.0 px", 0.1f,
            v => conf.ShadowX = v, ApplyShadow, Save);

        GenerateUI.SnapSlider(sec.Body, "Shadow Y", "songtitle_shadow_y",
            def.ShadowY, -10f, 10f, conf.ShadowY, "0.0 px", 0.1f,
            v => conf.ShadowY = v, ApplyShadow, Save);

        GenerateUI.SnapSlider(sec.Body, "Shadow Softness", "songtitle_shadow_soft",
            def.ShadowSoftness, 0f, 20f, conf.ShadowSoftness, "0.0 px", 0.1f,
            v => conf.ShadowSoftness = v, ApplyShadow, Save);

        GenerateUI.ColorPicker(
            GenerateUI.Row(sec.Body),
            def.GetShadowColor(),
            conf.GetShadowColor(),
            c => { conf.SetShadowColor(c); ApplyShadow(); },
            c => { conf.SetShadowColor(c); ApplyShadow(); Save(); },
            "Shadow Color",
            "songtitle_shadow_color"
        );
    }
}

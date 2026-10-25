using Quartz.UI.Nav;
namespace Quartz.Features.Editor;
[Feature("editor", "editor", 10, "Editor Tools", "EDITOR")]
public sealed class EditorModule : QuartzFeature {
    public override void OnLoad() {
        foreach(string lang in new[] { "en-US", "ko-KR", "zh-CN" })
            Context.RegisterTranslations(typeof(EditorModule), $"Quartz.Features.Editor.Lang.{lang}.json");
        EditorFeature.EnsureConf();
        Page("editor.tilereadout", 10, "Selected Tile Readout", "SECTION_SELECTED_TILE_READOUT", PageEditor.TileReadoutPage);
        Page("editor.bga", 20, "BGA Mod", "SECTION_BGA_MOD", PageEditor.BgaPage);
        Page("editor.fliprotate", 30, "Flip & Rotate Tiles", "SECTION_FLIP_ROTATE_TILES", PageEditor.FlipRotatePage);
        Page("editor.decopreview", 40, "Decoration Preview", "SECTION_DECORATION_PREVIEW", PageEditor.DecoPreviewPage);
        Context.PatchAll();
        Context.OnModDisable("EditorFeature", EditorFeature.Restore);
    }
    public override void OnTick() => EditorFeature.Ticker.Tick();
    public override void OnUnload() => EditorFeature.Restore();
    private void Page(string key, int order, string title, string localeKey, Action<UnityEngine.RectTransform> build) =>
        Context.AddPage(new NavPage {
            Key = key,
            CategoryKey = "editor",
            Order = order,
            Title = title,
            LocaleKey = localeKey,
            OwnScroll = true,
            Build = build,
        });
}

using Quartz.UI.Nav;
namespace Quartz.Features.Editor;
[Feature("editor", "editor", 10, "Editor Tools", "EDITOR")]
public sealed class EditorModule : QuartzFeature {
    public override void OnLoad() {
        foreach(string lang in new[] { "en-US", "ko-KR", "zh-CN" })
            Context.RegisterTranslations(typeof(EditorModule), $"Quartz.Features.Editor.Lang.{lang}.json");
        EditorFeature.EnsureConf();
        Page("editor.tilereadout", 10, "Selected Tile Readout", "SECTION_SELECTED_TILE_READOUT");
        Page("editor.bga", 20, "BGA Mod", "SECTION_BGA_MOD");
        Page("editor.fliprotate", 30, "Flip & Rotate Tiles", "SECTION_FLIP_ROTATE_TILES");
        Page("editor.decopreview", 40, "Decoration Preview", "SECTION_DECORATION_PREVIEW");
        Context.PatchAll();
        Context.OnModDisable("EditorFeature", EditorFeature.Restore);
    }
    public override void OnTick() => EditorFeature.Ticker.Tick();
    public override void OnUnload() => EditorFeature.Restore();
#if QUARTZ_CLASSIC_UI
    private static Action<UnityEngine.RectTransform> ClassicBuilder(string key) => key switch {
        "editor.tilereadout" => PageEditor.TileReadoutPage,
        "editor.bga" => PageEditor.BgaPage,
        "editor.fliprotate" => PageEditor.FlipRotatePage,
        "editor.decopreview" => PageEditor.DecoPreviewPage,
        _ => null,
    };
#endif
    private void Page(string key, int order, string title, string localeKey) =>
        Context.AddPage(new NavPage {
            Key = key,
            CategoryKey = "editor",
            Order = order,
            Title = title,
            LocaleKey = localeKey,
            OwnScroll = true,
#if QUARTZ_CLASSIC_UI
            Build = ClassicBuilder(key),
#endif
        });
}

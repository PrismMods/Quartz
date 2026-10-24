using Quartz.UI.Factory.Page;
using Quartz.UI.Nav;
namespace Quartz.Features.UiHider;
[Feature("uihider", "visuals", 60, "UI Hiding", "SECTION_UI_HIDING")]
public sealed class UiHiderModule : QuartzFeature {
    public override void OnLoad() {
        foreach(string lang in new[] { "en-US", "ko-KR", "zh-CN" })
            Context.RegisterTranslations(typeof(UiHiderModule), $"Quartz.Features.UiHider.Lang.{lang}.json");
        UiHider.EnsureConf();
        Context.AddPage(new NavPage {
            Key = "visuals.uihiding",
            CategoryKey = "visuals",
            Order = 60,
            Title = "UI Hiding",
            LocaleKey = "SECTION_UI_HIDING",
            Build = PageUiHider.Create,
            OwnScroll = true,
        });
        Context.RegisterImportHandler(new UiHiderImport());
        Context.PatchAll();
        Context.OnModDisable("UiHider", UiHider.Restore);
    }
    public override void OnUnload() => UiHider.Restore();
}

using Quartz.UI.Factory.Page;
using Quartz.UI.Nav;
namespace Quartz.Features.OttoIcon;
[Feature("ottoicon", "visuals", 50, "Otto Icon", "SECTION_OTTO_ICON")]
public sealed class OttoIconModule : QuartzFeature {
    public override void OnLoad() {
        foreach(string lang in new[] { "en-US", "ko-KR", "zh-CN" })
            Context.RegisterTranslations(typeof(OttoIconModule), $"Quartz.Features.OttoIcon.Lang.{lang}.json");
        OttoIcon.EnsureConf();
        Context.AddPage(new NavPage {
            Key = "visuals.ottoicon",
            CategoryKey = "visuals",
            Order = 50,
            Title = "Otto Icon",
            LocaleKey = "SECTION_OTTO_ICON",
            Build = PageOttoIcon.Create,
            OwnScroll = true,
        });
        Context.RegisterImportHandler(new OttoIconImport());
        Context.PatchAll();
        Context.OnModEnable("OttoIcon", OttoIcon.Refresh);
        Context.OnModDisable("OttoIcon", OttoIcon.Restore);
    }
    public override void OnUnload() {
        OttoIcon.Restore();
        OttoIcon.DisposeCustomImage();
    }
}

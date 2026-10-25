using Quartz.UI.Nav;
namespace Quartz.Features.AntiChatter;
[Feature("antichatter", "gameplay", 15, "AntiChatter", "SECTION_ANTICHATTER")]
public sealed class AntiChatterModule : QuartzFeature {
    public override void OnLoad() {
        foreach(string lang in new[] { "en-US", "ko-KR", "zh-CN" })
            Context.RegisterTranslations(typeof(AntiChatterModule), $"Quartz.Features.AntiChatter.Lang.{lang}.json");
        AntiChatter.EnsureConf();
        FeatureMasters.Claim(AntiChatter.ConfMgr, AntiChatter.Conf.Enabled, static c => c.Enabled = true);
        Context.AddPage(new NavPage {
            Key = "gameplay.antichatter",
            CategoryKey = "gameplay",
            Order = 15,
            Title = "AntiChatter",
            LocaleKey = "SECTION_ANTICHATTER",
            Build = PageAntiChatter.Create,
            OwnScroll = true,
        });
        Context.RegisterImportHandler(new AntiChatterImport());
        Context.PatchAll();
    }
    public override void OnDisable() => AntiChatter.ClearAsyncState();
}

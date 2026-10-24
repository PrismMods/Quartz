using Quartz.UI.Factory.Page;
using Quartz.UI.Nav;
namespace Quartz.Features.Practice;
[Feature("practice", "gameplay", 60, "Practice Difficulty", "SECTION_PRACTICE_DIFFICULTY", Deps = new[] { "overlay" })]
public sealed class PracticeModule : QuartzFeature {
    public override void OnLoad() {
        foreach(string lang in new[] { "en-US", "ko-KR", "zh-CN" })
            Context.RegisterTranslations(typeof(PracticeModule), $"Quartz.Features.Practice.Lang.{lang}.json");
        PracticeDifficulty.EnsureConf();
        Context.AddPage(new NavPage {
            Key = "gameplay.practice",
            CategoryKey = "gameplay",
            Order = 60,
            Title = "Practice Difficulty",
            LocaleKey = "SECTION_PRACTICE_DIFFICULTY",
            Build = PagePractice.Create,
            OwnScroll = true,
        });
        Context.PatchAll();
        Context.OnModEnable("PracticeOverlay", () => PracticeOverlay.Initialize(Quartz.Core.MainCore.Root));
        Context.OnModDisable("PracticeOverlay", PracticeOverlay.Dispose);
    }
    public override void OnUnload() => PracticeOverlay.Dispose();
    public override void OnTick() => PracticeOverlay.Gate();
}

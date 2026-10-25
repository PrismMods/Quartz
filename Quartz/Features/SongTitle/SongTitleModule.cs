using Quartz.UI.Nav;
namespace Quartz.Features.SongTitle;
[Feature("songtitle", "overlay", 60, "Song Title", "SECTION_SONG_TITLE", Deps = new[] { "overlay" })]
public sealed class SongTitleModule : QuartzFeature {
    public override void OnLoad() {
        foreach(string lang in new[] { "en-US", "ko-KR", "zh-CN" })
            Context.RegisterTranslations(typeof(SongTitleModule), $"Quartz.Features.SongTitle.Lang.{lang}.json");
        SongTitleOverlay.EnsureConf();
        FeatureMasters.Claim(SongTitleOverlay.ConfMgr, SongTitleOverlay.Conf.Enabled, static c => c.Enabled = true);
        Context.RegisterRescalable("songtitle", SongTitleOverlay.Rescale);
        Context.AddPage(new NavPage {
            Key = "overlay.songtitle",
            CategoryKey = "overlay",
            Order = 60,
            Title = "Song Title",
            LocaleKey = "SECTION_SONG_TITLE",
            Build = PageSongTitle.Create,
            OwnScroll = true,
        });
        Context.PatchAll();
        Context.OnModEnable("SongTitleOverlay", () => SongTitleOverlay.Initialize(Quartz.Core.MainCore.Root));
        Context.OnModDisable("SongTitleOverlay", SongTitleOverlay.Dispose);
    }
    public override void OnUnload() => SongTitleOverlay.Dispose();
    public override void OnTick() => SongTitleOverlay.Gate();
}

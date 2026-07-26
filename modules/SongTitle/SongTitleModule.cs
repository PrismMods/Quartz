using Quartz.Modules;
using Quartz.UI.Factory.Page;
using Quartz.UI.Nav;
namespace Quartz.Features.SongTitle;
public sealed class SongTitleModule : QuartzModule {
    public override void OnLoad() {
        foreach(string lang in new[] { "en-US", "ko-KR", "zh-CN" })
            Context.RegisterTranslations(typeof(SongTitleModule), $"Quartz.Features.SongTitle.Lang.{lang}.json");
        SongTitleOverlay.EnsureConf();
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
        Context.PatchAll(typeof(SongTitleModule));
        Context.OnModEnable("SongTitleOverlay", () => SongTitleOverlay.Initialize(Quartz.Core.MainCore.Root));
        Context.OnModDisable("SongTitleOverlay", SongTitleOverlay.Dispose);
    }
    public override void OnUnload() => SongTitleOverlay.Dispose();
}

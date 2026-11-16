using Quartz.UI.Nav;
namespace Quartz.Features.Tuf;
[Feature("tuf", "others", 30, "TUF", "TUF", Tab = "tuf")]
public sealed class TufModule : QuartzFeature {
    public const string LevelsPageKey = "tuf.levels";
    private TufService tufService;
    private TufPackService packService;
    public override void OnLoad() {
        foreach(string lang in new[] { "en-US", "ko-KR", "zh-CN" })
            Context.RegisterTranslations(typeof(TufModule), $"Quartz.Features.Tuf.Lang.{lang}.json");
        tufService = new TufService();
        packService = new TufPackService();
        tufService.Initialize();
        packService.Initialize();
        Context.AddPage(new NavPage {
            Key = LevelsPageKey,
            CategoryKey = "tuf",
            Order = 10,
            Title = "Levels",
            LocaleKey = "TUF_LEVELS",
#if QUARTZ_CLASSIC_UI
            Build = TufBrowserUI.Create,
#endif
            SearchLabel = static () => Quartz.Core.MainCore.Tr.Get("TUF", "TUF"),
            OwnScroll = true,
        });
        Context.AddPage(new NavPage {
            Key = "tuf.packs",
            CategoryKey = "tuf",
            Order = 20,
            Title = "Packs",
            LocaleKey = "TUF_PACKS",
#if QUARTZ_CLASSIC_UI
            Build = TufPacksUI.Create,
#endif
            SearchLabel = static () => Quartz.Core.MainCore.Tr.Get("TUF", "TUF") + " · " + Quartz.Core.MainCore.Tr.Get("TUF_PACKS", "Packs"),
            OwnScroll = true,
        });
        Context.AddPage(new NavPage {
            Key = "tuf.settings",
            CategoryKey = "tuf",
            Order = 30,
            Title = "Settings",
            LocaleKey = "TUF_SETTINGS",
#if QUARTZ_CLASSIC_UI
            Build = TufSettingsUI.Create,
#endif
            SearchLabel = static () => Quartz.Core.MainCore.Tr.Get("TUF", "TUF") + " · " + Quartz.Core.MainCore.Tr.Get("TUF_SETTINGS", "Settings"),
            OwnScroll = true,
        });
        Context.AddHomeCard(new Quartz.UI.Home.HomeCard {
            Key = "tuf.home",
            Title = "TUF",
            LocaleKey = "TUF",
            Order = 30,
            Lines = () => {
                TufService service = TufService.Instance;
                if(service == null) return [];
                return [
                    string.Format(
                        System.Globalization.CultureInfo.InvariantCulture,
                        Quartz.Core.MainCore.Tr.Get("TUF_HOME_INSTALLED", "{0} levels installed"),
                        service.InstalledCount),
                    service.ActiveRootPath,
                ];
            },
        });
        Context.PatchAll();
    }
    public override void OnUnload() {
        packService?.Dispose();
        tufService?.Dispose();
        packService = null;
        tufService = null;
    }
}

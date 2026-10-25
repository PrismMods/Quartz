using Quartz.UI.Nav;
namespace Quartz.Features.KeyLimiter;
[Feature("keylimiter", "gameplay", 10, "Key Limiter", "SECTION_KEY_LIMITER")]
public sealed class KeyLimiterModule : QuartzFeature {
    public override void OnLoad() {
        foreach(string lang in new[] { "en-US", "ko-KR", "zh-CN" })
            Context.RegisterTranslations(typeof(KeyLimiterModule), $"Quartz.Features.KeyLimiter.Lang.{lang}.json");
        KeyLimiter.EnsureConf();
        FeatureMasters.Claim(KeyLimiter.ConfMgr, KeyLimiter.Conf.Enabled, static c => c.Enabled = true);
        ChartKeyLimiter.EnsureConf();
        EnumToStringPatch.Bind(Context.Harmony);
        Quartz.Resource.ResourceManager assets = Context.Resources(typeof(KeyLimiterModule), "Quartz.Features.KeyLimiter.Assets.");
        byte[] icon = assets.Load("KeyLimiterEvent.png");
        if(icon != null) Context.RegisterSprite(ChartKeyLimiter.SpriteKey, icon);
        Context.OnModDisable("chart key limiter", ChartKeyLimiter.Unregister);
        Context.OnModEnable("chart key limiter", ChartKeyLimiter.Apply);
        Context.AddPage(new NavPage {
            Key = "gameplay.keylimiter",
            CategoryKey = "gameplay",
            Order = 10,
            Title = "Key Limiter",
            LocaleKey = "SECTION_KEY_LIMITER",
            Build = PageKeyLimiter.KeyLimiterPage,
            OwnScroll = true,
        });
        Context.AddHomeCard(new Quartz.UI.Home.HomeCard {
            Key = "keylimiter.home",
            Title = "Key Limiter",
            LocaleKey = "SECTION_KEY_LIMITER",
            Order = 20,
            Build = body => {
                KeyLimiter.EnsureConf();
                KeyLimiterSettings conf = KeyLimiter.Conf;
                if(conf == null) return;
                Quartz.UI.Home.HomeUI.Line(body, string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    Quartz.Core.MainCore.Tr.Get("KL_HOME_KEYS", "{0}: {1} keys · {2}"),
                    conf.ActiveProfileOrDefault().Name,
                    conf.AllowedKeys.Length,
                    conf.Enabled
                        ? Quartz.Core.MainCore.Tr.Get("KL_HOME_ON", "limiting")
                        : Quartz.Core.MainCore.Tr.Get("KL_HOME_OFF", "off")));
            },
        });
        Context.RegisterImportHandler(new KeyLimiterImport());
        Context.PatchAll();
        ChartKeyLimiter.Apply();
    }
}

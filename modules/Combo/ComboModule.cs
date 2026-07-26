using Quartz.Modules;
using Quartz.UI.Factory.Page;
using Quartz.UI.Nav;
namespace Quartz.Features.Combo;
public sealed class ComboModule : QuartzModule {
    public override void OnLoad() {
        foreach(string lang in new[] { "en-US", "ko-KR", "zh-CN" })
            Context.RegisterTranslations(typeof(ComboModule), $"Quartz.Features.Combo.Lang.{lang}.json");
        ComboOverlay.EnsureConf();
        Context.RegisterRescalable("combo", ComboOverlay.Rescale);
        Context.AddPage(new NavPage {
            Key = "overlay.combo", CategoryKey = "overlay", Order = 40,
            Title = "Combo", LocaleKey = "SECTION_COMBO", Build = PageCombo.Create,
            OwnScroll = true,
        });
        Context.RegisterImportHandler(new ComboImport());
        Context.PatchAll(typeof(ComboModule));
        Context.OnModEnable("ComboOverlay", () => ComboOverlay.Initialize(Quartz.Core.MainCore.Root));
        Context.OnModDisable("ComboOverlay", ComboOverlay.Dispose);
    }
    public override void OnUnload() => ComboOverlay.Dispose();
}

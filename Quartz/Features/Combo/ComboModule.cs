using Quartz.UI.Factory.Page;
using Quartz.UI.Nav;
namespace Quartz.Features.Combo;
[Feature("combo", "overlay", 40, "Combo", "SECTION_COMBO", Deps = new[] { "overlay" })]
public sealed class ComboModule : QuartzFeature {
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
        Context.PatchAll();
        Context.OnModEnable("ComboOverlay", () => ComboOverlay.Initialize(Quartz.Core.MainCore.Root));
        Context.OnModDisable("ComboOverlay", ComboOverlay.Dispose);
    }
    public override void OnUnload() => ComboOverlay.Dispose();
    public override void OnTick() => ComboOverlay.Gate();
}

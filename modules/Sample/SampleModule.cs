using Quartz.Modules;
using Quartz.UI.Generator;
using Quartz.UI.Nav;
using UnityEngine;
namespace Quartz.Features.Sample;
public sealed class SampleModule : QuartzModule {
    private int enables;
    public override void OnLoad() {
        Context.AddPage(new NavPage {
            Key = "tools.sample",
            CategoryKey = "addons",
            Order = 900,
            Title = "Sample",
            LocaleKey = "MODULE_SAMPLE",
            Build = Build,
            OwnScroll = true,
        });
        Context.RegisterStat("sample-enables", "Sample Enables", "Modules",
            () => enables.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Context.OnModEnable("Sample", () => enables++);
    }
    private void Build(RectTransform content) {
        GenerateUI.Localize(GenerateUI.AddTextH1(GenerateUI.Row(content.transform)), "MODULE_SAMPLE", "Sample");
        GenerateUI.AddMutedText(GenerateUI.Row(content.transform, 40f), 17f, 0.5f, true).text =
            "Loaded from " + Context.Manifest.Id + " v" + Context.Manifest.Version + ".";
    }
}

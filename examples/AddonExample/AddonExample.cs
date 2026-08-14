// The worked example for every Quartz addon API. Build it (dotnet build -c Release),
// drop bin/Release/AddonExample.qaddon into UserData/Quartz/Addons, press Reload Addons.
using System;
using System.Collections.Generic;
using Quartz.Addons;
using UnityEngine;

public enum ExampleMode { Plain, Fancy }

// Every public field/property becomes a control on the auto-generated settings tab:
// bool → toggle, enum/[Choices] → dropdown, [Slider] number → slider, plain number/string
// → input, Color → color picker, Dictionary<string,string> → editable key/value rows.
public class ExampleSettings {
    [Section("General")]
    [Desc("Master switch for the example.")]
    public bool Enabled = true;

    [Slider(0f, 100f, 1f)]
    [Desc("A slider between 0 and 100, stepping by 1.")]
    public float Threshold = 25f;

    [Name("Greeting Text")]
    public string Greeting = "hello from AddonExample";

    [Choices("Left", "Center", "Right")]
    public string Alignment = "Center";

    public ExampleMode Mode = ExampleMode.Fancy;

    public Color Accent = new Color(0.5f, 0.7f, 1f, 1f);

    [Section("Advanced")]
    [ReloadRequired]
    [Desc("Saving this option reloads all addons.")]
    public bool DeepHooks;

    [Desc("Free-form key/value pairs.")]
    public Dictionary<string, string> Macros = new Dictionary<string, string>();

    [Ignore]
    public int NotShownInUi;
}

public class AddonExample : QuartzAddon {
    public override string Name => "Addon Example";
    public override string Version => "1.0.0";
    public override string Author => "PrismMods";
    // "owner/repo" — Quartz checks GitHub releases and shows an update line on the Addons page.
    public override string Repo => "";
    // Hard dependencies by addon id: missing/disabled ones show an actionable error instead of loading.
    public override string[] Requires => Array.Empty<string>();
    // Soft ordering: load after these ids when they are present.
    public override string[] LoadAfter => Array.Empty<string>();

    private ExampleSettings conf;
    private int hits;

    public override void OnLoad() {
        conf = Context.GetSettings<ExampleSettings>();

        // Auto-generated settings UI from ExampleSettings above (one line, no UI code).
        Context.RegisterSettingsTab("Example");

        // Which loader is hosting Quartz right now.
        Context.Msg("running under " + AddonContext.Loader);

        // A per-addon data folder: UserData/Quartz/Addons/AddonExample
        Context.Msg("data folder: " + Context.DataPath);

        // Translations for the auto-derived keys (ADDON_<id>_<member> for settings labels).
        Context.RegisterTranslations(@"{
            ""ko-KR"": { ""0KTL"": ""DO_NOT_TRANSLATE_THIS_KEY!"", ""ADDON_ADDONEXAMPLE_GREETING"": ""인사말"" }
        }");

        // Buttons rendered under this addon on the Addons page.
        Context.RegisterAction("Say Hi", () => Context.Msg(conf.Greeting));
        Context.RegisterAction("Reset Hits", () => hits = 0);

        // Overlay stat + text tag, same registries the mod's own features use.
        Context.RegisterStat("example_hits", "Example Hits", "Addons", () => hits.ToString());
        Context.RegisterTag("exampleHits", () => hits.ToString());

        // Game events.
        AddonEvents.LevelStart += () => Context.Msg("level started");
        AddonEvents.Hit += margin => hits++;

        // Harmony is available too: Context.PatchAll(typeof(AddonExample));

        // Calling another addon, no reference needed:
        // object reply = AddonContext.CallAddon("SomeOtherAddon", "ping", 123);
        // object api = AddonContext.GetAddonApi("SomeOtherAddon");
    }

    // Other addons can reach this addon the same two ways:
    public override object OnCall(object[] args)
        => args != null && args.Length > 0 && "hits".Equals(args[0]) ? hits : null;
    public override object GetApi() => null;

    public override void OnTick() { }
    public override void OnUnload() => Context.SaveSettings();
}

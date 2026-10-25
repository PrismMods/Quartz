using Quartz.Compat.Game;
using Quartz.Core;
using Quartz.Localization;
using Quartz.UI;
using Quartz.UI.Generator;
using Quartz.UI.Nav;
using TMPro;
using UnityEngine;
namespace Quartz.Features;
// An unloaded feature keeps a stub tab where its pages would be, so it can be turned on from there.
internal static class FeaturePages {
    private const string StubOwner = "feature-off:";
    private static string StubKey(string id) => "feature-off." + id;
    public static void Sync() {
        foreach(FeatureService.Handle handle in FeatureService.Features) {
            bool want = !handle.Loaded;
            if(want == (NavRegistry.ByKey(StubKey(handle.Id)) != null)) continue;
            if(!want) {
                NavRegistry.RemoveOwner(StubOwner + handle.Id);
                continue;
            }
            NavRegistry.AddPage(new NavPage {
                Key = StubKey(handle.Id),
                CategoryKey = handle.Info.Tab ?? handle.Info.Group,
                Order = handle.Info.Order,
                Title = handle.Name,
                LocaleKey = handle.Info.NameKey,
                OwnerId = StubOwner + handle.Id,
                Build = content => Build(content, handle),
            });
        }
    }
    // The feature a page belongs to, real or stub; null for core and addon pages.
    public static string FeatureOf(NavPage page) {
        string owner = page?.OwnerId;
        if(owner == null) return null;
        if(owner.StartsWith(StubOwner, StringComparison.Ordinal)) return owner[StubOwner.Length..];
        return FeatureService.Find(owner) != null ? owner : null;
    }
    public static bool IsStub(NavPage page) => page?.OwnerId?.StartsWith(StubOwner, StringComparison.Ordinal) == true;
    public static int StateFor(string id) {
        if(id == null) return -1;
        foreach(NavPage page in NavRegistry.AllVisible())
            if(FeatureOf(page) == id) return page.State;
        return -1;
    }
    public static void AddSwitch(RectTransform content, string id) =>
        GenerateUI.Toggle(GenerateUI.Row(content.transform), true, true, v => FeatureService.SetEnabled(id, v), "Use this feature", "feature_page_enable");
    private static void Build(RectTransform content, FeatureService.Handle handle) {
        GenerateUI.AddTextH1(GenerateUI.Row(content.transform))
            .gameObject.AddComponent<TextLocalization>().Init(handle.Info.NameKey, handle.Name);
        TextMeshProUGUI note = GenerateUI.AddMutedText(GenerateUI.Row(content.transform, 74f), 17f, 0.45f, true);
        FeatureService.Handle blocker = Blocker(handle);
        if(blocker == null) note.gameObject.AddComponent<TextLocalization>().Init("FEATURE_OFF_HINT", "This feature is off. Turn it on to see its settings.");
        else note.text = string.Format(
            GenerateUI.Tr("FEATURE_OFF_NEEDS", "This feature needs {0}. Turning it on turns that on too."),
            MainCore.Tr.Get(blocker.Info.NameKey, blocker.Name));
        GenerateUI.Button(GenerateUI.Row(content.transform), () => FeatureService.SetEnabled(handle.Id, true), "Turn On", "feature_turn_on");
        if(handle.Error == null) return;
        TextMeshProUGUI error = GenerateUI.AddMutedText(GenerateUI.Row(content.transform, 96f), 15f, 0.45f, true);
        TextCompat.SetWrap(error, true);
        error.color = UIColors.SoftRed;
        error.text = handle.Error.Length > 900 ? handle.Error[..900] + "…" : handle.Error;
    }
    private static FeatureService.Handle Blocker(FeatureService.Handle handle) {
        foreach(string dep in handle.Info.Deps)
            if(FeatureService.Find(dep) is { Loaded: false } off) return off;
        return null;
    }
}

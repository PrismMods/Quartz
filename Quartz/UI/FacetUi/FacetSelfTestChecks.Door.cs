#if DEBUG && !IL2CPP
using System.Collections;
using System.Reflection;
using Quartz.Addons;
using Quartz.Core;
using Quartz.Features;
using Quartz.Features.Tuf;
using Quartz.IO;
using UnityEngine;
namespace Quartz.UI.FacetUi;
/// <summary>
/// The self-test door: TUF's fetches, its thumbnails and the update checks stay inside the run.
/// </summary>
internal static partial class FacetSelfTestChecks {
    private const string DoorKey = "selftest-door";

    private static IEnumerator Door(Action<string, bool, string> check, Action<string> info) {
        bool wasOn = FeatureService.IsEnabled("tuf");
        try {
            if(!wasOn) {
                FeatureService.SetEnabledForRun("tuf", true);
                yield return Frames(3);
            }
            TufService tuf = TufService.Instance;
            TufPackService packs = TufPackService.Instance;
            if(tuf == null || packs == null) {
                check("self-test door: the TUF services are up", false, $"tuf={tuf != null} packs={packs != null}");
                yield break;
            }
            tuf.ForgetForTest();
            packs.ForgetForTest();

            int fetches = SelfTestDoor.Fetches, network = SelfTestDoor.Network;
            tuf.Refresh();
            for(int i = 0; i < 30 && tuf.State != TufListState.Empty; i++) yield return Frames(1);
            check("self-test door: a levels refresh reaches the door, not the network",
                SelfTestDoor.Fetches == fetches + 1 && SelfTestDoor.Network == network && tuf.State == TufListState.Empty,
                $"fetches={SelfTestDoor.Fetches - fetches} network={SelfTestDoor.Network - network} state={tuf.State}");

            var pack = new TufPack(DoorKey, "Self-test pack", "", 0, 0, []);
            SelfTestDoor.FetchPacks = (_, _, _, _, _, _) => Task.FromResult(new TufPacksPage([pack], 1));
            fetches = SelfTestDoor.Fetches;
            packs.RefreshPacks();
            for(int i = 0; i < 30 && packs.ListState != TufPackListState.Ready; i++) yield return Frames(1);
            bool listed = packs.Packs.Count == 1 && packs.Packs[0].Id == DoorKey;
            if(listed) packs.OpenPack(packs.Packs[0]);
            for(int i = 0; i < 30 && listed && packs.DetailState != TufPackListState.Empty; i++) yield return Frames(1);
            check("self-test door: a pack list and a pack's levels reach the door",
                listed && packs.DetailState == TufPackListState.Empty && SelfTestDoor.Fetches == fetches + 2 && SelfTestDoor.Network == network,
                $"listed={listed} detail={packs.DetailState} fetches={SelfTestDoor.Fetches - fetches} network={SelfTestDoor.Network - network}");
            packs.ClosePack();
            SelfTestDoor.Defaults();

            yield return DoorThumbnail(check);

            var settings = typeof(TufService).GetField("settings", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(tuf)
                as SettingsFile<TufSettings>;
            if(settings == null) check("self-test door: AutoCheckUpdates starts no check", false, "no TUF settings");
            // With no installed level AutoCheckUpdates returns before any request whether the door is there or not.
            else if(tuf.InstalledCount == 0) info("self-test door: AutoCheckUpdates starts no check: not run, no installed TUF levels");
            else {
                // Due as far as the check knows, for this one call: the door alone holds it back. Put back in the same frame.
                long last = settings.Data.LastUpdateCheckUtc;
                settings.Data.LastUpdateCheckUtc = 0;
                bool checking;
                long after;
                try {
                    tuf.AutoCheckUpdates();
                    checking = tuf.CheckingUpdates;
                    after = settings.Data.LastUpdateCheckUtc;
                } finally {
                    settings.Data.LastUpdateCheckUtc = last;
                }
                check("self-test door: AutoCheckUpdates starts no check", !checking && after == 0,
                    $"checking={checking} last={after} installed={tuf.InstalledCount}");
            }
        } finally {
            TufService.Instance?.ForgetForTest();
            TufPackService.Instance?.ForgetForTest();
            TufPreviewCache.Clear();
            SelfTestDoor.Defaults();
            if(!wasOn) FeatureService.SetEnabledForRun("tuf", false);
        }
        int checks = SelfTestDoor.UpdateChecks, before = SelfTestDoor.Network;
        AddonUpdateCheck.Check("selftest/none", "0.0.0", _ => { });
        check("self-test door: an addon update check stays home",
            SelfTestDoor.UpdateChecks == checks + 1 && SelfTestDoor.Network == before,
            $"checks={SelfTestDoor.UpdateChecks - checks} network={SelfTestDoor.Network - before}");
    }

    private static IEnumerator DoorThumbnail(Action<string, bool, string> check) {
        var image = new Texture2D(64, 64, TextureFormat.RGB24, false);
        byte[] jpeg;
        try {
            var pixels = new Color32[64 * 64];
            for(int i = 0; i < pixels.Length; i++) pixels[i] = new Color32((byte)(i % 64 * 4), (byte)(i / 64 * 4), 128, 255);
            image.SetPixels32(pixels);
            image.Apply(false);
            jpeg = image.EncodeToJPG();
        } finally {
            UnityEngine.Object.Destroy(image);
        }
        string file = Path.Combine(MainCore.Paths.TufPath, "Thumbnails", DoorKey + ".jpg");
        bool existed = File.Exists(file);
        SelfTestDoor.Preview = _ => jpeg;
        int previews = SelfTestDoor.Previews, network = SelfTestDoor.Network;
        try {
            TufPreviewCache.Request(DoorKey, TufPreviewSource.Video("https://youtu.be/selftest"));
            for(int i = 0; i < 30 && !TufPreviewCache.TryGet(DoorKey, out _); i++) yield return Frames(1);
            bool ready = TufPreviewCache.TryGet(DoorKey, out _);
            check("self-test door: a thumbnail request decodes the door's bytes and touches no disk",
                ready && SelfTestDoor.Previews == previews + 1 && SelfTestDoor.Network == network && !existed && !File.Exists(file),
                $"ready={ready} previews={SelfTestDoor.Previews - previews} network={SelfTestDoor.Network - network} file={existed}->{File.Exists(file)}");
        } finally {
            TufPreviewCache.Clear();
            SelfTestDoor.Defaults();
        }
    }
}
#endif

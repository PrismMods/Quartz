using Quartz.Modules;
using Quartz.Features.KeyViewer.Js;
using Quartz.UI.Factory.Page;
using Quartz.UI.Nav;
namespace Quartz.Features.KeyViewer;
public sealed class KeyViewerModule : QuartzModule {
    private Action<UnityEngine.KeyCode, bool> hookHandler;
    private Action<UnityEngine.KeyCode, bool> jsHookHandler;
    public override void OnLoad() {
        foreach(string lang in new[] { "en-US", "ko-KR", "zh-CN" })
            Context.RegisterTranslations(typeof(KeyViewerModule), $"Quartz.Features.KeyViewer.Lang.{lang}.json");
        KeyViewerOverlay.EnsureConf();
        if(KvJsAssemblies.EnsureLoaded()) KvJsRuntime.Reload(KeyViewerOverlay.Conf);
        Layout.KvStore.RegisterHandle();
        hookHandler = KvInputQueue.Push;
        Quartz.Game.HookKeys.KeyEvent += hookHandler;
        jsHookHandler = KvJsRuntime.OnKeyEvent;
        Quartz.Game.HookKeys.KeyEvent += jsHookHandler;
        Context.RegisterRescalable("keyviewer", KeyViewerOverlay.Rescale);
        Context.AddPage(new NavPage {
            Key = "overlay.keyviewer",
            CategoryKey = "overlay",
            Order = 20,
            Title = "Key Viewer",
            LocaleKey = "SECTION_KEY_VIEWER",
            Build = PageKeyViewer.Create,
            OwnScroll = true,
        });
        Context.AddHomeCard(new Quartz.UI.Home.HomeCard {
            Key = "keyviewer.home",
            Title = "Key Viewer",
            LocaleKey = "SECTION_KEY_VIEWER",
            Order = 10,
            Build = body => {
                KeyViewerOverlay.EnsureConf();
                KeyViewerSettings conf = KeyViewerOverlay.Conf;
                if(conf == null) return;
                Quartz.UI.Home.HomeUI.Line(body, conf.Enabled
                    ? Quartz.Core.MainCore.Tr.Get("KVI_HOME_ON", "Showing during play")
                    : Quartz.Core.MainCore.Tr.Get("KVI_HOME_OFF", "Hidden"));
                Quartz.UI.Home.HomeUI.Line(body, string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    Quartz.Core.MainCore.Tr.Get("KVI_HOME_RAIN", "Rain {0} · speed {1:0} px/s"),
                    conf.RainEnabled
                        ? Quartz.Core.MainCore.Tr.Get("KVI_HOME_ON", "on")
                        : Quartz.Core.MainCore.Tr.Get("KVI_HOME_OFF", "off"),
                    conf.DmNoteSpeed));
            },
        });
        Context.RegisterImportHandler(new KeyViewerImport());
        Context.PatchAll(typeof(KeyViewerModule));
        Context.OnModEnable("KeyViewerOverlay", () => KeyViewerOverlay.Initialize(Quartz.Core.MainCore.Root));
        Context.OnModDisable("KeyViewerOverlay", KeyViewerOverlay.Dispose);
    }
    public override void OnUnload() {
        if(hookHandler != null) {
            Quartz.Game.HookKeys.KeyEvent -= hookHandler;
            hookHandler = null;
        }
        if(jsHookHandler != null) {
            Quartz.Game.HookKeys.KeyEvent -= jsHookHandler;
            jsHookHandler = null;
        }
        Layout.KvStore.UnregisterHandle();
        KvJsRuntime.Shutdown();
        KeyViewerOverlay.Dispose();
    }
}

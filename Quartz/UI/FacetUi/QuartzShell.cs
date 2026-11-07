#if !IL2CPP
using Facet;
using Quartz.Core;
using Quartz.Resource;
using Quartz.UI.Nav;
using UnityEngine;
using UnityEngine.UIElements;
namespace Quartz.UI.FacetUi;
/// <summary>
/// The new menu: one Facet window whose rail is NavRegistry's categories and whose tab rows are their pages
/// (QuartzShell.Pages). It stands in for the classic menu and hands off to it and back through MenuHandOff.
/// </summary>
internal static partial class QuartzShell {
    internal const string WindowId = "quartz.menu", PageStore = "menu.page";
    internal static Surface Surface { get; private set; }
    internal static Window Window { get; private set; }
    internal static Facet.Nav Nav { get; private set; }
    /// <summary>The page the window last built on screen.</summary>
    internal static string CurrentPage { get; private set; }
    /// <summary>No geometry was saved for the window when it was built, so its first show takes the initial rect.</summary>
    internal static bool FreshGeometry { get; private set; }
    private static bool rectPending;

    /// <summary>Builds the window hidden, on the page left last time (Home when that is gone). Once.</summary>
    internal static void Prebuild() {
        if(Surface != null) return;
        // Read before the Nav opens its first page, which stores itself.
        string last = ShellModel.LastPage(QuartzHost.Host.Read(PageStore), k => NavRegistry.ByKey(k) != null, CorePages.HomePageKey);
        Surface s = QuartzHost.NewSurface("QuartzMenu", Layer.Window);
        try {
            s.Visible = false;
            FreshGeometry = rectPending = QuartzHost.Host.Read("window." + WindowId) == null;
            // A placeholder until the first show knows the panel's size.
            var (x, y, w, h) = ShellModel.InitialRect(1920f, 1080f);
            Window = new Window(s.Root, WindowId, "QUARTZ", new Rect(x, y, w, h));
            // Facet stores the rect a window is built with: a prebuild never shown must not leave the placeholder as the user's.
            if(FreshGeometry) QuartzHost.Host.Write("window." + WindowId, null);
            VisualElement logo = Ui.Box();
            logo.name = "menu-logo";
            logo.style.width = 18f;
            logo.style.height = 18f;
            logo.style.marginRight = 6f;
            logo.style.alignSelf = Align.Center;
            logo.style.backgroundImage = new StyleBackground(MainCore.Spr.Get(UISprite.QuartzLogo));
            Window.TitleBarLeft.Insert(0, logo);
            BuildNav();
            s.Hidden += Hidden;
            Surface = s;
        } catch {
            s.Dispose();   // a half-built window is never kept or shown; the next open builds afresh
            Window = null;
            Nav = null;
            throw;
        }
        if(CurrentPage != last) Go(last);
    }

    /// <summary>Shows the window, on pageKey when given. From the classic menu, that menu closes and a close here brings it back.</summary>
    internal static void Open(string pageKey = null, bool fromClassic = false) {
        Prebuild();   // first: a build that throws leaves the classic menu as it was
        if(builtCategories != CategoryKeys()) {
            // An addon added or removed a category: rebuild the rail's groups, on the page it showed.
            string at = CurrentPage;
            BuildNav();
            if(at != null) Go(at);
        }
        if(fromClassic) {
            MenuHandOff.ReturnTo = MenuHandOff.Menu.None;   // the classic close below is no user's close
            UICore.HandToPreview();
            MenuHandOff.ReturnTo = MenuHandOff.Menu.Classic;
        }
        if(pageKey != null) Go(pageKey);
        ApplyLook();
        Surface.BlocksGameInput = MainCore.Conf.BlockInputsWhileMenuOpen;
        Surface.Visible = true;
    }

    internal static bool Hide() {
        if(Surface == null || !Surface.Visible) return false;
        Surface.Visible = false;
        return true;
    }

    /// <summary>"Open it" on a classic card: this window hides and the classic menu opens on that page, and comes back here on close.</summary>
    internal static void OpenClassic(int state) {
        MenuHandOff.ReturnTo = MenuHandOff.Menu.None;
        Hide();
        UICore.OpenAt(state);
    }

    // Whoever hid it (the close button, Escape, the menu hotkey, an opener): a classic menu it came from comes back.
    private static void Hidden() {
        if(MenuHandOff.ReturnTo != MenuHandOff.Menu.Classic) return;
        MenuHandOff.ReturnTo = MenuHandOff.Menu.None;
        UICore.BackFromPreview();
    }

    /// <summary>The classic menu's scale and opacity: its default scale, 0.85, is 1×.</summary>
    internal static void ApplyLook() {
        if(Window == null) return;
        Window.Scale = MainCore.Conf.UIScale / 0.85f;
        Window.Root.style.opacity = MainCore.Conf.PanelOpacity;
    }

    /// <summary>Per frame. Nothing while hidden.</summary>
    internal static void Tick() {
        if(Surface == null || !Surface.Visible) return;
        Nav.Tick();
        float pw = Surface.Root.layout.width;   // NaN or 0 until the shown window has been laid out
        if(!rectPending || !(pw > 0f)) return;
        rectPending = false;
        var (x, y, w, h) = ShellModel.InitialRect(pw, Surface.Root.layout.height);
        Window.Rect = new Rect(x, y, w, h);
    }

    /// <summary>Before QuartzHost disposes the surface: a dispose raises Hidden, which must not reopen the classic menu.</summary>
    internal static void Shutdown() {
        if(Surface != null) Surface.Hidden -= Hidden;
        if(MenuHandOff.ReturnTo == MenuHandOff.Menu.Classic) MenuHandOff.ReturnTo = MenuHandOff.Menu.None;
        Surface = null;
        Window = null;
        Nav = null;
        TabRow = null;
        CurrentPage = null;
        currentFeature = null;
    }
}
#endif

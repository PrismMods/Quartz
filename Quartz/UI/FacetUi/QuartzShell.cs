#if !IL2CPP
using System.Runtime.CompilerServices;
using Facet;
using Quartz.Core;
using Quartz.Prism;
using Quartz.Resource;
using UnityEngine.UIElements;
#if !QUARTZ_KEYVIEWER
using SuiteShell = PrismLib.UI.Toolkit.SuiteShell;
#endif
namespace Quartz.UI.FacetUi;
/// <summary>
/// The new menu: PrismLib.UI's suite shell, fed by <see cref="QuartzSuite"/>, behind a facade that names no PrismLib.UI
/// type. The shell is held as object and reached only through NoInlining …Impl helpers, so Quartz loads beside an older
/// PrismLib.UI without one.
/// </summary>
internal static partial class QuartzShell {
    internal const string WindowId = "quartz.menu", PageStore = "menu.page", SidebarStore = "menu.sidebar", CompactStore = "menu.compact";
    private static object shell;
    internal static Surface Surface => shell == null ? null : SurfaceImpl();
    internal static Window Window => shell == null ? null : WindowImpl();
    internal static Facet.Nav Nav => shell == null ? null : NavImpl();
    /// <summary>The tab row of the open category, when it has one.</summary>
    internal static Tabs TabRow => shell == null ? null : TabRowImpl();
    /// <summary>The page the window last built on screen.</summary>
    internal static string CurrentPage => shell == null ? null : CurrentPageImpl();
    /// <summary>No geometry was kept for the window when it was built, so its first show takes the initial rect.</summary>
    internal static bool FreshGeometry => shell != null && FreshGeometryImpl();

    /// <summary>The bind of a row writing a setting.</summary>
    internal static Bind<T> Writes<T>(Func<T> get, Action<T> set) => Bind.To(get, set);

    /// <summary>Builds the window hidden, on the page left last time (Home when that is gone). Once.</summary>
    internal static void Prebuild() {
        if(shell != null) return;
        BuildImpl();
        // A font found or a global font chosen relabels the Overlay pages' font lists: the open page rebuilds.
        globalFont = FontManager.CurrentName;
        FontManager.OnFontCatalogChanged += Changed;
        FontManager.OnFontChanged += GlobalFontChanged;
    }

    private static string globalFont;

    // An overlay's own pick fires this too, and its row reads that live: only a new global font relabels the lists, whose
    // first item names it. A rebuild for nothing would also drop the open page's preview lease.
    private static void GlobalFontChanged() {
        if(FontManager.CurrentName == globalFont) return;
        globalFont = FontManager.CurrentName;
        Changed();
    }

    /// <summary>Something the rail or pages show changed that NavRegistry.Version does not count: the open page rebuilds.</summary>
    internal static void Changed() {
        if(shell != null) ChangedImpl();
    }

    /// <summary>Shows the window, on pageKey when given.</summary>
    internal static void Open(string pageKey = null) {
        Prebuild();
        if(shell == null) return;
        OpenImpl(pageKey);
    }

    internal static bool Hide() => shell != null && HideImpl();

    /// <summary>Opens a page: its category in the rail, and its tab there.</summary>
    internal static void Go(string pageKey) {
        if(shell != null) GoImpl(pageKey);
    }

    /// <summary>"Edit layout": the window steps aside for the shared layout editor, and comes back when that session ends.</summary>
    internal static void EditLayout() {
        if(shell != null) EditLayoutImpl();
    }

    // The shell's page frames and Settings rows, for the page builders. With no shell a header or card is null and a row
    // is not built: no page builds then.
    internal static Control<bool> Header(VisualElement page, string title, Bind<bool> on, Func<string> description = null) =>
        shell == null ? null : HeaderImpl(page, title, on, description);

    internal static void FeatureFrame(VisualElement page, string title, Bind<bool> on, string description, Action<VisualElement> cards,
        Action<VisualElement> preview = null) {
        if(shell != null) FeatureFrameImpl(page, title, on, description, cards, preview);
    }

    /// <summary>The Edit layout row, shown only where the shared layout editor can open.</summary>
    internal static void LayoutRow(VisualElement card, string id) {
        if(shell != null) LayoutRowImpl(card, id);
    }

    internal static VisualElement SettingsCard(VisualElement page, string title, Action<CardOptions> more = null) =>
        shell == null ? null : SettingsCardImpl(page, title, more);

    internal static void SidebarRow(VisualElement card) {
        if(shell != null) SidebarRowImpl(card);
    }

    internal static void SmoothRow(VisualElement card, Bind<bool> on) {
        if(shell != null) SmoothRowImpl(card, on);
    }

    internal static void PrismLinkRow(VisualElement card, Action open, Func<bool> shown) {
        if(shell != null) PrismLinkRowImpl(card, open, shown);
    }

    internal static void ScrollSpeedRow(VisualElement card, Bind<float> speed) {
        if(shell != null) ScrollSpeedRowImpl(card, speed);
    }

    /// <summary>The window's scale at the classic menu's default, 0.85: larger than that menu, whose text it would otherwise match.</summary>
    internal const float BaseScale = 1.2f;

    /// <summary>The classic menu's scale and opacity, and its input blocking: its default scale, 0.85, is BaseScale here.</summary>
    internal static void ApplyLook() {
        if(shell != null) ApplyLookImpl();
    }

    /// <summary>The Sidebar setting: 0 toggleable (a button folds the rail to its icons, and the choice is kept), 1 always
    /// visible, 2 always hidden (the icon strip, widening over the page while the pointer rests on it). Applies at once.</summary>
    internal static int SidebarMode {
        get => shell == null ? QuartzHost.Host.Read(SidebarStore) switch { "1" => 1, "2" => 2, _ => 0 } : SidebarModeImpl();
        set {
            if(shell == null) QuartzHost.Host.Write(SidebarStore, value is 1 or 2 ? value.ToString() : null);
            else SetSidebarModeImpl(value);
        }
    }

    internal static void ApplySidebar() {
        if(shell != null) ApplySidebarImpl();
    }

    // The rail refilled from NavRegistry, as an added or removed category does on the next open.
    private static void BuildNav() {
        if(shell != null) RebuildImpl();
    }

    /// <summary>Per frame. Nothing while hidden, unless Edit layout is waiting to bring it back, or the search index is still
    /// being built ahead of the first search.</summary>
    internal static void Tick() {
        if(shell == null) return;
        TickImpl();
        if(!SearchWarm.Done) SearchWarm.Step();
    }

    /// <summary>Before QuartzHost disposes its own surfaces.</summary>
    internal static void Shutdown() {
        FontManager.OnFontCatalogChanged -= Changed;
        FontManager.OnFontChanged -= GlobalFontChanged;
        if(shell != null) DisposeImpl();
        shell = null;
    }

    // The doors: only these name the shell's type, each under the KeyViewer guard (that flavour has no PrismLib.UI and
    // never builds the shell). Their signatures are Facet or BCL types.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void BuildImpl() {
#if !QUARTZ_KEYVIEWER
        var s = new SuiteShell(new QuartzSuite());
        shell = s;   // first: the page Prebuild builds reaches the shell through this facade
        try {
            s.Prebuild();
            // A window that could not build logs and stays null: throw, so the bridge turns the menu off.
            if(s.Surface == null) throw new System.InvalidOperationException("new menu: no surface");
        } catch {
            shell = null;
            s.Dispose();   // a half-built window is never kept or shown; the next open builds afresh
            throw;
        }
#endif
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void DisposeImpl() {
#if !QUARTZ_KEYVIEWER
        ((SuiteShell)shell).Dispose();
#endif
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void OpenImpl(string pageKey) {
#if !QUARTZ_KEYVIEWER
        ((SuiteShell)shell).Open(pageKey);
#endif
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool HideImpl() {
#if !QUARTZ_KEYVIEWER
        return ((SuiteShell)shell).Hide();
#else
        return false;
#endif
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void GoImpl(string pageKey) {
#if !QUARTZ_KEYVIEWER
        ((SuiteShell)shell).Go(pageKey);
#endif
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ChangedImpl() {
#if !QUARTZ_KEYVIEWER
        ((SuiteShell)shell).Changed();
#endif
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ApplyLookImpl() {
#if !QUARTZ_KEYVIEWER
        ((SuiteShell)shell).ApplyLook();
#endif
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ApplySidebarImpl() {
#if !QUARTZ_KEYVIEWER
        ((SuiteShell)shell).ApplySidebar();
#endif
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int SidebarModeImpl() {
#if !QUARTZ_KEYVIEWER
        return ((SuiteShell)shell).SidebarMode;
#else
        return 0;
#endif
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void SetSidebarModeImpl(int mode) {
#if !QUARTZ_KEYVIEWER
        ((SuiteShell)shell).SidebarMode = mode;
#endif
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void EditLayoutImpl() {
#if !QUARTZ_KEYVIEWER
        ((SuiteShell)shell).EditLayout();
#endif
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void RebuildImpl() {
#if !QUARTZ_KEYVIEWER
        ((SuiteShell)shell).RebuildRail();
#endif
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void TickImpl() {
#if !QUARTZ_KEYVIEWER
        ((SuiteShell)shell).Tick();
#endif
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Surface SurfaceImpl() {
#if !QUARTZ_KEYVIEWER
        return ((SuiteShell)shell).Surface;
#else
        return null;
#endif
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Window WindowImpl() {
#if !QUARTZ_KEYVIEWER
        return ((SuiteShell)shell).Window;
#else
        return null;
#endif
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Facet.Nav NavImpl() {
#if !QUARTZ_KEYVIEWER
        return ((SuiteShell)shell).Nav;
#else
        return null;
#endif
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Tabs TabRowImpl() {
#if !QUARTZ_KEYVIEWER
        return ((SuiteShell)shell).TabRow;
#else
        return null;
#endif
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static string CurrentPageImpl() {
#if !QUARTZ_KEYVIEWER
        return ((SuiteShell)shell).CurrentPage;
#else
        return null;
#endif
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool FreshGeometryImpl() {
#if !QUARTZ_KEYVIEWER
        return ((SuiteShell)shell).FreshGeometry;
#else
        return false;
#endif
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Control<bool> HeaderImpl(VisualElement page, string title, Bind<bool> on, Func<string> description) {
#if !QUARTZ_KEYVIEWER
        return ((SuiteShell)shell).Header(page, title, on, description);
#else
        return null;
#endif
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void FeatureFrameImpl(VisualElement page, string title, Bind<bool> on, string description, Action<VisualElement> cards,
        Action<VisualElement> preview) {
#if !QUARTZ_KEYVIEWER
        ((SuiteShell)shell).FeatureFrame(page, title, on, description, cards, preview);
#endif
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void LayoutRowImpl(VisualElement card, string id) {
#if !QUARTZ_KEYVIEWER
        ((SuiteShell)shell).LayoutRow(card, id);
#endif
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static VisualElement SettingsCardImpl(VisualElement page, string title, Action<CardOptions> more) {
#if !QUARTZ_KEYVIEWER
        return ((SuiteShell)shell).SettingsCard(page, title, more);
#else
        return null;
#endif
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void SidebarRowImpl(VisualElement card) {
#if !QUARTZ_KEYVIEWER
        ((SuiteShell)shell).SidebarRow(card);
#endif
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void SmoothRowImpl(VisualElement card, Bind<bool> on) {
#if !QUARTZ_KEYVIEWER
        ((SuiteShell)shell).SmoothRow(card, on);
#endif
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void PrismLinkRowImpl(VisualElement card, Action open, Func<bool> shown) {
#if !QUARTZ_KEYVIEWER
        ((SuiteShell)shell).PrismLinkRow(card, open, shown);
#endif
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ScrollSpeedRowImpl(VisualElement card, Bind<float> speed) {
#if !QUARTZ_KEYVIEWER
        ((SuiteShell)shell).ScrollSpeedRow(card, speed);
#endif
    }

}
#endif

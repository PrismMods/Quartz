#if !IL2CPP && !QUARTZ_KEYVIEWER
using Quartz.Core;
using Quartz.Features;
using Quartz.Prism;
using Quartz.Resource;
using Quartz.UI.Nav;
using UnityEngine.UIElements;
using SuiteAdapter = PrismLib.UI.Toolkit.SuiteAdapter;
using SuiteCategory = PrismLib.UI.Toolkit.SuiteCategory;
using SuitePage = PrismLib.UI.Toolkit.SuitePage;
namespace Quartz.UI.FacetUi;
/// <summary>
/// What Quartz gives PrismLib.UI's suite shell: NavRegistry's categories and pages, Quartz's look settings and the
/// classic menu's hand-off. The one Quartz class that names a shell type; QuartzShell builds it behind its NoInlining
/// doors only, after FacetBridge.MenuAvailable().
/// </summary>
internal sealed class QuartzSuite : SuiteAdapter {
    public override Facet.Host Host => QuartzHost.Host;
    public override string WindowId => QuartzShell.WindowId;
    public override string Title => "QUARTZ";
    public override string HomePage => CorePages.HomePageKey;
    public override Background? Logo => Background.FromSprite(MainCore.Spr.Get(UISprite.QuartzLogo));
    public override float RailWidth => 160f;
    public override int Revision => NavRegistry.Version;

    public override IList<SuiteCategory> Categories() => NavRegistry.AllCategories.Select(c => new SuiteCategory {
        Key = c.Key,
        Label = QuartzShell.Label(c.LocaleKey, c.Title),
        Icon = QuartzShell.CategoryIcon(c) is { } icon ? Background.FromSprite(icon) : default,   // the rail draws a fixed square: IconScale has no say
        Group = ShellModel.RailGroup(c.Key),
        Tabs = NavRegistry.ShowsSubmenu(c),
        Visible = () => c.IsVisible && NavRegistry.PagesIn(c.Key).Count > 0,
        Pages = () => NavRegistry.PagesIn(c.Key).Select(pg => new SuitePage {
            Key = pg.Key,
            Label = QuartzShell.Label(pg.LocaleKey, pg.Title),
            Feature = FeaturePages.FeatureOf(pg),
            Visible = pg.Visible,
            OnHit = QuartzShell.ClassicHit(pg),
        }).ToList(),
        // A category without tabs is its one page, which can change under it: asked when the hit lands.
        OnHit = h => {
            IReadOnlyList<NavPage> pages = NavRegistry.PagesIn(c.Key);
            if(!NavRegistry.ShowsSubmenu(c) && pages.Count > 0) QuartzShell.ClassicHit(pages[0])?.Invoke(h);
        },
    }).ToList();

    public override void BuildPage(VisualElement panel, SuitePage page) {
        NavPage pg = NavRegistry.ByKey(page.Key);
        if(pg == null) return;
        if(QuartzShell.BuilderFor(pg) is { } build) build(panel);
        else if(FeaturePages.IsStub(pg)) QuartzShell.Stub(panel, pg);
        else QuartzShell.Classic(panel, pg);
    }

    public override IEnumerable<Facet.SearchIndex.Entry> SearchRows() => QuartzShell.ClassicRows();
    public override string Key(string id, string english) => id switch {
        "suite.search" => "SEARCH",
        "suite.sidebar" => "MENU_SIDEBAR",
        "suite.sidebar.tip" => "MENU_SIDEBAR_TIP",
        "suite.sidebar.toggle" => "MENU_SIDEBAR_TOGGLE",
        "suite.sidebar.shown" => "MENU_SIDEBAR_SHOWN",
        "suite.sidebar.hidden" => "MENU_SIDEBAR_HIDDEN",
        "suite.smooth" => "SMOOTH_SCROLL",
        "suite.smooth.tip" => "DESC_SMOOTH_SCROLL",
        "suite.animation" => "MENU_ANIM",
        "suite.animation.open" => "MENU_OPEN_PRISM",
        "suite.scroll" => "SCROLL_SPEED",
        "suite.classic.menu" => "MENU_CLASSIC_MENU",
        "suite.classic.page" => "MENU_CLASSIC_PAGE",
        "suite.classic.open" => "MENU_CLASSIC_OPEN",
        "suite.layout.hint" => "MENU_LAYOUT_HINT",
        "suite.layout.edit" => "MENU_EDIT_LAYOUT",
        _ => english,
    };

    public override float Scale => MainCore.Conf.UIScale / 0.85f * QuartzShell.BaseScale;
    public override float Opacity => MainCore.Conf.PanelOpacity;
    public override bool BlockInput => MainCore.Conf.BlockInputsWhileMenuOpen;
    public override bool KeepEscape => Keybind.Capturing;
    public override bool CanEditLayout => PrismBridge.UiAvailable;

    public override void OnLeaving() => MenuHandOff.ReturnTo = MenuHandOff.Menu.None;   // a classic menu this came from stays shut under the editor
    public override void OpenLayoutEditor() => PrismBridge.OpenLayoutEditor();
    public override bool LayoutEditorShowing => UICore.IsArranging || LayoutEditorProbe.Shown();
    public override bool ReturnFromLayout => !UICore.IsOpen;
    // Back from a trip away that began at the hub: the menu's close still goes to the hub.
    public override void OnReturning() {
        if(MenuHandOff.FromHub) MenuHandOff.ReturnTo = MenuHandOff.Menu.Classic;
    }
}
#endif

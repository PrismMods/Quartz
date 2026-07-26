using Quartz.Resource;
namespace Quartz.UI.Nav;
public sealed class NavCategory {
    public string Key;
    public string Title;
    public string LocaleKey;
    public UISprite Icon;
    public string IconAsset;
    public int Order;
    public float IconScale = 1f;
    public bool AlwaysSubmenu;
    public bool Togglable;
    public Func<bool> Visible;
    public bool IsVisible => Visible == null || Visible();
}

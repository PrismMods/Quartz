using UnityEngine;
namespace Quartz.UI.Nav;
public sealed class NavPage {
    public int State { get; internal set; } = -1;
    public string Key;
    public string CategoryKey;
    public string Title;
    public string LocaleKey;
    public Func<string> SearchLabel;
    public int Order;
    public string OwnerId;
    public bool OwnScroll;
    public bool SeparatorBefore;
    public Func<bool> Visible;
    public Action<RectTransform> Build;
    public bool IsVisible => Visible == null || Visible();
}

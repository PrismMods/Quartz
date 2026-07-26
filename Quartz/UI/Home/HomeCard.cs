using UnityEngine;
namespace Quartz.UI.Home;
public sealed class HomeCard {
    public string Key;
    public string OwnerId;
    public string Title;
    public string LocaleKey;
    public int Order;
    public Func<bool> Visible;
    public Action<RectTransform> Build;
    public bool IsVisible => Visible == null || Visible();
}

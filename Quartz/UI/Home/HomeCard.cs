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
    /// <summary>Plain status lines, used when Build is null: the classic Home draws one line each, the new menu one status row.</summary>
    public Func<IEnumerable<string>> Lines;
    public bool IsVisible => Visible == null || Visible();
}

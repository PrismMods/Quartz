using UnityEngine;
namespace Quartz.UI.Search;
/// <summary>A row's tooltip text, so search can match words that only the description uses.</summary>
public sealed class SearchTip : MonoBehaviour {
    public Func<string> Text;
}

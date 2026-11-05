using UnityEngine;
namespace Quartz.UI.Search;
/// <summary>On a container search can't just scroll to — a tab or panel that only shows its rows on demand. Search runs <see cref="Reveal"/> before jumping to a row inside it.</summary>
public sealed class SearchReveal : MonoBehaviour {
    public Action Reveal;
}
/// <summary>A row's tooltip text, so search can match words that only the description uses.</summary>
public sealed class SearchTip : MonoBehaviour {
    public Func<string> Text;
}

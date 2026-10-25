using UnityEngine;
namespace Quartz.Resource;
// Marks an overlay's texts (or a settings-window preview of them) so they take that overlay's font, not the menu font.
internal sealed class OverlayFontScope : MonoBehaviour {
    // FontManager.For key; null follows the global font.
    public string Key;
}

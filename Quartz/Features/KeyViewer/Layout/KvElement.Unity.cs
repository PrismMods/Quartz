using UnityEngine;
namespace Quartz.Features.KeyViewer.Layout;
/// <summary>
/// Unity-side bridge. Kept out of <see cref="KvElement"/>'s main file so the model stays
/// engine-free and compiles into Quartz.Tests, where the round-trip contract is verified.
/// </summary>
internal sealed partial class KvElement {
    internal KeyCode KeyCodeValue => KeyViewerOverlay.ResolveGlobalKey(GlobalKey);
    internal KeyCode GhostKeyCodeValue => KeyViewerOverlay.ResolveGlobalKey(GhostKey);
    internal Rect Bounds => new(X, Y, W, H);
    internal void BindKey(KeyCode key) => GlobalKey = KvKeyNames.ToGlobalKeyOrRaw(key);
    internal void BindGhostKey(KeyCode key) =>
        GhostKey = key == KeyCode.None ? "" : KvKeyNames.ToGlobalKeyOrRaw(key);
}

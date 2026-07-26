using Quartz.Features.Interop;
using UnityEngine;
namespace Quartz.Interop;
public sealed class ImportedKeyViewer {
    public SettingsImportKeyViewerPart Available;
    public bool HasStyle;
    public int Style;
    public int[] Key10, Key12, Key16, Key20;
    public string[] Key10Text, Key12Text, Key16Text, Key20Text;
    public Color? Bg, BgClicked, Outline, OutlineClicked, Text, TextClicked, Rain, Rain2, Rain3;
    public bool HasRainEnabled; public bool RainEnabled;
    public bool HasRainSpeed; public float RainSpeed;
    public bool HasRainHeight; public float RainHeight;
    public bool HasSize; public float Size;
    public bool HasEnabled; public bool Enabled;
    public bool HasSync; public bool SyncToKeyLimiter;
    public bool HasFoot; public int FootStyle; public int[] FootKeys;
    public int[] GhostKey10, GhostKey12, GhostKey16, GhostKey20;
    public Color? GhostRain;
}

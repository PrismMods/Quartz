using UnityEngine;
namespace Quartz.Features.KeyViewer;
public static partial class KeyViewerOverlay {
    private static RawRain SpawnDmRain(Box box, float now, bool ghost) {
        DmNoteSpec spec = box.Dm;
        if(spec == null || tabubActive) return null;
        RainManager manager = box.Rain;
        if(manager == null) return null;
        RawRain raw = manager.Rent();
        raw.Group = 1;
        raw.Order = spec.ZIndex;
        raw.StartTime = now;
        raw.AnchorX = box.CenterX;
        raw.Width = box.BoxW;
        raw.BaseY = -spec.TrackBottomY;
        raw.TrackHeight = Mathf.Max(1f, dmTrackHeight);
        raw.Speed = Mathf.Max(1f, dmNoteSpeed);
        raw.FadePx = Mathf.Max(0f, dmFadePx);
        raw.Reverse = dmNoteReverse;
        raw.Color = ghost ? spec.GhostRain : spec.Rain;
        raw.ColorTop = ghost ? spec.GhostRainTop : spec.RainTop;
        raw.ColorBottom = ghost ? spec.GhostRainBottom : spec.RainBottom;
        raw.GlowSize = spec.RainGlowOn ? spec.RainGlowSize : 0f;
        raw.GlowTop = ghost ? spec.GhostRainGlowTop : spec.RainGlowTop;
        raw.GlowBottom = ghost ? spec.GhostRainGlowBottom : spec.RainGlowBottom;
        if(spec.RainShadowOn) {
            raw.ShadowColor = spec.RainShadowColor;
            raw.ShadowX = spec.RainShadowX;
            raw.ShadowY = spec.RainShadowY;
        }
        raw.BorderColor = spec.NoteBorderColor;
        raw.BorderWidth = spec.NoteBorderWidth;
        raw.BorderSide = spec.NoteBorderSide;
        raw.CornerRadius = spec.NoteRadius;
        if(ghost && Conf.GhostRainDotted) {
            raw.Dotted = true;
            raw.DotLength = Conf.GhostRainDotLength;
            raw.GapLength = Conf.GhostRainGapLength;
        }
        manager.Enqueue(raw);
        return raw;
    }
}

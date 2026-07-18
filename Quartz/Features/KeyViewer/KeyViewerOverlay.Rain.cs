using UnityEngine;
namespace Quartz.Features.KeyViewer;
public static partial class KeyViewerOverlay {
    private static RawRain SpawnRain(Box box, float now, bool ghost = false) {
        bool frontRow = box.RainGroup == 1;
        float width = frontRow ? Conf.RainWidth : Conf.Rain2Width;
        if(width <= 0.5f) {
            width = KeyW;
        }
        float keyOffset = Mathf.Max(0f, box.BoxW - KeyW) * 0.5f;
        RawRain raw = rainManager.Rent();
        raw.Group = box.RainGroup;
        raw.StartTime = now;
        raw.AnchorX = box.CenterX + box.RainAlign * keyOffset;
        raw.Width = width;
        raw.BaseY = -(frontRow ? Conf.RainOffsetY : Conf.Rain2OffsetY);
        raw.TrackHeight = Mathf.Max(1f, Conf.RainHeight);
        raw.Speed = Mathf.Max(1f, Conf.RainSpeed);
        raw.FadePx = Mathf.Max(0f, Conf.RainFade);
        raw.Color = ghost ? Conf.GetGhostRain() : Conf.PerKeyOr(Conf.PerKeyRain, box.Slot, box.RainGroup switch {
            1 => Conf.GetRain(),
            3 => Conf.GetRain3(),
            _ => Conf.GetRain2(),
        });
        raw.ColorTop = raw.Color;
        raw.ColorBottom = raw.Color;
        if(ghost && Conf.GhostRainDotted) {
            raw.Dotted = true;
            raw.DotLength = Conf.GhostRainDotLength;
            raw.GapLength = Conf.GhostRainGapLength;
        }
        rainManager.Enqueue(raw);
        return raw;
    }
    private static RawRain SpawnDmRain(Box box, float now, bool ghost) {
        DmNoteSpec spec = box.Dm;
        if(spec == null) return null;
        RawRain raw = rainManager.Rent();
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
        rainManager.Enqueue(raw);
        return raw;
    }
}

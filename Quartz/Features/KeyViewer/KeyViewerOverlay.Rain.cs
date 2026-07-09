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
        RawRain raw = new() {
            Group = box.RainGroup,
            StartTime = now,
            AnchorX = box.CenterX + box.RainAlign * keyOffset,
            Width = width,
            BaseY = -(frontRow ? Conf.RainOffsetY : Conf.Rain2OffsetY),
            TrackHeight = Mathf.Max(1f, Conf.RainHeight),
            Speed = Mathf.Max(1f, Conf.RainSpeed),
            FadePx = Mathf.Max(0f, Conf.RainFade),
            Color = ghost ? Conf.GetGhostRain() : Conf.PerKeyOr(Conf.PerKeyRain, box.Slot, box.RainGroup switch {
                1 => Conf.GetRain(),
                3 => Conf.GetRain3(),
                _ => Conf.GetRain2(),
            }),
        };
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
        RawRain raw = new() {
            Group = 1,
            Order = spec.ZIndex,
            StartTime = now,
            AnchorX = box.CenterX,
            Width = box.BoxW,
            BaseY = -spec.TrackBottomY,
            TrackHeight = Mathf.Max(1f, dmTrackHeight),
            Speed = Mathf.Max(1f, dmNoteSpeed),
            FadePx = Mathf.Max(0f, dmFadePx),
            Reverse = dmNoteReverse,
            Color = ghost ? spec.GhostRain : spec.Rain,
            ColorTop = ghost ? spec.GhostRainTop : spec.RainTop,
            ColorBottom = ghost ? spec.GhostRainBottom : spec.RainBottom,
            GlowSize = spec.RainGlowOn ? spec.RainGlowSize : 0f,
            GlowTop = ghost ? spec.GhostRainGlowTop : spec.RainGlowTop,
            GlowBottom = ghost ? spec.GhostRainGlowBottom : spec.RainGlowBottom,
        };
        if(ghost && Conf.GhostRainDotted) {
            raw.Dotted = true;
            raw.DotLength = Conf.GhostRainDotLength;
            raw.GapLength = Conf.GhostRainGapLength;
        }
        rainManager.Enqueue(raw);
        return raw;
    }
}

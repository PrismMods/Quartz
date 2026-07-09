using Quartz.IO;
using UnityEngine;
namespace Quartz.Core;
public static class OverlayCalibration {
    public static void EnsureCaptured() {
        CoreSettings c = MainCore.Conf;
        if(c == null || (c.CalibWidth > 0f && c.CalibHeight > 0f)) return;
        c.CalibWidth = Screen.width;
        c.CalibHeight = Screen.height;
        MainCore.ConfMgr?.RequestSave();
    }
    public static Vector2 Factor() {
        EnsureCaptured();
        CoreSettings c = MainCore.Conf;
        float cw = c != null && c.CalibWidth > 0f ? c.CalibWidth : Screen.width;
        float ch = c != null && c.CalibHeight > 0f ? c.CalibHeight : Screen.height;
        return new Vector2(
            cw > 0f ? Screen.width / cw : 1f,
            ch > 0f ? Screen.height / ch : 1f
        );
    }
    public static Vector2 Scale(Vector2 stored) {
        Vector2 f = Factor();
        return new Vector2(stored.x * f.x, stored.y * f.y);
    }
    public static Vector2 Unscale(Vector2 anchored) {
        Vector2 f = Factor();
        return new Vector2(
            f.x != 0f ? anchored.x / f.x : anchored.x,
            f.y != 0f ? anchored.y / f.y : anchored.y
        );
    }
}

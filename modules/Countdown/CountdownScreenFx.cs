using System;
using DG.Tweening;
using Quartz.Core;
using UnityEngine;
namespace Quartz.Features.Countdown;
internal static class CountdownScreenFx {
    internal static void ResetVolatileState() {
        if(!MainCore.IsModEnabled) return;
        scrCamera cam;
        try { cam = scrCamera.instance; } catch(Exception e) { Diag.Ignore(e); return; }
        if(cam == null) return;
        ResetBloom(cam);
        ResetFlashPlane(cam.flashPlusRendererFg);
        ResetFlashPlane(cam.flashPlusRendererBg);
    }
    private static void ResetBloom(scrCamera cam) {
        try {
            VideoBloom bloom = cam.GetComponent<VideoBloom>();
            if(bloom != null && bloom.enabled) bloom.enabled = false;
        } catch(Exception e) { Diag.Ignore(e); }
    }
    private static void ResetFlashPlane(Renderer plane) {
        try {
            if(plane == null) return;
            Material mat = plane.material;
            if(mat == null) return;
            DOTween.Kill(mat);
            mat.color = Color.clear;
        } catch(Exception e) { Diag.Ignore(e); }
    }
}

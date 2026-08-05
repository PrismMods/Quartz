using System;
using System.Collections.Generic;
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
internal static class CountdownScrubWindow {
    private static bool pending;
    private static double lead;
    private static double earliest;
    internal static void Disarm() => pending = false;
    internal static void Arm(int floorNum) {
        pending = false;
        try {
            scrConductor conductor = ADOBase.conductor;
            List<scrFloor> floors = ADOBase.lm?.listFloors;
            if(conductor == null || floors == null || floorNum < 0 || floorNum >= floors.Count) return;
            scrFloor floor = floors[floorNum];
            if(floor == null || floor.speed <= 0f) return;
            int ticks = floor.countdownTicks > 1 && floor.extraBeats >= floor.countdownTicks
                ? floor.countdownTicks
                : 4;
            lead = conductor.crotchetAtStart / floor.speed * ticks;
            earliest = conductor.separateCountdownTime
                ? conductor.crotchetAtStart * conductor.adjustedCountdownTicks
                : 0.0;
            pending = lead > 0.0;
        } catch(Exception e) { Diag.Ignore(e); }
    }
    internal static float Restore(float scrubTime) {
        if(!pending) return scrubTime;
        pending = false;
        return (float)Math.Max(scrubTime - lead, earliest);
    }
}

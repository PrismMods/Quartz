using System.Collections.Generic;
using UnityEngine;
namespace Quartz.Features.Editor;
public static partial class EditorFeature {
    internal static bool ShouldHideForBga => Enabled && Conf.BgaMod && IsPlaying;
    private static bool IsPlaying {
        get {
            if(ADOBase.isLevelEditor) {
                scnEditor ed = scnEditor.instance;
                return ed != null && ed.playMode;
            }
            scrController c = ADOBase.controller;
            return c != null && c.gameworld;
        }
    }
    private static bool bgaApplied;
    private static void ReconcileBga() {
        bool want;
        try { want = ShouldHideForBga; }
        catch { return; }
        if(!want) {
            if(bgaApplied) {
                try {
                    SetFloorsVisible(true);
                    SetPlanetsVisible(true);
                } catch {
                }
                bgaApplied = false;
            }
            ReconcileBgaDecorations(false);
            return;
        }
        try {
            SetPlanetsVisible(false);
            List<scrFloor> floors = ADOBase.lm?.listFloors;
            if(floors != null && floors.Count > 0 && (!bgaApplied || FloorsLookVisible(floors)))
                SetFloorsVisible(floors, false);
            bgaApplied = true;
        } catch {
        }
        ReconcileBgaDecorations(true);
    }
    private static void RestoreBga() {
        if(bgaApplied) {
            try {
                SetFloorsVisible(true);
                SetPlanetsVisible(true);
            } catch {
            }
            bgaApplied = false;
        }
        ReconcileBgaDecorations(false);
    }
    private static bool FloorsLookVisible(List<scrFloor> floors) {
        return FloorBodyEnabled(floors[0]) || FloorBodyEnabled(floors[floors.Count - 1]);
    }
    private static bool FloorBodyEnabled(scrFloor floor) {
        Renderer r = floor != null && floor.floorRenderer != null ? floor.floorRenderer.renderer : null;
        return r != null && r.enabled;
    }
    private static void SetFloorsVisible(bool visible)
        => SetFloorsVisible(ADOBase.lm?.listFloors, visible);
    private static void SetFloorsVisible(List<scrFloor> floors, bool visible) {
        if(floors == null) return;
        foreach(scrFloor floor in floors) {
            if(floor == null) continue;
            if(floor.floorRenderer != null) Set(floor.floorRenderer.renderer, visible);
            Set(floor.legacyFloorSpriteRenderer, visible);
            Set(floor.iconsprite, visible);
            Set(floor.outlineSprite, visible);
            Set(floor.topGlow, visible);
            Set(floor.bottomGlow, visible);
            Set(floor.multiplanetLine, visible);
            if(floor.holdRenderer != null) Set(floor.holdRenderer.m_meshRenderer, visible);
        }
    }
    private static void SetPlanetsVisible(bool visible) {
        scrPlayerManager pm = ADOBase.playerManager;
        if(pm != null && pm.players != null) {
            foreach(scrPlayer player in pm.players) {
                List<scrPlanet> planets = player != null && player.planetarySystem != null
                    ? player.planetarySystem.planetList
                    : null;
                if(planets == null) continue;
                foreach(scrPlanet planet in planets)
                    if(planet != null) SetPlanetRendererVisible(planet.planetRenderer, visible);
            }
        }
        List<PlanetRenderer> dummies = ADOBase.controller != null ? ADOBase.controller.dummyPlanets : null;
        if(dummies != null) {
            foreach(PlanetRenderer dummy in dummies)
                SetPlanetRendererVisible(dummy, visible);
        }
    }
    private static void SetPlanetRendererVisible(PlanetRenderer pr, bool visible) {
        if(pr == null) return;
        Set(pr.sprite != null ? pr.sprite.meshRenderer : null, visible);
        Set(ParticleRenderer(pr.coreParticles), visible);
        Set(ParticleRenderer(pr.tailParticles), visible);
        Set(ParticleRenderer(pr.sparks), visible);
        Set(pr.ring, visible);
        Set(pr.glow, visible);
        Set(pr.faceSprite, visible);
        Set(pr.faceDetails, visible);
        Set(pr.samuraiSprite, visible);
    }
    private static Renderer ParticleRenderer(ParticleSystem ps)
        => ps != null ? ps.GetComponent<Renderer>() : null;
    private static void Set(Renderer r, bool visible) {
        if(r != null && r.enabled != visible) r.enabled = visible;
    }
    private enum DecoKind { Tile, Planet }
    private static readonly HashSet<scrDecoration> bgaTileDecos = new();
    private static readonly HashSet<scrDecoration> bgaPlanetDecos = new();
    private static void ReconcileBgaDecorations(bool bgaActive) {
        try {
            UpdateDecoSet(bgaTileDecos, bgaActive && Conf.BgaHideTileDeco, DecoKind.Tile);
            UpdateDecoSet(bgaPlanetDecos, bgaActive && Conf.BgaHidePlanetDeco, DecoKind.Planet);
        } catch {
        }
    }
    private static void UpdateDecoSet(HashSet<scrDecoration> hidden, bool hide, DecoKind kind) {
        if(hide) {
            scrDecorationManager mgr = scrDecorationManager.instance;
            List<scrDecoration> all = mgr != null ? mgr.allDecorations : null;
            if(all == null) return;
            foreach(scrDecoration deco in all) {
                if(deco == null || !Matches(deco, kind) || !deco.GetVisible()) continue;
                deco.forceHide = true;
                deco.SetVisible(false);
                hidden.Add(deco);
            }
        } else if(hidden.Count > 0) {
            foreach(scrDecoration deco in hidden) {
                if(deco != null) {
                    deco.forceHide = false;
                    deco.SetVisible(true);
                }
            }
            hidden.Clear();
        }
    }
    private static bool Matches(scrDecoration deco, DecoKind kind) {
        DecPlacementType p = deco.placementType;
        return kind == DecoKind.Tile
            ? p == DecPlacementType.Tile
            : p == DecPlacementType.RedPlanet
                || p == DecPlacementType.BluePlanet
                || p == DecPlacementType.GreenPlanet;
    }
}

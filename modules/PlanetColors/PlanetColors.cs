using System.Reflection;
using HarmonyLib;
using Quartz.Core;
using Quartz.IO;
using UnityEngine;
using Quartz.Compat.Game;
namespace Quartz.Features.PlanetColors;
public static partial class PlanetColors {
    public static SettingsFile<PlanetColorsSettings> ConfMgr { get; private set; }
    public static PlanetColorsSettings Conf => ConfMgr?.Data;
    public static void EnsureConf() => ConfMgr ??= SettingsFile<PlanetColorsSettings>.Loaded("PlanetColors.json");
    public static void Save() => ConfMgr?.RequestSave();
    private static bool ShouldChange {
        get {
            EnsureConf();
            return MainCore.IsModEnabled && Conf.Enabled;
        }
    }
    private static bool applying;
    private static readonly Dictionary<int, int> rendererSlots = [];
    private static readonly Dictionary<string, MethodInfo> colorMethodCache = [];
    private static readonly object[] colorInvokeArgs = new object[1];
    private static MethodInfo setParticleSystemColorMethod;
    private static Action<PlanetRenderer, ParticleSystem, Color, Color> setParticleSystemColorFast;
    private static bool setParticleSystemColorResolved;
    private static readonly object[] particleColorInvokeArgs = new object[3];
    private static bool ringAccessorsResolved;
    private static AccessTools.FieldRef<PlanetRenderer, bool> onlyRingRef;
    private static readonly scrPlanet[] EmptyPlanets = [];
    private static PlanetarySystem cachedSystem;
    private static int cachedSystemCount = -1;
    private static scrPlanet[] cachedSystemPlanets;
    private static readonly Color TailStartColorMultiplier = new(0.5f, 0.5f, 0.5f, 1f);
    public static void ClearSceneCaches() {
        InvalidatePlanetCache();
        rendererSlots.Clear();
        ClearOverlayCaches();
    }
    private static void InvalidatePlanetCache() {
        cachedSystem = null;
        cachedSystemCount = -1;
        cachedSystemPlanets = null;
    }
    private static T[] FindObjectsCompat<T>() where T : UnityEngine.Object
        => UnityEngine.Object.FindObjectsByType<T>(FindObjectsSortMode.None);
    private static scrPlanet[] GetSystemPlanets(PlanetarySystem system) {
        if(system == null) return EmptyPlanets;
        int count = system.allPlanets != null ? system.allPlanets.Count : 0;
        if(cachedSystemPlanets != null && cachedSystem == system && cachedSystemCount == count)
            return cachedSystemPlanets;
        cachedSystem = system;
        cachedSystemCount = count;
        cachedSystemPlanets = system.allPlanets != null && count > 0
            ? [.. system.allPlanets]
            : EmptyPlanets;
        return cachedSystemPlanets;
    }
    private static scrPlanet[] GetPlanets() {
        try {
            PlanetarySystem system = GameApi.Planetary(ADOBase.controller);
            scrPlanet[] planets = GetSystemPlanets(system);
            if(planets.Length > 0) return planets;
        } catch(Exception e) { Diag.Ignore(e); }
        try { return FindObjectsCompat<scrPlanet>(); }
        catch(Exception e) { Diag.Ignore(e); return EmptyPlanets; }
    }
    private static bool IsRedPlanet(scrPlanet planet) {
        try {
            PlanetarySystem system = planet != null ? planet.planetarySystem : null;
            system ??= GameApi.Planetary(ADOBase.controller);
            if(system != null) {
                if(system.planetRed == planet) return true;
                if(system.planetBlue == planet) return false;
            }
        } catch(Exception e) { Diag.Ignore(e); }
        try { return planet != null && planet.planetIndex == 0; }
        catch(Exception e) { Diag.Ignore(e); return true; }
    }
    private static int GetPlanetSlot(scrPlanet planet) {
        if(planet == null) return 0;
        try {
            PlanetarySystem system = planet.planetarySystem;
            system ??= GameApi.Planetary(ADOBase.controller);
            if(system != null) {
                if(system.planetRed == planet) return 0;
                if(system.planetBlue == planet) return 1;
                int index = system.allPlanets != null ? system.allPlanets.IndexOf(planet) : -1;
                if(index >= 0) return Mathf.Clamp(index, 0, PlanetColorsSettings.Slots - 1);
            }
        } catch(Exception e) { Diag.Ignore(e); }
        try { return Mathf.Clamp(planet.planetIndex, 0, PlanetColorsSettings.Slots - 1); }
        catch(Exception e) { Diag.Ignore(e); return 0; }
    }
    private static int GetPlanetSlot(PlanetRenderer renderer) {
        if(renderer == null) return 0;
        int rendererId = renderer.GetInstanceID();
        if(rendererSlots.TryGetValue(rendererId, out int slot))
            return Mathf.Clamp(slot, 0, PlanetColorsSettings.Slots - 1);
        scrPlanet planet = FindPlanetForRenderer(renderer);
        slot = planet == null ? 0 : GetPlanetSlot(planet);
        rendererSlots[rendererId] = slot;
        return slot;
    }
    private static scrPlanet FindPlanetForRenderer(PlanetRenderer renderer) {
        if(renderer == null) return null;
        scrPlanet[] planets = GetPlanets();
        for(int i = 0; i < planets.Length; i++) {
            scrPlanet planet = planets[i];
            if(planet == null) continue;
            try {
                if(planet.planetRenderer == renderer) return planet;
            } catch(Exception e) { Diag.Ignore(e); }
        }
        return null;
    }
    private static void RememberRendererSlot(scrPlanet planet) {
        if(planet == null) return;
        try {
            if(planet.planetRenderer != null)
                rendererSlots[planet.planetRenderer.GetInstanceID()] = GetPlanetSlot(planet);
        } catch(Exception e) { Diag.Ignore(e); }
    }
    private static Color BallColor(int slot) => Conf.GetBallColor(slot);
    private static Color TailColor(int slot) => Conf.GetTailColor(slot);
    public static void Refresh() {
        if(!ShouldChange) {
            DisableAllOverlays();
            return;
        }
        scrPlanet[] planets = GetPlanets();
        for(int i = 0; i < planets.Length; i++)
            ApplyPlanetColor(planets[i]);
        if(!OverlayEnabled) DisableAllOverlays();
        try { ApplyLogoColor(scrLogoText.instance); } catch(Exception e) { Diag.Ignore(e); }
    }
    public static void Restore() {
        DisableAllOverlays();
        scrPlanet[] planets = GetPlanets();
        for(int i = 0; i < planets.Length; i++) {
            scrPlanet planet = planets[i];
            if(planet == null || planet.planetRenderer == null) continue;
            bool wasApplying = applying;
            applying = true;
            try { planet.planetRenderer.LoadPlanetColor(IsRedPlanet(planet)); }
            catch(Exception e) { Diag.Ignore(e); }
            finally { applying = wasApplying; }
        }
        try { scrLogoText.instance?.UpdateColors(); } catch(Exception e) { Diag.Ignore(e); }
        rendererSlots.Clear();
    }
    private static MethodInfo logoColorMethod;
    private static bool logoColorMethodResolved;
    private static readonly object[] logoColorInvokeArgs = new object[2];
    private static Color LogoColor {
        get {
            Color ball = Conf.GetBallColor(0);
            return new Color(ball.r, ball.g, ball.b, 1f);
        }
    }
    private static void ApplyLogoColor(scrLogoText logoText) {
        if(logoText == null || !ShouldChange) return;
        Color color = LogoColor;
        InvokeLogoColor(logoText, color, true);
        InvokeLogoColor(logoText, color, false);
    }
    private static void InvokeLogoColor(scrLogoText logoText, Color color, bool isFire) {
        try {
            MethodInfo method = GetLogoColorMethod(logoText.GetType());
            if(method == null) return;
            logoColorInvokeArgs[0] = color;
            logoColorInvokeArgs[1] = isFire;
            method.Invoke(logoText, logoColorInvokeArgs);
        } catch(Exception e) { Diag.Ignore(e); }
    }
    private static MethodInfo GetLogoColorMethod(Type type) {
        if(logoColorMethodResolved) return logoColorMethod;
        logoColorMethodResolved = true;
        if(type == null) return null;
        MethodInfo[] methods = type.GetMethods(MemberFlags);
        for(int i = 0; i < methods.Length; i++) {
            MethodInfo method = methods[i];
            if(method.Name != "ColorLogo") continue;
            ParameterInfo[] parameters = method.GetParameters();
            if(parameters.Length != 2 || parameters[1].ParameterType != typeof(bool)) continue;
            Type colorType = parameters[0].ParameterType;
            if(colorType == typeof(Color) || Nullable.GetUnderlyingType(colorType) == typeof(Color)) {
                logoColorMethod = method;
                return logoColorMethod;
            }
        }
        return null;
    }
    private static void ApplyPlanetColor(scrPlanet planet) {
        if(planet == null) return;
        RememberRendererSlot(planet);
        ApplyPlanetRendererColor(planet.planetRenderer, GetPlanetSlot(planet));
    }
    private static void ApplyPlanetRendererColor(PlanetRenderer renderer)
        => ApplyPlanetRendererColor(renderer, GetPlanetSlot(renderer));
    private static void ApplyPlanetRendererColor(PlanetRenderer renderer, int slot) {
        if(renderer == null || !ShouldChange || applying) return;
        applying = true;
        try {
            slot = Mathf.Clamp(slot, 0, PlanetColorsSettings.Slots - 1);
            Color ballColor = BallColor(slot);
            Color tailColor = TailColor(slot);
            try { renderer.DisableAllSpecialPlanets(); } catch(Exception e) { Diag.Ignore(e); }
            try {
                if(renderer.sprite != null && ADOBase.gc != null && ADOBase.gc.tex_planetWhite != null)
                    renderer.sprite.sprite = ADOBase.gc.tex_planetWhite;
            } catch(Exception e) { Diag.Ignore(e); }
            try { renderer.SetPlanetColor(ballColor); } catch(Exception e) { Diag.Ignore(e); }
            try { renderer.SetTailColor(tailColor); } catch(Exception e) { Diag.Ignore(e); }
            ApplyTailParticleColor(renderer, tailColor);
            try { renderer.SetCoreColor(ballColor); } catch(Exception e) { Diag.Ignore(e); }
            InvokeRendererColor(renderer, "SetFaceColor", ballColor);
            ApplyPlanetGlowColor(renderer, ballColor);
            ApplyOverlayToPlanet(renderer, slot);
        } finally {
            applying = false;
        }
    }
    private static void ApplyPlanetGlowColor(PlanetRenderer renderer, Color color) {
        try {
            SpriteRenderer glow = renderer.glow;
            if(glow == null) return;
            Color next = color;
            next.a = glow.color.a;
            if(glow.color != next) glow.color = next;
        } catch(Exception e) { Diag.Ignore(e); }
    }
    private static void ApplyTailParticleColor(PlanetRenderer renderer, Color tailColor) {
        if(renderer == null) return;
        Color startColor = tailColor * TailStartColorMultiplier;
        ParticleSystem tail = GetParticles(renderer, "tailParticles");
        ParticleSystem tailCoop = GetParticles(renderer, "tailParticlesCoop");
        ApplyTailParticleSystemColor(renderer, tail, tailColor, startColor);
        if(tailCoop != tail) ApplyTailParticleSystemColor(renderer, tailCoop, tailColor, startColor);
    }
    private static void ApplyTailParticleSystemColor(PlanetRenderer renderer, ParticleSystem particles, Color baseColor, Color startColor) {
        if(renderer == null || particles == null) return;
        try {
            EnsureSetParticleSystemColorMethod();
            if(setParticleSystemColorFast != null) {
                setParticleSystemColorFast(renderer, particles, baseColor, startColor);
                return;
            }
            if(setParticleSystemColorMethod != null) {
                particleColorInvokeArgs[0] = particles;
                particleColorInvokeArgs[1] = baseColor;
                particleColorInvokeArgs[2] = startColor;
                setParticleSystemColorMethod.Invoke(renderer, particleColorInvokeArgs);
                return;
            }
        } catch(Exception e) { Diag.Ignore(e); }
        try {
            ParticleSystem.MainModule main = particles.main;
            main.startColor = new ParticleSystem.MinMaxGradient(startColor);
        } catch(Exception e) { Diag.Ignore(e); }
    }
    private static void EnsureSetParticleSystemColorMethod() {
        if(setParticleSystemColorResolved) return;
        setParticleSystemColorResolved = true;
        setParticleSystemColorMethod = AccessTools.Method(
            typeof(PlanetRenderer),
            "SetParticleSystemColor",
            [typeof(ParticleSystem), typeof(Color), typeof(Color)]
        );
        if(setParticleSystemColorMethod == null) return;
        try {
            setParticleSystemColorFast = (Action<PlanetRenderer, ParticleSystem, Color, Color>)Delegate.CreateDelegate(
                typeof(Action<PlanetRenderer, ParticleSystem, Color, Color>), setParticleSystemColorMethod);
        } catch(Exception e) {
            Diag.Ignore(e);
            setParticleSystemColorFast = null;
        }
    }
    private static void ApplyPlanetRing(PlanetRenderer renderer) {
        if(!ShouldChange || renderer == null) return;
        if(IsOnlyRing(renderer)) return;
        try {
            if(Conf.EnableRingRecolor) {
                GameApi.SetRingColor(
                    renderer,
                    Conf.SeparateRingColor ? Conf.GetRingColor(GetPlanetSlot(renderer)) : Conf.GetRingColor()
                );
                return;
            }
            if(!GameApi.TryGetRingColor(renderer, out Color current) || current.a == 0f) return;
            current.a = 0f;
            GameApi.SetRingColor(renderer, current);
        } catch(Exception e) { Diag.Ignore(e); }
    }
    private static void EnsureRingAccessors() {
        if(ringAccessorsResolved) return;
        ringAccessorsResolved = true;
        try {
            onlyRingRef = AccessTools.FieldRefAccess<PlanetRenderer, bool>("onlyRing");
        } catch(Exception e) { Diag.Ignore(e); }
    }
    private static bool IsOnlyRing(PlanetRenderer renderer) {
        EnsureRingAccessors();
        if(onlyRingRef != null) return onlyRingRef(renderer);
        return TryGetMemberValue(renderer, "onlyRing", out object onlyRing) && onlyRing is bool b && b;
    }
    private static ParticleSystem GetParticles(PlanetRenderer renderer, string name)
        => TryGetMemberValue(renderer, name, out object value) ? value as ParticleSystem : null;
    private static void InvokeRendererColor(PlanetRenderer renderer, string methodName, Color color) {
        try {
            if(!colorMethodCache.TryGetValue(methodName, out MethodInfo method)) {
                method = AccessTools.Method(typeof(PlanetRenderer), methodName, [typeof(Color)]);
                colorMethodCache[methodName] = method;
            }
            if(method != null) {
                colorInvokeArgs[0] = color;
                method.Invoke(renderer, colorInvokeArgs);
            }
        } catch(Exception e) { Diag.Ignore(e); }
    }
    private const BindingFlags MemberFlags =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
    private static bool TryGetMemberValue(object target, string name, out object value) =>
        Refl.TryRead(target, name, out value);
}

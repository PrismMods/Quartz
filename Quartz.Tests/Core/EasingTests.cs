using GTweens.Easings;
using static Asserts;
static class EasingTests {
    private static readonly Easing[] All = (Easing[])Enum.GetValues(typeof(Easing));
    public static void TestEveryEasingHitsBothEndpoints() {
        foreach(Easing easing in All) {
            EasingDelegate ease = PresetEasingDelegateFactory.GetEaseDelegate(easing);
            Assert(ease != null, $"{easing} has a delegate");
            Assert(Math.Abs(ease(10f, 20f, 0f) - 10f) < 0.001f, $"{easing} starts at the from value");
            Assert(Math.Abs(ease(10f, 20f, 1f) - 20f) < 0.001f, $"{easing} ends at the to value");
        }
    }
    public static void TestEveryEasingStaysFiniteAcrossTheRange() {
        foreach(Easing easing in All) {
            EasingDelegate ease = PresetEasingDelegateFactory.GetEaseDelegate(easing);
            for(int step = 0; step <= 100; step++) {
                float value = ease(0f, 1f, step / 100f);
                Assert(!float.IsNaN(value) && !float.IsInfinity(value), $"{easing} at t={step / 100f} is finite");
            }
        }
    }
    public static void TestLinearIsExactAndUnknownFallsBackToIt() {
        EasingDelegate linear = PresetEasingDelegateFactory.GetEaseDelegate(Easing.Linear);
        Assert(Math.Abs(linear(0f, 100f, 0.25f) - 25f) < 0.001f, "linear quarter");
        Assert(Math.Abs(linear(0f, 100f, 0.5f) - 50f) < 0.001f, "linear half");
        Assert(Math.Abs(linear(-50f, 50f, 0.5f) - 0f) < 0.001f, "linear across zero");
        EasingDelegate unknown = PresetEasingDelegateFactory.GetEaseDelegate((Easing)9999);
        Assert(Math.Abs(unknown(0f, 100f, 0.25f) - 25f) < 0.001f, "an unmapped easing behaves as linear");
    }
    public static void TestInAndOutCurvesSitOnOppositeSidesOfLinear() {
        (Easing In, Easing Out)[] pairs = [
            (Easing.InSine, Easing.OutSine),
            (Easing.InQuad, Easing.OutQuad),
            (Easing.InCubic, Easing.OutCubic),
            (Easing.InQuart, Easing.OutQuart),
            (Easing.InQuint, Easing.OutQuint),
            (Easing.InExpo, Easing.OutExpo),
            (Easing.InCirc, Easing.OutCirc),
        ];
        foreach((Easing easeIn, Easing easeOut) in pairs) {
            float inHalf = PresetEasingDelegateFactory.GetEaseDelegate(easeIn)(0f, 1f, 0.5f);
            float outHalf = PresetEasingDelegateFactory.GetEaseDelegate(easeOut)(0f, 1f, 0.5f);
            Assert(inHalf < 0.5f, $"{easeIn} eases in, so it lags linear at the midpoint");
            Assert(outHalf > 0.5f, $"{easeOut} eases out, so it leads linear at the midpoint");
        }
    }
    public static void TestMonotonicCurvesNeverGoBackwards() {
        Easing[] monotonic = [
            Easing.Linear,
            Easing.InSine, Easing.OutSine, Easing.InOutSine,
            Easing.InQuad, Easing.OutQuad, Easing.InOutQuad,
            Easing.InCubic, Easing.OutCubic, Easing.InOutCubic,
            Easing.InQuart, Easing.OutQuart, Easing.InOutQuart,
            Easing.InQuint, Easing.OutQuint, Easing.InOutQuint,
            Easing.InExpo, Easing.OutExpo,
            Easing.InCirc, Easing.OutCirc, Easing.InOutCirc,
        ];
        foreach(Easing easing in monotonic) {
            EasingDelegate ease = PresetEasingDelegateFactory.GetEaseDelegate(easing);
            float previous = ease(0f, 1f, 0f);
            for(int step = 1; step <= 100; step++) {
                float value = ease(0f, 1f, step / 100f);
                Assert(value >= previous - 0.0001f, $"{easing} must not reverse between {(step - 1) / 100f} and {step / 100f}");
                previous = value;
            }
        }
    }
    public static void TestBounceLandsOnItsPlateaus() {
        EasingDelegate outBounce = PresetEasingDelegateFactory.GetEaseDelegate(Easing.OutBounce);
        Assert(outBounce(0f, 1f, 1f) > 0.999f, "out bounce settles at 1");
        bool dipped = false;
        float peak = 0f;
        for(int step = 0; step <= 100; step++) {
            float value = outBounce(0f, 1f, step / 100f);
            if(value > peak) peak = value;
            else if(peak > 0.3f) dipped = true;
        }
        Assert(dipped, "out bounce must actually bounce back down at least once");
        Assert(peak <= 1.0001f, "out bounce never overshoots 1");
    }
    public static void TestBackCurvesOvershootOnPurpose() {
        Assert(PresetEasingDelegateFactory.GetEaseDelegate(Easing.InBack)(0f, 1f, 0.2f) < 0f,
            "in back dips below the start before moving");
        Assert(PresetEasingDelegateFactory.GetEaseDelegate(Easing.OutBack)(0f, 1f, 0.8f) > 1f,
            "out back passes the target before settling");
    }
}

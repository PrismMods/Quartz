using ADOFAI;
using Quartz.Core;
namespace Quartz.Features.KeyLimiter;
public sealed class FfxKeyLimiter : ffxPlusBase {
    private bool prepared;
    private bool limitActive;
    private int limit = int.MaxValue;
    private KeyExceedMethod exceedMethod = KeyExceedMethod.Ignore;
    private string[] targetTags = [];
    private RunEventBehaviour runBehaviour = RunEventBehaviour.Once;
    private string message = "";
    public override void Decode(LevelEvent evnt) {
        if(evnt == null) return;
        limitActive = evnt.GetBool(ChartKeyLimiter.PropEnabled);
        limit = evnt.GetInt(ChartKeyLimiter.PropLimit);
        exceedMethod = evnt.Get(ChartKeyLimiter.PropExceed, KeyExceedMethod.Ignore);
        targetTags = ChartEventTags.Split(evnt.Get<string>(ChartKeyLimiter.PropTargetTag, null));
        runBehaviour = evnt.Get(ChartKeyLimiter.PropRunBehaviour, RunEventBehaviour.Once);
        message = evnt.GetString(ChartKeyLimiter.PropMessage);
    }
    public override void PrepVfx() {
        if(prepared) return;
        prepared = true;
        if(!RunsEvents()) return;
        try {
            foreach(ffxPlusBase effect in ChartEventTags.Tagged(this, targetTags)) {
                effect.triggered = true;
                effect.runManually = true;
            }
        } catch(Exception e) {
            Diag.Warn(e, "claiming the events tagged by a chart key limiter");
        }
    }
    public override void StartEffect(scrPlanet planet) =>
        ChartKeyLimiterState.Instance.Set(
            this, limitActive, limit, exceedMethod, targetTags, runBehaviour, message);
    private bool RunsEvents() => exceedMethod is KeyExceedMethod.RunEvent
        or KeyExceedMethod.IgnoreAndRunEvent
        or KeyExceedMethod.KillAndRunEvent;
}

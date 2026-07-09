using GTweens.Easings;
using GTweens.Enums;
using GTweens.TweenBehaviours;
namespace GTweens.Tweens;
public sealed class GTween {
    public event Action OnStartAction;
    public event Action OnTickAction;
    public event Action OnLoopAction;
    public event Action OnResetAction;
    public event Action OnCompleteAction;
    public event Action OnKillAction;
    public event Action OnCompleteOrKillAction;
    public event Action<float> OnTimeScaleChangedAction;
    public ITweenBehaviour Behaviour { get; }
    public float TimeScale { get; private set; } = 1;
    public float Delay { get; private set; }
    public int Loops { get; private set; }
    public ResetMode LoopResetMode { get; private set; }
    public bool IsNested { get; set; }
    public bool IsPlaying { get; private set; }
    public bool IsPaused { get; private set; }
    public bool IsCompleted { get; private set; }
    public bool IsKilled { get; private set; }
    public bool IsCompletedOrKilled => IsCompleted || IsKilled;
    public bool IsAlive { get; set; }
    float _delayRemaining;
    int _loopsRemaining;
    public GTween(ITweenBehaviour behaviour) => Behaviour = behaviour;
    public void Start(bool isCompletingInstantly = false) {
        if(IsPlaying) Kill();
        IsPlaying = true;
        IsCompleted = false;
        IsKilled = false;
        _delayRemaining = Delay;
        _loopsRemaining = Loops;
        Behaviour.Start(isCompletingInstantly);
        OnStartAction?.Invoke();
    }
    public void Tick(float deltaTime) {
        if(!IsPlaying || IsPaused) return;
        float deltaTimeWithTimeScale = TimeScale * deltaTime;
        if(_delayRemaining > 0f) {
            _delayRemaining -= deltaTime;
            return;
        }
        Behaviour.Tick(deltaTimeWithTimeScale);
        OnTickAction?.Invoke();
        if(!Behaviour.GetFinished()) return;
        bool needsToLoop = _loopsRemaining > 0 && Behaviour.GetLoopable();
        if(needsToLoop) {
            Loop(LoopResetMode);
        } else {
            MarkFinished();
        }
    }
    public GTween SetPaused(bool paused) {
        IsPaused = paused;
        return this;
    }
    public void Complete() {
        if(!IsPlaying && !IsCompleted) Start(true);
        Behaviour.Complete();
        _loopsRemaining = 0;
        MarkFinished();
    }
    public void Kill() {
        if(!IsPlaying) return;
        IsPlaying = false;
        IsCompleted = false;
        IsKilled = true;
        Behaviour.Kill();
        OnKillAction?.Invoke();
        OnCompleteOrKillAction?.Invoke();
    }
    public GTween Reset(bool kill, ResetMode resetMode = ResetMode.InitialValues) {
        if(kill) {
            Kill();
            IsPlaying = false;
        }
        IsCompleted = false;
        IsKilled = false;
        _delayRemaining = Delay;
        Behaviour.Reset(kill, resetMode);
        OnResetAction?.Invoke();
        return this;
    }
    public GTween Simulate(float time) {
        if(!IsPlaying) Start();
        float simulationTime = Loops > 0 ?
            time % Behaviour.GetDuration() :
            Math.Min(time, Behaviour.GetDuration());
        float progress = Behaviour.GetElapsed();
        while(simulationTime > 0) {
            Tick(simulationTime);
            float newProgress = Behaviour.GetElapsed();
            float tickElapsed = newProgress - progress;
            progress = newProgress;
            simulationTime -= tickElapsed;
        }
        return this;
    }
    public GTween SetDelay(float delaySeconds) {
        Delay = delaySeconds;
        return this;
    }
    public GTween SetTimeScale(float timeScale) {
        TimeScale = timeScale;
        OnTimeScaleChangedAction?.Invoke(timeScale);
        return this;
    }
    public GTween SetEasing(EasingDelegate easingFunction) {
        Behaviour.SetEasing(easingFunction);
        return this;
    }
    public GTween SetEasing(Easing easing) => SetEasing(PresetEasingDelegateFactory.GetEaseDelegate(easing));
    public GTween SetLoops(int loops, ResetMode resetMode = ResetMode.InitialValues) {
        Loops = Math.Max(loops, 0);
        LoopResetMode = resetMode;
        return this;
    }
    public GTween SetMaxLoops(ResetMode resetMode = ResetMode.InitialValues) => SetLoops(int.MaxValue, resetMode);
    public float GetDuration() => Behaviour.GetDuration();
    public float GetElapsed() {
        if(!IsPlaying && !IsCompleted) return 0f;
        if(!IsPlaying && IsCompleted) return GetDuration();
        return Behaviour.GetElapsed();
    }
    public float GetRemaining() {
        if(!IsPlaying && !IsCompleted) return GetDuration();
        if(!IsPlaying && IsCompleted) return 0f;
        return Behaviour.GetRemaining();
    }
    public GTween OnStart(Action action) {
        OnStartAction += action;
        return this;
    }
    public GTween OnTick(Action action) {
        OnTickAction += action;
        return this;
    }
    public GTween OnLoop(Action action) {
        OnLoopAction += action;
        return this;
    }
    public GTween OnReset(Action action) {
        OnResetAction += action;
        return this;
    }
    public GTween OnComplete(Action action) {
        OnCompleteAction += action;
        return this;
    }
    public GTween OnKill(Action action) {
        OnKillAction += action;
        return this;
    }
    public GTween OnCompleteOrKill(Action action) {
        OnCompleteOrKillAction += action;
        return this;
    }
    public GTween OnTimeScaleChanged(Action<float> action) {
        OnTimeScaleChangedAction += action;
        return this;
    }
    void Loop(ResetMode loopResetMode) {
        if(!(_loopsRemaining > 0) || !Behaviour.GetLoopable()) return;
        --_loopsRemaining;
        Reset(kill: false, loopResetMode);
        IsPlaying = true;
        IsCompleted = false;
        IsKilled = false;
        Behaviour.Start(false);
        OnLoopAction?.Invoke();
    }
    void MarkFinished() {
        if(!IsPlaying) return;
        IsPlaying = false;
        IsCompleted = true;
        IsKilled = false;
        Behaviour.Complete();
        OnCompleteAction?.Invoke();
        OnCompleteOrKillAction?.Invoke();
    }
}

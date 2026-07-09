using GTweens.Delegates;
using GTweens.TweenBehaviours;
using GTweens.Tweeners;
using GTweens.Tweens;
using System.Drawing;
using System.Numerics;
namespace GTweens.Extensions;
public static class GTweenExtensions {
    public static GTween Tween(
        Tweener<int>.Getter getter,
        Tweener<int>.Setter setter,
        Tweener<int>.Getter to,
        float duration,
        ValidationDelegates.Validation validation
    ) => FromTweener(new IntTweener(getter, setter, to, duration, validation));
    public static GTween Tween(
        Tweener<int>.Getter getter,
        Tweener<int>.Setter setter,
        int to,
        float duration,
        ValidationDelegates.Validation validation
    ) => Tween(getter, setter, () => to, duration, validation);
    public static GTween Tween(
        Tweener<int>.Getter getter,
        Tweener<int>.Setter setter,
        int to,
        float duration
    ) => Tween(getter, setter, () => to, duration, ValidationExtensions.AlwaysValid);
    public static GTween Tween(
        Tweener<float>.Getter getter,
        Tweener<float>.Setter setter,
        Tweener<float>.Getter to,
        float duration,
        ValidationDelegates.Validation validation
    ) => FromTweener(new FloatTweener(getter, setter, to, duration, validation));
    public static GTween Tween(
        Tweener<float>.Getter getter,
        Tweener<float>.Setter setter,
        float to,
        float duration,
        ValidationDelegates.Validation validation
    ) => Tween(getter, setter, () => to, duration, validation);
    public static GTween Tween(
        Tweener<float>.Getter getter,
        Tweener<float>.Setter setter,
        float to,
        float duration
    ) => Tween(getter, setter, to, duration, ValidationExtensions.AlwaysValid);
    public static GTween Tween(
        Tweener<Vector2>.Getter getter,
        Tweener<Vector2>.Setter setter,
        Tweener<Vector2>.Getter to,
        float duration,
        ValidationDelegates.Validation validation
    ) => FromTweener(new SystemVector2Tweener(getter, setter, to, duration, validation));
    public static GTween Tween(
        Tweener<Vector2>.Getter getter,
        Tweener<Vector2>.Setter setter,
        Vector2 to,
        float duration,
        ValidationDelegates.Validation validation
    ) => Tween(getter, setter, () => to, duration, validation);
    public static GTween Tween(
        Tweener<Vector2>.Getter getter,
        Tweener<Vector2>.Setter setter,
        Vector2 to,
        float duration
    ) => Tween(getter, setter, to, duration, ValidationExtensions.AlwaysValid);
    public static GTween Tween(
        Tweener<Vector3>.Getter getter,
        Tweener<Vector3>.Setter setter,
        Tweener<Vector3>.Getter to,
        float duration,
        ValidationDelegates.Validation validation
    ) => FromTweener(new SystemVector3Tweener(getter, setter, to, duration, validation));
    public static GTween Tween(
        Tweener<Vector3>.Getter getter,
        Tweener<Vector3>.Setter setter,
        Vector3 to,
        float duration,
        ValidationDelegates.Validation validation
    ) => Tween(getter, setter, () => to, duration, validation);
    public static GTween Tween(
        Tweener<Vector3>.Getter getter,
        Tweener<Vector3>.Setter setter,
        Vector3 to,
        float duration
    ) => Tween(getter, setter, to, duration, ValidationExtensions.AlwaysValid);
    public static GTween Tween(
        Tweener<Vector4>.Getter getter,
        Tweener<Vector4>.Setter setter,
        Tweener<Vector4>.Getter to,
        float duration,
        ValidationDelegates.Validation validation
    ) => FromTweener(new SystemVector4Tweener(getter, setter, to, duration, validation));
    public static GTween Tween(
        Tweener<Vector4>.Getter getter,
        Tweener<Vector4>.Setter setter,
        Vector4 to,
        float duration,
        ValidationDelegates.Validation validation
    ) => Tween(getter, setter, () => to, duration, validation);
    public static GTween Tween(
        Tweener<Vector4>.Getter getter,
        Tweener<Vector4>.Setter setter,
        Vector4 to,
        float duration
    ) => Tween(getter, setter, to, duration, ValidationExtensions.AlwaysValid);
    public static GTween Tween(
        Tweener<Color>.Getter getter,
        Tweener<Color>.Setter setter,
        Tweener<Color>.Getter to,
        float duration,
        ValidationDelegates.Validation validation
    ) => FromTweener(new SystemColorTweener(getter, setter, to, duration, validation));
    public static GTween Tween(
        Tweener<Color>.Getter getter,
        Tweener<Color>.Setter setter,
        Color to,
        float duration,
        ValidationDelegates.Validation validation
    ) => Tween(getter, setter, () => to, duration, validation);
    public static GTween Tween(
        Tweener<Color>.Getter getter,
        Tweener<Color>.Setter setter,
        Color to,
        float duration
    ) => Tween(getter, setter, to, duration, ValidationExtensions.AlwaysValid);
    public static GTween Tween(
        Tweener<Quaternion>.Getter getter,
        Tweener<Quaternion>.Setter setter,
        Tweener<Quaternion>.Getter to,
        float duration,
        ValidationDelegates.Validation validation
    ) => FromTweener(new SystemQuaternionTweener(getter, setter, to, duration, validation));
    private static GTween FromTweener(ITweener tweener) {
        InterpolationTweenBehaviour tweenBehaviour = new();
        tweenBehaviour.Add(tweener);
        return new GTween(tweenBehaviour);
    }
    public static GTween Tween(
        Tweener<Quaternion>.Getter getter,
        Tweener<Quaternion>.Setter setter,
        Quaternion to,
        float duration,
        ValidationDelegates.Validation validation
    ) => Tween(getter, setter, () => to, duration, validation);
    public static GTween Tween(
        Tweener<Quaternion>.Getter getter,
        Tweener<Quaternion>.Setter setter,
        Quaternion to,
        float duration
    ) => Tween(getter, setter, to, duration, ValidationExtensions.AlwaysValid);
    public static GTween Tween(
        int from,
        int to,
        Tweener<int>.Setter setter,
        float duration
    ) => Tween(() => from, setter, to, duration, ValidationExtensions.AlwaysValid);
    public static GTween Tween(
        float from,
        float to,
        Tweener<float>.Setter setter,
        float duration
    ) => Tween(() => from, setter, to, duration, ValidationExtensions.AlwaysValid);
    public static GTween TweenTimeScale(this GTween target, float to, float duration)
        => Tween(() => target.TimeScale, current => target.SetTimeScale(current), to, duration);
    public static bool IsPlayingOrCompleted(this GTween gTween) => gTween.IsPlaying || gTween.IsCompleted;
    public static bool IsPlayingOrCompletedOrNested(this GTween gTween) => gTween.IsPlaying || gTween.IsCompleted || gTween.IsNested;
    public static Task AwaitCompleteOrKill(this GTween gTween, CancellationToken cancellationToken) {
        TaskCompletionSource<bool> taskCompletionSource = new();
        if(!gTween.IsPlaying || cancellationToken.IsCancellationRequested) return Task.CompletedTask;
        void OnCompleteOrKill() {
            gTween.OnCompleteOrKillAction -= OnCompleteOrKill;
            taskCompletionSource.TrySetResult(true);
        }
        cancellationToken.Register(OnCompleteOrKill);
        gTween.OnCompleteOrKill(OnCompleteOrKill);
        return taskCompletionSource.Task;
    }
}

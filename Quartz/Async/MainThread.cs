using Quartz.Core;
using System.Collections.Concurrent;
using System.Diagnostics;
using UnityEngine;
namespace Quartz.Async;
public class MainThread : MonoBehaviour {
    private const long MaxDrainMillis = 4;
    private static readonly ConcurrentQueue<Action> queue = new();
    private static readonly Stopwatch drainClock = new();
    private static int pending;
    public static void Enqueue(Action action) {
        if(action == null) return;
        Interlocked.Increment(ref pending);
        queue.Enqueue(action);
    }
    private void Update() {
        int budget = Volatile.Read(ref pending);
        if(budget <= 0) return;
        drainClock.Restart();
        while(budget > 0 && queue.TryDequeue(out Action action)) {
            budget--;
            Interlocked.Decrement(ref pending);
            try {
                action();
            } catch(Exception e) {
                MainCore.Log.Err(e.ToString());
            }
            if(drainClock.ElapsedMilliseconds >= MaxDrainMillis) break;
        }
    }
}

using Quartz.Core;
using Quartz.IO;
using MonsterLove.StateMachine;
using SkyHook;
using System.Threading;
using UnityEngine;
namespace Quartz.Features.KeyLimiter;
internal static partial class KeyLimiter {
    private sealed class Ticker : MonoBehaviour {
        private readonly HashSet<KeyCode> prevHeld = [];
        private bool wasCapturing;
        private void Update() {
            InPlayerControl();
            // Release edges of injected keys (e.g. Tab) must be sampled every frame; the
            // game only calls CountValidKeysPressed on frames with fresh game-visible
            // input, so ChatterBlocker can't see releases on its own (consecutive-Tab bug).
            ChatterBlocker.ChatterBlocker.SampleInjectedKeyReleases();
            if(!IsCapturing) {
                wasCapturing = false;
                if(prevHeld.Count > 0) prevHeld.Clear();
                return;
            }
            bool priming = !wasCapturing;
            wasCapturing = true;
            KeyCode[] candidates = CaptureCandidates;
            for(int i = 0; i < candidates.Length; i++) {
                KeyCode key = candidates[i];
                bool held;
                try { held = UnityEngine.Input.GetKey(key); }
                catch { continue; }
                if(!held && IsHookTrackedKey(key)) held = HookKeyHeld(key);
                if(held && !priming && !prevHeld.Contains(key)) {
                    prevHeld.Add(key);
                    EndCapture(key);
                    return;
                }
                if(held) prevHeld.Add(key);
                else prevHeld.Remove(key);
            }
        }
    }
}

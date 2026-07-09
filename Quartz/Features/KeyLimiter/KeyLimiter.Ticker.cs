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
                if(!held && IsHookOnlyModifier(key)) held = HookKeyHeld(key);
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

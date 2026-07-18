using Quartz.Core;
using Quartz.Features.Panels;
using Quartz.Features.Status;
using Quartz.Resource;
using Quartz.UI;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
namespace Quartz.Features.KeyViewer;
public static partial class KeyViewerOverlay {
    private sealed class Updater : MonoBehaviour {
        /// <summary>
        /// The InGame veto is for a run being *played*, not for whichever scene the mod menu was
        /// opened over. gameworld && !paused can hold while the user sits in the menu — the
        /// result screen, a level idling behind it, a custom level's scnEditor playmode — and
        /// there the veto turned every layout edit into "doesn't apply until the game restarts"
        /// (nothing re-requests the rebuild once they leave the scene). Menu open means the
        /// user is in a menu: the rebuild hitch is fine, and it is their edit being applied.
        /// </summary>
        private static bool CanRebuild => !GameStats.InGame || Quartz.UI.UICore.IsOpen;
        private void LateUpdate() {
            // A finished font/image download triggers a full Rebuild — a visible
            // hitch. Hold the flag until the player is out of gameplay.
            if(cssDownloadArrived && CanRebuild) {
                cssDownloadArrived = false;
                if(Conf != null) {
                    Rebuild();
                    return;
                }
            }
            // Same reasoning as the download above: a layout edit costs a full Rebuild, so it
            // waits for the debounce to settle and for the player to be out of gameplay.
            if(layoutRebuildPending && Time.unscaledTime >= layoutRebuildAt && CanRebuild) {
                layoutRebuildPending = false;
                if(Conf != null) {
                    Rebuild();
                    return;
                }
            }
            if(cssFx.Count > 0 && root != null && root.gameObject.activeSelf) CssTick(Time.unscaledTime);
            if(counterBounces.Count > 0) TickCounterBounces(Time.unscaledTime);
        }
        /// <summary>
        /// DM Note's counter press animation: pop to the spec's scale on the press edge, ease
        /// back to 1 along its cubic-bezier. Scaling happens around the counter's visual centre —
        /// its pivot is layout-driven (often a corner), so the position is compensated per frame
        /// from a base captured at the pop.
        /// </summary>
        private static void TickCounterBounces(float now) {
            for(int i = counterBounces.Count - 1; i >= 0; i--) {
                Box box = counterBounces[i];
                DmNoteSpec spec = box.Dm;
                if(box.Value == null || spec == null) {
                    RemoveBounce(i);
                    continue;
                }
                float t = Mathf.Clamp01((now - box.BounceStart) * 1000f / spec.CounterAnimDurationMs);
                float eased = CubicBezierEase(spec.CounterAnimBezier, t);
                float scale = 1f + (spec.CounterAnimScale - 1f) * (1f - eased);
                RectTransform rt = box.Value.rectTransform;
                rt.localScale = new Vector3(scale, scale, 1f);
                Vector2 centerOffset = (new Vector2(0.5f, 0.5f) - rt.pivot) * rt.rect.size * (scale - 1f);
                rt.anchoredPosition = box.BounceBasePos - centerOffset;
                if(t >= 1f) {
                    rt.localScale = Vector3.one;
                    rt.anchoredPosition = box.BounceBasePos;
                    RemoveBounce(i);
                }
            }
        }
        private static void RemoveBounce(int index) {
            counterBounces[index].Bouncing = false;
            counterBounces.RemoveAt(index);
        }
        /// <summary>
        /// CSS cubic-bezier(x1, y1, x2, y2) easing: solve the curve parameter whose X is
        /// <paramref name="t"/> (Newton, with the curve's monotonic X), return its Y. Y control
        /// points may leave [0,1] — that is what makes an overshoot preset bounce.
        /// </summary>
        private static float CubicBezierEase(Vector4 b, float t) {
            if(t <= 0f) return 0f;
            if(t >= 1f) return 1f;
            float s = t;
            for(int i = 0; i < 6; i++) {
                float x = Bez(b.x, b.z, s) - t;
                if(Mathf.Abs(x) < 0.0005f) break;
                float dx = BezDeriv(b.x, b.z, s);
                if(Mathf.Abs(dx) < 0.0001f) break;
                s = Mathf.Clamp01(s - x / dx);
            }
            return Bez(b.y, b.w, s);
        }
        private static float Bez(float p1, float p2, float s) {
            float inv = 1f - s;
            return 3f * inv * inv * s * p1 + 3f * inv * s * s * p2 + s * s * s;
        }
        private static float BezDeriv(float p1, float p2, float s) {
            float inv = 1f - s;
            return 3f * inv * inv * p1 + 6f * inv * s * (p2 - p1) + 3f * s * s * (1f - p2);
        }
        private void Update() {
            if(root == null) return;
            float now = Time.unscaledTime;
            bool inGame = GameStats.InGame;
            if(gameStateKnown && wasInGame && !inGame) FlushCounts();
            wasInGame = inGame;
            gameStateKnown = true;
            bool isReorganizing = UICore.IsReorganizing;
            bool show = Panels.PanelsOverlay.IsEnabled && Conf.Enabled && (Conf.ShowOutsideGame || inGame) || isReorganizing;
            if(raycaster != null && raycaster.enabled != isReorganizing) raycaster.enabled = isReorganizing;
            if(root.gameObject.activeSelf != show) root.gameObject.SetActive(show);
            if(dragObj != null && dragObj.activeSelf != isReorganizing) dragObj.SetActive(isReorganizing);
            if(!show || !Application.isFocused) {
                MarkInputInactive(now, clearTransientStats: !show);
                TryFlushCounts(now, inGame);
                return;
            }
            if(isReorganizing) {
                Vector2 stored = OverlayCalibration.Unscale(root.anchoredPosition);
                Conf.DmOffsetX = stored.x;
                Conf.DmOffsetY = stored.y;
            }
            if(!InputReady(now)) {
                TryFlushCounts(now, inGame);
                return;
            }
            UpdateDmNote(now);
            TryFlushCounts(now, inGame);
        }
    }
}

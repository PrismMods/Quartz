using Quartz.Core;
using Quartz.Game.Stats;
using Quartz.Resource;
using Quartz.UI;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Quartz.Features.KeyViewer.Js;
using Quartz.Features.Overlay;
using Quartz.Overlay;
namespace Quartz.Features.KeyViewer;
public static partial class KeyViewerOverlay {
    private sealed class Updater : MonoBehaviour {
        // Not mid-run: the classic menu or the new one is open, or no level is playing.
        private static bool CanRebuild => !GameStats.InGame || UICore.MenuShowing;
        private bool focusKnown;
        private bool hasFocus = true;
        private void Awake() {
            hasFocus = Application.isFocused;
            focusKnown = true;
        }
        private void OnApplicationFocus(bool focus) {
            hasFocus = focus;
            focusKnown = true;
        }
        private void LateUpdate() {
            if(cssDownloadArrived && CanRebuild) {
                cssDownloadArrived = false;
                if(Conf != null) {
                    Rebuild();
                    return;
                }
            }
            if(layoutRebuildPending && KvClock.Now >= layoutRebuildAt && CanRebuild) {
                layoutRebuildPending = false;
                if(Conf != null) {
                    Rebuild();
                    return;
                }
            }
            if(cssFx.Count > 0 && root != null && root.gameObject.activeSelf) CssTick(KvClock.Now);
            if(counterBounces.Count > 0) TickCounterBounces(KvClock.Now);
        }
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
            KvJsRuntime.Tick(KvClock.Now);
            if(root == null) return;
            float now = KvClock.Now;
            bool inGame = GameStats.InGame;
            if(gameStateKnown && wasInGame && !inGame) FlushCounts();
            wasInGame = inGame;
            gameStateKnown = true;
            bool isReorganizing = UICore.IsReorganizing;
            bool arranging = UICore.IsArranging;
            // A preview shows the keys light, but a press counts only where the Key Viewer would show without it.
            counting = OverlaySwitch.Enabled && Conf.Enabled && (Conf.ShowOutsideGame || inGame) || arranging;
            bool show = counting || OverlayPreview.Is(FontKey);
            bool focused = focusKnown ? hasFocus : Application.isFocused;
            bool independent = Conf.Enabled && Conf.IndependentInput;
            KvInputQueue.SetWanted(independent);
            KvInputQueue.SetFocused(focused);
            KvInputQueue.Pump(now, independent);
            if(raycaster != null && raycaster.enabled != isReorganizing) raycaster.enabled = isReorganizing;
            if(root.gameObject.activeSelf != show) root.gameObject.SetActive(show);
            bool showFoot = show && footBuilt;
            if(footRoot != null && footRoot.gameObject.activeSelf != showFoot)
                footRoot.gameObject.SetActive(showFoot);
            bool showDrag = isReorganizing;
            if(arranging && !wasArranging) {
                Canvas.ForceUpdateCanvases();
                RefreshDragBounds();
            }
            wasArranging = arranging;
            if(dragObj != null && dragObj.activeSelf != showDrag) {
                if(showDrag) {
                    Canvas.ForceUpdateCanvases();
                    RefreshDragBounds();
                }
                dragObj.SetActive(showDrag);
            }
            bool showFootDrag = isReorganizing && footBuilt;
            if(footDragObj != null && footDragObj.activeSelf != showFootDrag)
                footDragObj.SetActive(showFootDrag);
            UpdateTabubVisibility(show, isReorganizing, arranging);
            if(!show || !focused) {
                MarkInputInactive(now, clearTransientStats: !show);
                TryFlushCounts(now, inGame);
                return;
            }
            // Arranging copies the RectTransform into Conf every frame: a shared-editor SetBox must move the RectTransform (or this capture skip shared sessions), or it is overwritten.
            if(arranging) CapturePosition();
            inputNow = now;
            // The new menu holds keys from the game while it is open; the Key Viewer's pane there must still light from them.
            if(OverlayPreview.Is(FontKey)) Quartz.Prism.PrismBridge.Exempt(inputStep ??= ReadInput);
            else ReadInput();
            TryFlushCounts(now, inGame);
        }
        private static float inputNow;
        private static Action inputStep;
        private static void ReadInput() {
            EnsureInputPrimed(inputNow);
            UpdateDmNote(inputNow);
        }
    }
}

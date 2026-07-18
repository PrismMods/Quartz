using Quartz.Features;
using Quartz.Resource;
using UnityEngine;
using TMPro;
namespace Quartz.Features.KeyViewer;
public static partial class KeyViewerOverlay {
    private static void BuildDmNote() {
        built = true;
        GameObject rainObj = new("RainLayer");
        rainObj.transform.SetParent(root, false);
        RectTransform rainLayer = rainObj.AddComponent<RectTransform>();
        rainLayer.anchorMin = Vector2.zero;
        rainLayer.anchorMax = Vector2.one;
        rainLayer.offsetMin = Vector2.zero;
        rainLayer.offsetMax = Vector2.zero;
        rainObj.AddComponent<Canvas>().overrideSorting = false;
        rainManager?.SetLayer(rainLayer);
        List<DmNoteSpec> specs = ParseLayoutSpecs(Layout.KvStore.Current);
        root.sizeDelta = new Vector2(dmCanvasWidth, dmCanvasHeight);
        int[] order = new int[specs.Count];
        for(int i = 0; i < order.Length; i++) order[i] = i;
        System.Array.Sort(order, (a, b) => {
            int byZ = specs[a].ZIndex.CompareTo(specs[b].ZIndex); 
            return byZ != 0 ? byZ : a.CompareTo(b);                
        });
        foreach(int i in order) AddDmNoteBox(i, specs[i]);
        totalCount = 0;
        foreach(Box box in boxes)
            if(!box.IsStat && box.CountInTotal) totalCount += box.Count;
        PaintInitialCounts();
        AddReorganizeHandle();
        Apply();
    }
    /// <summary>
    /// Write each counter's loaded value at build time, not just from UpdateDmNote.
    ///
    /// The counter text is created as "0" and UpdateDmNote paints the real number — but Unity does
    /// not run Update while the window is unfocused, so a game launched in the background showed
    /// every count as 0 until the user clicked in. The counts were loaded correctly; only the paint
    /// was deferred. Mirrors UpdateDmNote's display exactly so the first Update is a no-op.
    /// </summary>
    private static void PaintInitialCounts() {
        foreach(Box box in boxes) {
            DmNoteSpec spec = box.Dm;
            if(spec == null) continue;
            if(spec.IsStat) {
                int value = DmStatValue(box);
                if(box.Value != null) SetCount(box.Value, value, thousands: false);
                else if(box.Label != null && spec.InlineStatCounter)
                    SetPrefixedCount(box.Label, box.DmStatPrefix, value, thousands: false);
                box.LastShown = value;
            } else if(box.Value != null) {
                SetCount(box.Value, box.Count, thousands: false);
                box.LastShown = box.Count;
            }
        }
    }
    private static void RecordDmPress(Box box, float now) {
        box.Count++;
        // DM Note's counter press animation: the number pops to animScale and eases back along
        // the counter's cubic-bezier. Started on the press edge, ticked by the Updater.
        if(box.Value != null && box.Dm is { CounterAnimEnabled: true } spec && spec.CounterAnimScale > 1.001f) {
            if(!box.Bouncing) {
                box.Bouncing = true;
                // Captured only from rest: mid-bounce the position is already offset.
                box.BounceBasePos = box.Value.rectTransform.anchoredPosition;
                counterBounces.Add(box);
            }
            box.BounceStart = now;
        }
        // Fed at the same edge as Count, which is what it stands in for on screen — and so not
        // gated on CountInTotal: that excludes a box from the *shared* readouts below, while this
        // queue is only ever read by this box. Gated on the flag because nothing trims it when
        // it is off.
        if(box.PerKeyKps) box.KpsLog.Enqueue(now);
        // A box outside the total is outside KPS too: pressLog is what every KPS readout
        // counts, so a foot key left in it would inflate KPS as well as the total.
        if(box.CountInTotal) {
            totalCount++;
            pressLog.Enqueue(now);
        }
        MarkCountsDirty(now);
    }
    private static void BeginDmNoteRain(Box box, float now) {
        DmNoteSpec spec = box.Dm;
        if(!Conf.DmNoteEffect || spec == null || !spec.NoteEnabled || rainManager == null) return;
        float delay = dmDelayedNoteEnabled ? dmShortNoteThresholdMs / 1000f : 0f;
        if(delay > 0.0001f) {
            box.DelayedNotePending = true;
            box.DelayedReleasedBeforeStart = false;
            box.DelayedDownTime = now;
            box.DelayedStartTime = now + delay;
            box.DelayedReleaseTime = -1f;
            return;
        }
        box.LastRain = SpawnDmRain(box, now, false);
    }
    private static void EndDmNoteRain(Box box, float now, bool forceMinLength = false) {
        if(box.DelayedNotePending) {
            box.DelayedReleasedBeforeStart = true;
            box.DelayedReleaseTime = now;
            return;
        }
        if(box.LastRain == null) return;
        float minLengthSeconds = dmNoteSpeed > 0f ? dmShortNoteMinLengthPx / dmNoteSpeed : 0f;
        float end = now;
        if(forceMinLength || minLengthSeconds > 0.0001f) end = Mathf.Max(end, box.LastRain.StartTime + Mathf.Max(0.001f, minLengthSeconds));
        box.LastRain.EndTime = end;
        box.LastRain = null;
    }
    private static void UpdateDelayedDmNote(Box box, float now) {
        if(!box.DelayedNotePending || now < box.DelayedStartTime) return;
        DmNoteSpec spec = box.Dm;
        box.DelayedNotePending = false;
        if(!Conf.DmNoteEffect || spec == null || !spec.NoteEnabled || rainManager == null) return;
        box.LastRain = SpawnDmRain(box, box.DelayedStartTime, false);
        if(box.DelayedReleasedBeforeStart) {
            EndDmNoteRain(box, box.DelayedReleaseTime >= 0f ? box.DelayedReleaseTime : now, forceMinLength: true);
            box.DelayedReleasedBeforeStart = false;
        }
    }
    private static int DmStatValue(Box box) {
        if(box.IsKps) return pressLog.Count;
        if(box.IsKpsAvg) return kpsSamples > 0 ? Mathf.RoundToInt(kpsSum / (float)kpsSamples) : 0;
        if(box.IsKpsMax) return kpsMax;
        return box.IsTotal ? totalCount : 0;
    }
    internal static int GraphStatValue(string statType) {
        if(string.IsNullOrEmpty(statType)) return pressLog.Count;
        if(statType.Equals("kpsAvg", StringComparison.OrdinalIgnoreCase)) return kpsSamples > 0 ? Mathf.RoundToInt(kpsSum / (float)kpsSamples) : 0;
        if(statType.Equals("kpsMax", StringComparison.OrdinalIgnoreCase)) return kpsMax;
        if(statType.Equals("total", StringComparison.OrdinalIgnoreCase)) return totalCount;
        return pressLog.Count; 
    }
    private static void UpdateDmNote(float now) {
        while(pressLog.Count > 0 && now - pressLog.Peek() > 1f) pressLog.Dequeue();
        if(now >= nextKpsSample) {
            int kps = pressLog.Count;
            if(kps > kpsMax) kpsMax = kps;
            if(kps > 0) {
                kpsSum += kps;
                kpsSamples++;
            }
            nextKpsSample = now + 0.05f;
        }
        TMP_FontAsset font = FontManager.Current;
        int limiterMode = Mathf.Clamp(Conf.DmOutOfLimiterMode, 0, 2);
        foreach(Box box in boxes) {
            if(box.Label != null && box.Label.font != font) { box.Label.font = font; box.GradLabelText = null; }
            if(box.Value != null && box.Value.font != font) { box.Value.font = font; box.GradValueText = null; box.CounterStrokeMat = null; }
            DmNoteSpec spec = box.Dm;
            if(spec == null) continue;
            if(spec.IsStat) {
                int value = DmStatValue(box);
                if(box.Value != null && box.LastShown != value) {
                    SetCount(box.Value, value, thousands: false);
                    box.GradValueText = null;
                } else if(box.Value == null && box.Label != null && spec.InlineStatCounter && box.LastShown != value) {
                    SetPrefixedCount(box.Label, box.DmStatPrefix, value, thousands: false);
                    box.GradLabelText = null;
                }
                box.LastShown = value;
                continue;
            }
            bool rawPressed = KeyHeld(box.Key);
            bool blocked = box.Key != KeyCode.None && rawPressed && KeyLimiter.KeyLimiter.ShouldBlockKey(box.Key);
            bool hidden = blocked && limiterMode == 0;
            bool rainOnly = blocked && limiterMode == 1;
            bool physicalPressed = rawPressed && !hidden && !rainOnly;
            bool ghostPressed = (rainOnly || KeyHeld(spec.GhostKeyCode)) && !hidden;
            if(physicalPressed && !box.RawPressed) {
                RecordDmPress(box, now);
                BeginDmNoteRain(box, now);
            } else if(!physicalPressed && box.RawPressed) {
                EndDmNoteRain(box, now);
            }
            box.RawPressed = physicalPressed;
            UpdateDelayedDmNote(box, now);
            if(ghostPressed && !box.GhostPressed) {
                if(Conf.DmNoteEffect && spec.NoteEnabled && rainManager != null) box.LastGhostRain = SpawnDmRain(box, now, true);
                if(rainOnly && box.CountInTotal) {
                    totalCount++;
                    pressLog.Enqueue(now);
                }
            } else if(!ghostPressed && box.GhostPressed && box.LastGhostRain != null) {
                float minLengthSeconds = dmNoteSpeed > 0f ? dmShortNoteMinLengthPx / dmNoteSpeed : 0f;
                box.LastGhostRain.EndTime = Mathf.Max(now, box.LastGhostRain.StartTime + Mathf.Max(0.001f, minLengthSeconds));
                box.LastGhostRain = null;
            }
            bool displayPressed;
            float displayDelay = dmKeyDisplayDelayMs / 1000f;
            if(displayDelay <= 0.0001f) {
                box.DisplayTargetPressed = physicalPressed;
                box.DisplayTargetTime = now;
                displayPressed = physicalPressed;
            } else {
                if(physicalPressed != box.DisplayTargetPressed) {
                    box.DisplayTargetPressed = physicalPressed;
                    box.DisplayTargetTime = now + displayDelay;
                }
                displayPressed = now >= box.DisplayTargetTime ? box.DisplayTargetPressed : box.Pressed;
            }
            if(displayPressed != box.Pressed) {
                box.Pressed = displayPressed;
                ApplyBoxColors(box);
                RaisePressChanged(box);
            }
            box.GhostPressed = ghostPressed;
            int shown = box.Count;
            if(box.PerKeyKps) {
                // Same one-second sliding window as simple mode's own PerKeyKps readout, trimmed
                // lazily against `now`. Trimmed whenever the flag is set rather than only when the
                // counter is drawn: this box's every press feeds the queue, so an untrimmed one
                // behind a disabled counter would grow for the whole session.
                while(box.KpsLog.Count > 0 && now - box.KpsLog.Peek() > 1f) box.KpsLog.Dequeue();
                shown = box.KpsLog.Count;
            }
            if(box.Value != null && shown != box.LastShown) {
                box.LastShown = shown;
                SetCount(box.Value, shown, thousands: false);
                box.GradValueText = null;
            }
        }
    }
}

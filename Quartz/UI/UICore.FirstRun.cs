using Quartz.Async;
using Quartz.Core;
using Quartz.Localization;
using Quartz.Resource;
using Quartz.UI.Factory;
using Quartz.UI.Factory.Page;
using Quartz.UI.Generator;
using Quartz.UI.Objects;
using Quartz.UI.Panes;
using Quartz.UI.Utility;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Random = UnityEngine.Random;
using GTweens.Tweens;
using GTweens.Builders;
using GTweens.Extensions;
using Quartz.Tween;
using GTweens.Easings;
using GTweenExtensions = GTweens.Extensions.GTweenExtensions;
using TMPro;
namespace Quartz.UI;
public static partial class UICore {
    private static bool firstRunHelperActivated = false;
    private static GameObject firstRunCanvasObj;
    private static Image firstRunHelperImage;
    private static TextMeshProUGUI firstRunHelperText;
    private static GTween firstRunHelperImageSequence;
    private static GTween secondRunHelperTextSequence;
    // The helper canvas hangs off MainCore.Root, not the menu canvas, so it can show
    // while the menu is closed — which also means UICore.Dispose()'s Destroy(canvasObj)
    // never touches it. Rebuild() is Dispose() + Initialize(), and Initialize() asks
    // for a helper whenever IsFirstRun is still set, so every rebuild before the user
    // opened the menu used to orphan another canvas on screen with nothing left
    // pointing at it. The generation counter ties each helper to the UI that built it:
    // one alive at a time, and a delayed build from a torn-down generation is dropped.
    private static int firstRunGeneration;
    private static void MakeFirstRunHelper() {
        if(firstRunCanvasObj != null) return;
        int generation = firstRunGeneration;
        Task.Run(async () => {
            await Task.Delay(4000);
            MainThread.Enqueue(() => {
                if(generation != firstRunGeneration || firstRunCanvasObj != null) return;
                if(!MainCore.Conf.IsFirstRun) return;
                // Menu already open (ShowOnStartup) by the time the delay elapsed:
                // prompting for the key that opens it is nonsense, and the user has
                // plainly found it, so the tutorial is simply done.
                if(isOpen) {
                    MainCore.Conf.IsFirstRun = false;
                    MainCore.ConfMgr.Save();
                    return;
                }
                firstRunHelperActivated = true;
                firstRunCanvasObj = new GameObject("FirstRunHelperCanvas");
                firstRunCanvasObj.transform.SetParent(MainCore.Root.transform, false);
                firstRunCanvasObj.AddComponent<RectTransform>();
                var canvas = firstRunCanvasObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 32767;
                var scaler = firstRunCanvasObj.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = 0.5f;
                var frh = new GameObject("FirstRunHelper");
                var frhRect = frh.AddComponent<RectTransform>();
                frh.transform.SetParent(firstRunCanvasObj.transform, false);
                frhRect.anchorMin = new Vector2(0f, 0f);
                frhRect.anchorMax = new Vector2(1f, 0f);
                frhRect.pivot = new Vector2(0.5f, 0f);
                frhRect.offsetMin = new Vector2(0f, 0f);
                frhRect.offsetMax = new Vector2(0f, 4f);
                firstRunHelperImage = frh.AddComponent<Image>();
                firstRunHelperImage.raycastTarget = false;
                firstRunHelperImage.color = new Color(1f, 1f, 1f, 0f);
                var frhTextObj = new GameObject("Text");
                var frhTextRect = frhTextObj.AddComponent<RectTransform>();
                frhTextObj.transform.SetParent(frh.transform, false);
                var tmp = frhTextObj.AddComponent<TextMeshProUGUI>();
                tmp.fontSize = 22f;
                tmp.color = Color.white;
                tmp.alignment = TextAlignmentOptions.Bottom;
                tmp.text = "";
                tmp.font = FontManager.Current;
                frhTextRect.anchorMin = new Vector2(0.5f, 0.5f);
                frhTextRect.anchorMax = new Vector2(0.5f, 0.5f);
                frhTextRect.anchoredPosition = new Vector2(0f, 6f);
                frhTextRect.sizeDelta = new Vector2(1000f, 50f);
                frhTextRect.pivot = new Vector2(0.5f, 0f);
                firstRunHelperText = tmp;
                firstRunHelperImageSequence = GTweenSequenceBuilder.New()
                    .Append(firstRunHelperImage.GTAlpha(1.6f, 0.1f).SetEasing(Easing.OutSine))
                    .Append(firstRunHelperImage.GTAlpha(0.04f, 1f).SetEasing(Easing.OutSine))
                    .Build()
                    .SetMaxLoops();
                string fullText = string.Format(
                    MainCore.Tr.Get("FIRST_RUN_PRESS", "Press {0}"),
                    Keybind.Format(
                        (Keybind.KeyModifier)MainCore.Conf.ToggleModifier,
                        (KeyCode)MainCore.Conf.ToggleKey
                    )
                );
                secondRunHelperTextSequence = GTweenSequenceBuilder.New()
                    .Append(GTweenExtensions.Tween(
                        () => 0,
                        x => { if(firstRunHelperText != null) firstRunHelperText.text = fullText[..x]; },
                        fullText.Length,
                        1.4f
                    ).SetEasing(Easing.OutSine))
                    .Build();
                MainCore.TC.Play(firstRunHelperImageSequence);
                MainCore.TC.Play(secondRunHelperTextSequence);
            });
        });
    }
    /// <summary>
    /// Tears the helper down now, without animating. Called from
    /// <see cref="Dispose"/> so a helper never outlives the UI generation that
    /// built it — <c>Dispose</c> also runs <c>MainCore.TC.Clear()</c>, which kills
    /// the farewell sequence in <see cref="EndFirstRunHelper"/> mid-flight and with
    /// it the callback that was supposed to destroy this canvas.
    /// </summary>
    private static void DestroyFirstRunHelper() {
        firstRunGeneration++;
        firstRunHelperActivated = false;
        firstRunHelperImageSequence?.Kill();
        secondRunHelperTextSequence?.Kill();
        firstRunHelperImageSequence = null;
        secondRunHelperTextSequence = null;
        firstRunHelperImage = null;
        firstRunHelperText = null;
        if(firstRunCanvasObj != null) UnityEngine.Object.Destroy(firstRunCanvasObj);
        firstRunCanvasObj = null;
    }
    private static void EndFirstRunHelper() {
        MainCore.Conf.IsFirstRun = false;
        MainCore.ConfMgr.Save();
        firstRunHelperImageSequence?.Kill();
        secondRunHelperTextSequence?.Kill();
        // A torn-down helper still owes the settings write above; the farewell has
        // nothing left to play on.
        if(firstRunHelperText == null || firstRunHelperImage == null) {
            DestroyFirstRunHelper();
            return;
        }
        firstRunHelperText.text = "";
        string endText = MainCore.Tr.Get("FIRST_RUN_GREAT_JOB", "Great Job!");
        var sequence = GTweenSequenceBuilder.New()
            .Append(firstRunHelperImage.GTAlpha(1.0f, 0.2f).SetEasing(Easing.OutSine))
            .Join(GTweenExtensions.Tween(
                () => 0,
                x => firstRunHelperText.text = endText[..x],
                endText.Length,
                0.8f
            ).SetEasing(Easing.Linear))
            .AppendTime(3.0f)
            .Append(firstRunHelperImage.GTAlpha(0f, 2.0f))
            .Join(firstRunHelperText.GTAlpha(0f, 2.0f))
            .AppendCallback(DestroyFirstRunHelper)
            .Build();
        MainCore.TC.Play(sequence);
    }
}

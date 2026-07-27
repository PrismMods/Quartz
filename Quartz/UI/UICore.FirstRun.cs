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
    private static void MakeFirstRunHelper() {
        Task.Run(async () => {
            await Task.Delay(4000);
            MainThread.Enqueue(() => {
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
    private static void EndFirstRunHelper() {
        MainCore.Conf.IsFirstRun = false;
        MainCore.ConfMgr.Save();
        firstRunHelperImageSequence?.Kill();
        secondRunHelperTextSequence?.Kill();
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
            .AppendCallback(() => {
                if(firstRunCanvasObj != null) UnityEngine.Object.Destroy(firstRunCanvasObj);
            })
            .Build();
        MainCore.TC.Play(sequence);
    }
}

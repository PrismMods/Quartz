using System.Text;
using Quartz.Core;
using Quartz.Features.Tuf;
using Quartz.Localization;
using Quartz.Resource;
using Quartz.Tween;
using Quartz.UI.Generator;
using Quartz.UI.Utility;
using GTweens.Builders;
using GTweens.Easings;
using GTweens.Tweens;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Quartz.Compat.Game;
namespace Quartz.UI.Factory.Page;
internal sealed partial class TufBrowserView : MonoBehaviour {
    private TufService service;
    private RectTransform content;
    private UIScrollController scroll;
    private TMP_InputField search;
    private readonly List<(TufSort Sort, Image Image)> sortChips = [];
    private readonly List<(string Name, Image Image)> difficultyChips = [];
    private Image directionChip;
    private TMP_Text directionLabel;
    private TufDifficultyRangeBar difficultyRange;
    private TufDifficultyRangeBar quantumRange;
    private RectTransform quantumRow;
    private RectTransform viewport;
    private RectTransform specialChecks;
    private CanvasGroup specialChecksCg;
    private RectTransform specialArrowRect;
    private Image specialArrow;
    private GTween specialArrowSeq;
    private GTween filterLayoutSeq;
    private GTween chartChooserSeq;
    private bool specialExpanded;
    private bool lastQuantumOn;
    private float quantumLayout;
    private float specialChecksScale = 0.82f;
    private const float ArmSeconds = 4f;
    private const float MetaGap = 12f;
    private const float GridMinWidth = 268f;
    private const float GridGap = 8f;
    private const float GridCardHeight = 176f;
    private readonly Dictionary<int, TMP_Text> cardLabels = [];
    private readonly Dictionary<int, Image> deleteChips = [];
    private TufPreviewGroup previews;
    private Image installedChip;
    private TMP_Text installedLabel;
    private Image gridChip;
    private Image updatesChip;
    private TMP_Text updatesLabel;
    private int gridColumns = 2;
    private string listSignature;
    private bool built;
    private bool pendingRebuild;
    private int armedDeleteId;
    private float armedUntil;
    private void OnEnable() {
        if(!pendingRebuild) return;
        pendingRebuild = false;
        Rebuild();
    }
    public void Build(RectTransform parent) {
        service = TufService.Instance;
        if(service == null) return;
        RectTransform pad = Rect("TUF Browser", parent, Vector2.zero, Vector2.one, new(18f, 18f), new(-18f, -18f));
        BuildHeader(pad);
        viewport = Rect("Level Viewport", pad, Vector2.zero, Vector2.one, Vector2.zero, new(0f, -266f));
        viewport.gameObject.AddComponent<EmptyGraphic>().raycastTarget = true;
        viewport.gameObject.AddComponent<RectMask2D>();
        lastQuantumOn = service.QuantumEnabled;
        quantumLayout = lastQuantumOn ? 1f : 0f;
        ApplyFilterLayout();
        content = Rect("Level Cards", viewport, new(0f, 1f), new(1f, 1f), Vector2.zero, Vector2.zero);
        content.pivot = new(0.5f, 1f);
        GenerateUI.FitVertical(content.gameObject, 8f);
        scroll = pad.gameObject.AddComponent<UIScrollController>();
        scroll.SetContent(content, viewport);
        built = true;
        previews = new TufPreviewGroup();
        service.Changed += Rebuild;
        service.EnsureLoaded();
        Rebuild();
    }
    private void Update() {
        if(!built || service == null || content == null || viewport == null) return;
        previews?.Tick();
        if(service.GridView) {
            int columns = ComputeColumns();
            if(columns != gridColumns) {
                gridColumns = columns;
                listSignature = null;
                Rebuild();
            }
        }
        if(armedDeleteId != 0 && Time.unscaledTime >= armedUntil) DisarmDelete();
        if(!service.HasMore || service.LoadingMore || service.State != TufListState.Ready) return;
        float max = content.rect.height - viewport.rect.height;
        if(max <= 0f || content.anchoredPosition.y >= max - 400f) service.LoadMore();
    }
    private void OnDestroy() {
        if(service != null) service.Changed -= Rebuild;
        previews?.Dispose();
        filterLayoutSeq?.Kill();
        specialArrowSeq?.Kill();
        chartChooserSeq?.Kill();
    }
}

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
internal sealed partial class TufPacksView : MonoBehaviour {
    private void Rebuild() {
        if(this == null || !built || content == null || service == null) return;
        if(!gameObject.activeInHierarchy) {
            pendingRebuild = true;
            return;
        }
        string signature = BuildSignature();
        if(signature == listSignature && cardLabels.Count > 0) {
            foreach(TufLevel level in service.PackLevels) {
                if(!cardLabels.TryGetValue(level.Id, out TMP_Text label) || label == null) continue;
                string text = ActionLabel(level);
                if(label.text != text) label.text = text;
            }
            RefreshControls();
            return;
        }
        listSignature = signature;
        float oldY = content.anchoredPosition.y;
        RefreshControls();
        chartChooserSeq?.Kill();
        previews.ClearSlots();
        GenerateUI.ClearChildren(content);
        cardLabels.Clear();
        bool detail = service.SelectedPack != null;
        if(detail) RebuildDetail();
        else RebuildList();
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        if(detail != lastDetailView) {
            if(detail) {
                listScrollY = oldY;
                scroll.ScrollTo(0f);
            } else {
                scroll.ScrollTo(listScrollY);
            }
            lastDetailView = detail;
            PlayViewSwitch(detail);
        } else {
            scroll.ScrollTo(oldY);
        }
    }
    private void PlayViewSwitch(bool detail) {
        if(contentCg == null) return;
        viewSwitchSeq?.Kill();
        contentCg.alpha = 0f;
        float fromX = detail ? 48f : -48f;
        content.anchoredPosition = new(fromX, content.anchoredPosition.y);
        viewSwitchSeq = GTweenSequenceBuilder.New()
            .Join(contentCg.GTAlpha(1f, 0.2f).SetEasing(Easing.OutSine))
            .Join(GTweens.Extensions.GTweenExtensions.Tween(
                () => content.anchoredPosition.x,
                x => content.anchoredPosition = new(x, content.anchoredPosition.y),
                0f, 0.3f).SetEasing(Easing.OutCubic))
            .Build();
        MainCore.TC.Play(viewSwitchSeq);
    }
    private void RebuildList() {
        if(service.ListState == TufPackListState.Loading) {
            AddLoadingStatus(Tr("TUF_PACK_LOADING", "Loading packs…"));
        } else if(service.ListState == TufPackListState.Error && service.Packs.Count == 0) {
            if(service.OfflineError) AddOfflineStatus(service.ListError, service.RefreshPacks);
            else AddStatus(Tr("TUF_PACK_API_ERROR", "Could not load TUF packs.") + "\n" + service.ListError, true, service.RefreshPacks);
        } else if(service.ListState == TufPackListState.Empty) {
            AddStatus(Tr("TUF_PACK_EMPTY", "No packs matched your search."), false, null);
        } else {
            foreach(TufPack pack in service.Packs) AddPackCard(pack);
            if(service.HasMore) {
                if(service.LoadingMore) AddLoadingStatus(Tr("TUF_PACK_LOADING", "Loading packs…"));
                else if(service.ListState == TufPackListState.Error) AddStatus(Tr("TUF_RETRY", "Retry"), true, service.LoadMore);
            }
            else if(service.Packs.Count > 0) AddStatus(Tr("TUF_END", "End of results"), false, null, 38f);
        }
    }
    private void RebuildDetail() {
        TufPack pack = service.SelectedPack;
        if(expandedPackId != pack.Id) {
            expandedPackId = pack.Id;
            expandedFolders.Clear();
        }
        AddBackRow(pack);
        if(service.DetailState == TufPackListState.Loading) {
            AddLoadingStatus(Tr("TUF_PACK_LOADING_LEVELS", "Loading pack levels…"));
        } else if(service.DetailState == TufPackListState.Error && service.PackLevels.Count == 0) {
            if(service.OfflineError) AddOfflineStatus(service.DetailError, service.RetryPackLevels);
            else AddStatus(Tr("TUF_PACK_LEVELS_ERROR", "Could not load this pack.") + "\n" + service.DetailError, true, service.RetryPackLevels);
        } else if(service.DetailState == TufPackListState.Empty) {
            AddStatus(Tr("TUF_PACK_NO_LEVELS", "This pack has no playable levels."), false, null);
        } else {
            AddLevelSortRow();
            RenderItems(service.PackItems, 0);
        }
    }
    private IReadOnlyList<TufPackItem> SortItems(IReadOnlyList<TufPackItem> items) {
        if(service.LevelSort == TufPackLevelSort.PackOrder) return items;
        List<TufPackItem> result = [.. items];
        List<int> slots = [];
        List<TufPackItem> levels = [];
        for(int i = 0; i < result.Count; i++) {
            if(result[i].IsFolder) continue;
            slots.Add(i);
            levels.Add(result[i]);
        }
        IEnumerable<TufPackItem> sorted = (service.LevelSort, service.LevelAscending) switch {
            (TufPackLevelSort.Difficulty, true) => levels.OrderBy(RankOf),
            (TufPackLevelSort.Difficulty, false) => levels.OrderByDescending(RankOf),
            (TufPackLevelSort.Clears, true) => levels.OrderBy(ClearsOf),
            _ => levels.OrderByDescending(ClearsOf),
        };
        int slot = 0;
        foreach(TufPackItem item in sorted) result[slots[slot++]] = item;
        return result;
    }
    private static int RankOf(TufPackItem item) => item.Level?.DifficultyRank ?? 0;
    private static int ClearsOf(TufPackItem item) => item.Level?.Clears ?? 0;
    private void RenderItems(IReadOnlyList<TufPackItem> items, int depth) {
        float indent = depth * 26f;
        foreach(TufPackItem item in SortItems(items)) {
            if(item.IsFolder) {
                AddFolderRow(item, indent);
                if(expandedFolders.Contains(item.Key)) RenderItems(item.Children, depth + 1);
            } else {
                RenderLevel(item.Level, indent);
            }
        }
    }
    private void RenderLevel(TufLevel level, float indent) {
        AddLevelCard(level, indent);
        if(level.State == TufItemState.ChooseChart && level.Charts != null) AddChartChooser(level, indent);
    }
    private string BuildSignature() {
        StringBuilder sb = new();
        sb.Append(ShowPreviews ? 'P' : 'p').Append(service.OfflineError ? 'O' : 'o');
        if(service.SelectedPack != null) {
            sb.Append("D:").Append(service.SelectedPack.Id).Append('|')
                .Append((int)service.DetailState).Append('|').Append(service.IsBusy ? '1' : '0').Append('|')
                .Append((int)service.LevelSort).Append(service.LevelAscending ? '1' : '0').Append('|');
            foreach(long key in expandedFolders) sb.Append(key).Append('^');
            sb.Append('|');
            foreach(TufLevel level in service.PackLevels)
                sb.Append(level.Id).Append(':').Append((int)level.State)
                    .Append('#').Append(level.Charts?.Count ?? 0).Append(',');
        } else {
            sb.Append("L:").Append((int)service.ListState).Append('|')
                .Append(service.HasMore ? '1' : '0').Append(service.LoadingMore ? '1' : '0').Append('|');
            foreach(TufPack pack in service.Packs) sb.Append(pack.Id).Append(',');
        }
        return sb.ToString();
    }
    private void RefreshControls() {
        foreach((TufPackSort sort, Image image) in sortChips)
            image.color = sort == service.Sort ? UIColors.ObjectActive : UIColors.ObjectBG;
        directionChip.color = service.Ascending ? UIColors.ObjectActive : UIColors.ObjectBG;
        directionLabel.text = service.Ascending ? "↑" : "↓";
    }
}

using Quartz.Async;
using Quartz.Compat.Interface;
using Quartz.Core;
using Quartz.IO;
using Quartz.UI;
namespace Quartz.Features.Tuf;
public sealed partial class TufService : IRuntimeService {
    public string CustomLevelsRoot => settings?.Data.CustomLevelsRoot ?? "";
    public string ActiveRootPath => downloads?.ActiveRoot().Path ?? MainCore.Paths.TufLevelsPath;
    private TufInstallRoot ResolveInstallRoot() {
        if(settings?.Data.LinkTufHelperLite == true) {
            string helper = TufHelperLiteLink.DownloadsRoot();
            if(!string.IsNullOrEmpty(helper)) return new(helper, true);
        }
        string custom = settings?.Data.CustomLevelsRoot;
        if(!string.IsNullOrWhiteSpace(custom)) {
            try { if(Directory.Exists(custom)) return new(Path.GetFullPath(custom), false); }
            catch(Exception e) { Diag.Ignore(e); }
        }
        return new(MainCore.Paths.TufLevelsPath, false);
    }
    private IEnumerable<string> TrustedRoots() {
        List<string> roots = [MainCore.Paths.TufLevelsPath, ResolveInstallRoot().Path];
        if(settings != null) roots.AddRange(settings.Data.KnownRoots);
        return roots;
    }
    private void SaveSettings() {
        if(settings == null) return;
        settings.Data.Sort = (int)Sort;
        settings.Data.Ascending = Ascending;
        settings.Data.SetDifficultyFilter(DifficultyFilter, quantumMinIndex, quantumMaxIndex);
        settings.RequestSave();
    }
    private bool LinkOwnsTarget => settings?.Data.LinkTufHelperLite == true && TufHelperLiteLink.Installed;
    public bool SetCustomLevelsRoot(string path, out string reason) {
        reason = "";
        if(settings == null || disposed) return false;
        if(MoveState == TufMoveState.Moving) {
            reason = "busy";
            return false;
        }
        if(LinkOwnsTarget) {
            reason = "linked";
            return false;
        }
        string full;
        try { full = Path.GetFullPath(path); } catch { reason = "invalid"; return false; }
        if(!TufInstallPaths.IsUsableLibraryRoot(full, out reason)) return false;
        if(string.Equals(Path.GetFullPath(ResolveInstallRoot().Path), full, PathComparison)) return true;
        if(TufInstallPaths.IsSameOrNested(full, MainCore.Paths.TufLevelsPath)) {
            reason = "nested";
            return false;
        }
        settings.Data.CustomLevelsRoot = full;
        settings.Data.RememberRoot(full);
        settings.Save();
        StartMove(full);
        return true;
    }
    public bool ClearCustomLevelsRoot(out string reason) {
        reason = "";
        if(settings == null || disposed) return false;
        if(MoveState == TufMoveState.Moving) {
            reason = "busy";
            return false;
        }
        if(LinkOwnsTarget) {
            reason = "linked";
            return false;
        }
        if(string.IsNullOrEmpty(settings.Data.CustomLevelsRoot)) return true;
        settings.Data.CustomLevelsRoot = "";
        settings.Save();
        StartMove(MainCore.Paths.TufLevelsPath);
        return true;
    }
    private void StartMove(string toRoot) {
        moveRequest?.Cancel();
        moveRequest?.Dispose();
        moveRequest = new CancellationTokenSource();
        string destination = Path.GetFullPath(toRoot);
        List<(int Id, string From)> pending = index.Data.Entries
            .Where(e => !string.Equals(Path.GetDirectoryName(e.Folder), destination, PathComparison))
            .Select(e => (e.Id, e.Folder))
            .ToList();
        MoveDone = 0;
        MoveTotal = pending.Count;
        MoveError = "";
        if(pending.Count == 0) {
            MoveState = TufMoveState.Done;
            Notify();
            return;
        }
        MoveState = TufMoveState.Moving;
        Notify();
        MoveLibrary(pending, new TufInstallRoot(destination, false), moveRequest.Token);
    }
    private static StringComparison PathComparison =>
        Path.DirectorySeparatorChar == '\\' ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
    private async void MoveLibrary(List<(int Id, string From)> pending, TufInstallRoot target, CancellationToken token) {
        List<string> roots = TrustedRoots().ToList();
        int failures = 0;
        string firstError = "";
        foreach((int id, string from) in pending) {
            if(token.IsCancellationRequested) break;
            try {
                string moved = await Task.Run(
                    () => downloads.MoveLevel(id, from, target.Path, target.Linked, roots, token), token);
                MainThread.Enqueue(() => {
                    if(disposed || token.IsCancellationRequested) return;
                    index.Data.SetFolder(id, moved);
                    index.RequestSave();
                    MoveDone++;
                    Notify();
                });
            } catch(OperationCanceledException) {
                break;
            } catch(Exception e) {
                failures++;
                if(firstError.Length == 0) firstError = e.Message;
                MainCore.Log.Wrn($"[TUF] could not move level {id} to the new library: {e}");
                MainThread.Enqueue(() => {
                    if(disposed) return;
                    MoveDone++;
                    Notify();
                });
            }
        }
        MainThread.Enqueue(() => {
            if(disposed) return;
            MoveState = failures > 0 ? TufMoveState.Failed : TufMoveState.Done;
            MoveError = failures > 0
                ? string.Format(MainCore.Tr.Get("TUF_MOVE_FAILED_COUNT",
                    "{0} level(s) could not be moved and stayed where they were: {1}"), failures, firstError)
                : "";
            index.Save();
            if(ShowInstalled) LoadInstalled();
            else Notify();
        });
    }
}

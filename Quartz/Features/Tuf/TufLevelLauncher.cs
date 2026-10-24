using System.Collections;
using System.Reflection;
using HarmonyLib;
using Quartz.Core;
using Quartz.UI;
using Quartz.UI.Utility;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
namespace Quartz.Features.Tuf;
public sealed class TufLevelLauncher : MonoBehaviour {
    private const int ExitPlayFrames = 180;
    private const int SaveFrames = 30;
    private string levelsRoot;
    private Func<IEnumerable<string>> trustedRoots;
    private Coroutine pending;
    private Action<bool, string> completion;
    private GameObject loadingCover;
    private int dialogChoice = TufUnsavedDialog.Pending;
    public void Initialize(string root, Func<IEnumerable<string>> trustedRoots = null) {
        levelsRoot = Path.GetFullPath(root);
        this.trustedRoots = trustedRoots;
    }
    private bool ChartUnderTrustedRoot(string chart) {
        if(TufArchive.IsChartUnderRoot(chart, levelsRoot)) return true;
        try {
            foreach(string root in trustedRoots?.Invoke() ?? Array.Empty<string>())
                if(!string.IsNullOrEmpty(root) && TufArchive.IsChartUnderRoot(chart, root)) return true;
        } catch(Exception e) { Diag.Ignore(e); }
        return false;
    }
    public bool Launch(string chartPath, Action<bool, string> completed) {
        if(pending != null || completion != null) Cancel();
        completion = completed;
        try {
            if(!ChartUnderTrustedRoot(chartPath) || !File.Exists(chartPath))
                throw new InvalidDataException(Tr("TUF_LAUNCH_INVALID_PATH", "Playable chart path is invalid."));
            string expected = Path.GetFullPath(chartPath);
            if(!ClearTufHelperLaunchState())
                throw new InvalidOperationException(Tr("TUF_LAUNCH_STATE_ERROR",
                    "Could not clear conflicting TUFHelper launch state."));
            DiscordController.shouldUpdatePresence = true;
            scnEditor active = CurrentEditor();
            if(active != null) {
                MainCore.Log.Msg("[TUF] opening chart in current editor: " + expected);
                ShowLoadingCover();
                pending = StartCoroutine(Guarded(OpenInCurrentEditor(active, expected)));
                return true;
            }
            MainCore.Log.Msg("[TUF] opening editor for chart: " + expected);
            ShowLoadingCover();
            scnEditor stale = scnEditor.instance;
            GCS.sceneToLoad = "scnEditor";
            GCS.worldEntrance = null;
            scnEditor.levelToOpenOnLoad = null;
            SceneManager.LoadScene("scnEditor");
            pending = StartCoroutine(Guarded(WaitAndLoad(stale, expected)));
            return true;
        } catch(Exception e) {
            Complete(false, Tr("TUF_LAUNCH_FAILED", "Could not launch the TUF level: {0}", e.Message));
            return false;
        }
    }
    private static scnEditor CurrentEditor() {
        if(SceneManager.GetActiveScene().name != "scnEditor") return null;
        scnEditor editor = scnEditor.instance;
        return editor != null && editor.initialized ? editor : null;
    }
    private IEnumerator WaitAndLoad(scnEditor stale, string expected) {
        float initDeadline = Time.realtimeSinceStartup + 15f;
        scnEditor editor = null;
        while(Time.realtimeSinceStartup < initDeadline) {
            scnEditor candidate = scnEditor.instance;
            if(candidate != null && candidate.initialized && !ReferenceEquals(candidate, stale)) {
                editor = candidate;
                break;
            }
            yield return null;
        }
        if(editor == null) {
            MainCore.Log.Wrn("[TUF] editor initialization did not complete");
            Complete(false, Tr("TUF_EDITOR_INIT_FAILED",
                "Editor initialization was interrupted; check other mods."));
            yield break;
        }
        IEnumerator load = OpenAndLoad(editor, expected);
        while(load.MoveNext()) yield return load.Current;
    }
    private IEnumerator OpenInCurrentEditor(scnEditor editor, string expected) {
        if(editor.playMode) {
            MainCore.Log.Msg("[TUF] leaving editor play mode before loading the chart");
            editor.SwitchToEditMode();
            for(int frame = 0; frame < ExitPlayFrames && editor != null && editor.playMode; frame++)
                yield return null;
            if(editor == null) {
                Complete(false, Tr("TUF_EDITOR_CLOSED", "Editor closed before the TUF level could load."));
                yield break;
            }
            if(editor.playMode) {
                MainCore.Log.Wrn("[TUF] the editor stayed in play mode; cannot load the chart");
                Complete(false, Tr("TUF_EXIT_PLAY_FAILED",
                    "Could not leave play mode; stop the run yourself, then try again."));
                yield break;
            }
            yield return null;
        }
        if(HasUnsavedChanges(editor)) {
            IEnumerator ask = ResolveUnsavedChanges(editor);
            while(ask.MoveNext()) yield return ask.Current;
            if(completion == null) yield break;
        }
        IEnumerator load = OpenAndLoad(editor, expected);
        while(load.MoveNext()) yield return load.Current;
    }
    private IEnumerator ResolveUnsavedChanges(scnEditor editor) {
        UICore.Close(true);
        dialogChoice = TufUnsavedDialog.Pending;
        TufUnsavedDialog.Show(choice => dialogChoice = choice);
        while(dialogChoice == TufUnsavedDialog.Pending) {
            if(editor == null) {
                TufUnsavedDialog.Close();
                Complete(false, Tr("TUF_EDITOR_CLOSED", "Editor closed before the TUF level could load."));
                yield break;
            }
            if(!TufUnsavedDialog.IsOpen) dialogChoice = TufUnsavedDialog.Cancel;
            else yield return null;
        }
        if(dialogChoice == TufUnsavedDialog.Cancel) {
            MainCore.Log.Msg("[TUF] chart load cancelled at the unsaved-changes prompt");
            Complete(false, "");
            yield break;
        }
        if(dialogChoice != TufUnsavedDialog.Save) {
            SetUnsavedChanges(editor, false);
            yield break;
        }
        editor.SaveLevel();
        for(int frame = 0; frame < SaveFrames && editor != null && HasUnsavedChanges(editor); frame++)
            yield return null;
        if(editor == null) {
            Complete(false, Tr("TUF_EDITOR_CLOSED", "Editor closed before the TUF level could load."));
            yield break;
        }
        if(HasUnsavedChanges(editor)) {
            MainCore.Log.Wrn("[TUF] the editor still reports unsaved changes after saving");
            Complete(false, Tr("TUF_SAVE_FAILED", "Your editor changes could not be saved."));
        }
    }
    private IEnumerator Guarded(IEnumerator operation) {
        while(true) {
            bool moved = false;
            object current = null;
            Exception failure = null;
            try {
                moved = operation.MoveNext();
                if(moved) current = operation.Current;
            } catch(Exception e) {
                failure = e;
            }
            if(failure != null) {
                MainCore.Log.Wrn("[TUF] unexpected level-load failure: " + failure);
                Complete(false, Tr("TUF_LAUNCH_FAILED",
                    "Could not launch the TUF level: {0}", failure.Message));
                yield break;
            }
            if(!moved) yield break;
            yield return current;
        }
    }
    private IEnumerator OpenAndLoad(scnEditor editor, string expected) {
        yield return null;
        if(editor == null) {
            Complete(false, Tr("TUF_EDITOR_CLOSED", "Editor closed before the TUF level could load."));
            yield break;
        }
        GameObject failurePopup = editor.notificationPopupContainer;
        bool popupWasActive = failurePopup != null && failurePopup.activeInHierarchy;
        try {
            MainCore.Log.Msg("[TUF] invoking scnEditor.OpenLevel for: " + expected);
            editor.OpenLevel(expected);
        } catch(Exception e) {
            Complete(false, Tr("TUF_CHART_OPEN_FAILED",
                "Could not open the downloaded chart: {0}", e.Message));
            yield break;
        }
        float loadDeadline = Time.realtimeSinceStartup + 30f;
        while(Time.realtimeSinceStartup < loadDeadline) {
            if(editor == null) break;
            if(!popupWasActive && failurePopup != null && failurePopup.activeInHierarchy) {
                string reason = PopupMessage(failurePopup);
                MainCore.Log.Wrn("[TUF] the game rejected the chart: " + (reason ?? "<no message>"));
                Complete(false, string.IsNullOrWhiteSpace(reason)
                    ? Tr("TUF_CHART_LOAD_REJECTED", "The game could not load this level — it may require another mod.")
                    : reason);
                yield break;
            }
            if(!editor.isLoading && SamePath(ADOBase.levelPath, expected) && editor.floors?.Count > 1) {
                yield return null;
                MainCore.Log.Msg("[TUF] chart loaded, ready to play: " + expected);
                Complete(true, "");
                yield break;
            }
            yield return null;
        }
        string loadedPath = ADOBase.levelPath ?? "<none>";
        int floorCount = editor?.floors?.Count ?? 0;
        MainCore.Log.Wrn($"[TUF] chart load failed; expected='{expected}', loaded='{loadedPath}', floors={floorCount}");
        Complete(false, SamePath(loadedPath, expected) && floorCount <= 1
            ? Tr("TUF_CHART_UNPLAYABLE", "The downloaded chart could not be loaded or is not playable.")
            : Tr("TUF_CHART_LOAD_TIMEOUT", "The downloaded chart did not finish loading in the editor."));
    }
    private void ShowLoadingCover() {
        HideLoadingCover();
        loadingCover = UnityUtils.CreateOverlayCanvas(
            "TUF Loading Cover", MainCore.Root.transform, 32766, out GraphicRaycaster raycaster);
        raycaster.enabled = true;
        GameObject background = new("Background");
        background.transform.SetParent(loadingCover.transform, false);
        RectTransform rect = background.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        Image image = background.AddComponent<Image>();
        image.color = Color.Lerp(UIColors.PanelBG, Color.black, 0.3f);
        image.raycastTarget = true;
    }
    private void HideLoadingCover() {
        if(loadingCover != null) Destroy(loadingCover);
        loadingCover = null;
    }
    private void Complete(bool success, string error) {
        pending = null;
        TufUnsavedDialog.Close();
        HideLoadingCover();
        if(success) UICore.Close(true);
        Action<bool, string> callback = completion;
        completion = null;
        callback?.Invoke(success, error ?? "");
    }
    private static readonly FieldInfo UnsavedChangesField =
        AccessTools.Field(typeof(scnEditor), "_unsavedChanges");
    private static readonly PropertyInfo UnsavedChangesProperty =
        AccessTools.Property(typeof(scnEditor), "unsavedChanges");
    private static bool HasUnsavedChanges(scnEditor editor) {
        try { return UnsavedChangesField?.GetValue(editor) is true; }
        catch(Exception e) { Diag.Ignore(e); return false; }
    }
    private static void SetUnsavedChanges(scnEditor editor, bool value) {
        try {
            if(UnsavedChangesProperty?.GetSetMethod(true) != null) {
                UnsavedChangesProperty.SetValue(editor, value, null);
                return;
            }
            UnsavedChangesField?.SetValue(editor, value);
        } catch(Exception e) { Diag.Warn(e, "TUF/DiscardEditorChanges"); }
    }
    private static string PopupMessage(GameObject popup) {
        try {
            TMPro.TMP_Text text = popup.GetComponentInChildren<TMPro.TMP_Text>(true);
            string value = text != null ? text.text?.Trim() : null;
            if(string.IsNullOrWhiteSpace(value)) return null;
            return value.Length <= 300 ? value : value[..300] + "…";
        } catch(Exception e) { Diag.Ignore(e); return null; }
    }
    private static bool SamePath(string a, string b) {
        if(string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b)) return false;
        try {
            StringComparison comparison = Path.DirectorySeparatorChar == '\\'
                ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            return string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), comparison);
        } catch(Exception e) { Diag.Ignore(e); return false; }
    }
    private static bool ClearTufHelperLaunchState() {
        bool mainCleared = TrySetStatic("TUFHelper.Main", "isInTUFHelper", false, false);
        bool sourceCleared = TrySetStatic(
            "TUFHelper.Utils.ADOFAIGameplayHandler", "IsFromTUFHelper", false, true);
        bool infoCleared = TrySetStatic(
            "TUFHelper.Utils.ADOFAIGameplayHandler+EditorPlayPatch", "CurrentLevelInfo", null, true);
        if(!mainCleared || !sourceCleared || !infoCleared)
            MainCore.Log.Wrn("[TUF] found TUFHelper state but could not fully clear its editor handoff");
        return mainCleared && sourceCleared && infoCleared;
    }
    private static bool TrySetStatic(string typeName, string memberName, object value, bool property) {
        Type type;
        try { type = AccessTools.TypeByName(typeName); }
        catch(Exception e) { Diag.Ignore(e); return true; }
        if(type == null) return true;
        try {
            if(property) {
                PropertyInfo member = AccessTools.Property(type, memberName);
                if(member == null) return false;
                member.SetValue(null, value, null);
            } else {
                FieldInfo member = AccessTools.Field(type, memberName);
                if(member == null) return false;
                member.SetValue(null, value);
            }
            return true;
        } catch(Exception e) { Diag.Ignore(e); return false; }
    }
    private static string Tr(string key, string fallback) => MainCore.Tr.Get(key, fallback);
    private static string Tr(string key, string fallback, object value) =>
        string.Format(MainCore.Tr.Get(key, fallback), value);
    public void Cancel() {
        if(pending != null) StopCoroutine(pending);
        if(completion != null) Complete(false, "");
        else {
            pending = null;
            TufUnsavedDialog.Close();
            HideLoadingCover();
        }
    }
    private void OnDestroy() => Cancel();
}

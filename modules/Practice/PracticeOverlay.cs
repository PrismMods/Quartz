using Quartz.Compat.Game;
using Quartz.Core;
using Quartz.Game.Stats;
using Quartz.Localization;
using Quartz.Resource;
using Quartz.UI;
using Quartz.UI.Utility;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using Quartz.Overlay;
namespace Quartz.Features.Practice;
public static class PracticeOverlay {
    private static PracticeSettings Conf => PracticeDifficulty.Conf;
    private static GameObject canvasObj;
    private static GraphicRaycaster raycaster;
    private static RectTransform root;
    private static TextMeshProUGUI text;
    private static GameObject dragObj;
    private static Updater updater;
    private static bool ShouldShow() =>
        (OverlaySwitch.Enabled && Conf.Enabled && Conf.ShowIndicator
            && (!Conf.IndicatorOnlyInGame || GameStats.InGame)) || UICore.IsReorganizing;
    private static bool gateWant;
    private static bool gateRunning = true;
    internal static void Gate() {
        if(updater == null) return;
        PracticeInput.Poll();
        PracticeDifficulty.FlushPendingDifficulty();
        bool want = ShouldShow() || UICore.IsReorganizing;
        bool run = want || gateWant;
        gateWant = want;
        if(gateRunning == run) return;
        gateRunning = run;
        updater.enabled = run;
    }
    public static void Initialize(GameObject rootObject) {
        if(canvasObj != null) return;
        PracticeDifficulty.EnsureConf();
        canvasObj = UnityUtils.CreateOverlayCanvas("QuartzPracticeCanvas", rootObject.transform, 32754, out raycaster);
        GameObject holder = new("PracticeRoot");
        holder.transform.SetParent(canvasObj.transform, false);
        root = holder.AddComponent<RectTransform>();
        root.anchorMin = new Vector2(0.5f, 1f);
        root.anchorMax = new Vector2(0.5f, 1f);
        root.pivot = new Vector2(0.5f, 1f);
        GameObject label = new("Readout");
        label.transform.SetParent(root, false);
        RectTransform labelRect = label.AddComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0.5f, 1f);
        labelRect.anchorMax = new Vector2(0.5f, 1f);
        labelRect.pivot = new Vector2(0.5f, 1f);
        text = label.AddComponent<TextMeshProUGUI>();
        text.font = FontManager.Current;
        text.alignment = TextAlignmentOptions.Top;
        text.raycastTarget = false;
        TextCompat.NoWrap(text);
        text.text = "";
        dragObj = ReorganizeHandle.CreateDragSurface(root,
            () => MainCore.Tr.Get("SECTION_PRACTICE_DIFFICULTY", "Practice Difficulty"), PracticeDifficulty.Save);
        updater = canvasObj.AddComponent<Updater>();
        gateWant = true;
        gateRunning = true;
        Apply();
    }
    public static void Apply() {
        if(root == null) return;
        root.anchoredPosition = OverlayCalibration.Scale(new Vector2(Conf.OffsetX, Conf.OffsetY));
        root.localScale = Vector3.one * Mathf.Max(0.01f, Conf.MasterSize);
        if(text == null) return;
        text.font = FontManager.Current;
        text.fontSize = Mathf.Clamp(Conf.FontSize, 4f, 200f);
        text.color = Conf.GetColor();
    }
    public static void ResetPosition() {
        PracticeSettings def = new();
        Conf.OffsetX = def.OffsetX;
        Conf.OffsetY = def.OffsetY;
        Apply();
        PracticeDifficulty.Save();
    }
    public static void Dispose() {
        if(canvasObj == null) return;
        PracticeDifficulty.ConfMgr?.Save();
        Object.Destroy(canvasObj);
        canvasObj = null;
        raycaster = null;
        root = null;
        text = null;
        dragObj = null;
        updater = null;
    }
    internal static string BuildBody() {
        int difficulty = PracticeDifficulty.CurrentDifficulty;
        string body = difficulty < 0
            ? MainCore.Tr.Get("PRACTICE_DIFF_UNKNOWN", "Difficulty ?")
            : PracticeDifficulty.DifficultyName(difficulty);
        int pending = PracticeDifficulty.PendingDifficulty;
        if(pending >= 0) body += " → " + PracticeDifficulty.DifficultyName(pending);
        if(!Conf.ShowSpeed) return body;
        return body + "  ·  "
            + PracticeDifficulty.CurrentPitch.ToString(System.Globalization.CultureInfo.InvariantCulture) + "%";
    }
    private sealed class Updater : MonoBehaviour {
        private string lastBody;
        private int lastDifficulty = int.MinValue;
        private int lastPending = int.MinValue;
        private int lastPitch = int.MinValue;
        private bool lastShowSpeed;
        private string lastLang;
        private bool bodyValid;
        private void OnEnable() {
            if(MainCore.Runtime != null) MainCore.Tr.OnLoadEnd += OnTranslationsLoaded;
        }
        private void OnDisable() {
            if(MainCore.Runtime != null) MainCore.Tr.OnLoadEnd -= OnTranslationsLoaded;
        }
        private void OnTranslationsLoaded(TranslationFailState state) => bodyValid = false;
        private void Update() {
            if(root == null || text == null) return;
            bool isReorganizing = UICore.IsReorganizing;
            bool show = ShouldShow();
            if(raycaster != null && raycaster.enabled != isReorganizing) raycaster.enabled = isReorganizing;
            if(root.gameObject.activeSelf != show) root.gameObject.SetActive(show);
            if(dragObj != null && dragObj.activeSelf != isReorganizing) dragObj.SetActive(isReorganizing);
            if(!show) return;
            if(isReorganizing) {
                Vector2 stored = OverlayCalibration.Unscale(root.anchoredPosition);
                Conf.OffsetX = stored.x;
                Conf.OffsetY = stored.y;
            }
            TMP_FontAsset font = FontManager.Current;
            if(text.font != font) text.font = font;
            int difficulty = PracticeDifficulty.CurrentDifficulty;
            int pending = PracticeDifficulty.PendingDifficulty;
            int pitch = PracticeDifficulty.CurrentPitch;
            bool showSpeed = Conf.ShowSpeed;
            string lang = MainCore.Tr.Language;
            if(bodyValid && difficulty == lastDifficulty && pending == lastPending
                && pitch == lastPitch && showSpeed == lastShowSpeed && lang == lastLang) return;
            lastLang = lang;
            lastDifficulty = difficulty;
            lastPending = pending;
            lastPitch = pitch;
            lastShowSpeed = showSpeed;
            bodyValid = true;
            string body = BuildBody();
            if(body == lastBody) return;
            text.text = body;
            lastBody = body;
        }
    }
}
public static class PracticeInput {
    private static float lastApply;
    internal static void Poll() {
        PracticeSettings conf = PracticeDifficulty.Conf;
        if(conf == null || !conf.Enabled || conf.Bindings.Count == 0) return;
        if(UICore.IsOpen || UICore.IsReorganizing) return;
        if(Time.unscaledTime - lastApply < 0.2f) return;
        foreach(PracticeBinding binding in conf.Bindings) {
            if(!Keybind.IsUsableKey(binding.Key) || !Input.GetKeyDown(binding.Key)) continue;
            lastApply = Time.unscaledTime;
            PracticeDifficulty.Apply(binding);
            return;
        }
    }
}

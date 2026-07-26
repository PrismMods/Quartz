using HarmonyLib;
using Quartz.Core;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;
using Object = UnityEngine.Object;
namespace Quartz.Features.Nostalgia;
public static partial class Nostalgia {
    private static bool SetUiActive(object instance, string field, bool active) {
        try {
            if(Traverse.Create(instance).Field(field).GetValue() is Component comp
               && comp != null && comp.gameObject.activeSelf != active)
                comp.gameObject.SetActive(active);
            return Traverse.Create(instance).Field(field).GetValue() != null;
        } catch {
            return false;
        }
    }
    private static void SetGoActive(Component comp, bool active) {
        try {
            if(comp != null && comp.gameObject.activeSelf != active)
                comp.gameObject.SetActive(active);
        } catch { }
    }
    private static readonly string[] DifficultyFields = {
        "difficultyContainer", "difficultyFadeContainer", "difficultyImage",
        "difficultyText", "difficultyButtonLeft", "difficultyButtonRight",
    };
    public static void ToggleDifficulty(bool show) {
        try {
            scrUIController ui = scrUIController.instance;
            scnEditor editor = scnEditor.instance;
            if(show) {
                if(editor != null) SetGoActive(editor.editorDifficultySelector, true);
                if(ui != null)
                    foreach(string f in DifficultyFields) SetUiActive(ui, f, true);
                return;
            }
            try { GCS.difficulty = Difficulty.Strict; } catch { }
            if(editor != null) {
                try { Traverse.Create(editor.editorDifficultySelector).Method("UpdateDifficultyDisplay").GetValue(); } catch { }
                SetGoActive(editor.editorDifficultySelector, false);
            }
            if(ui == null) return;
            if(AccessTools.Field(typeof(scrUIController), "currentDifficultyIndex") != null) {
                try {
                    Traverse.Create(ui).Field("currentDifficultyIndex").SetValue(2);
                    Traverse.Create(ui).Method("UpdateDifficultyUI").GetValue();
                } catch { }
            } else {
                try { Traverse.Create(ui).Method("UpdateDifficultyUI", Difficulty.Strict).GetValue(); } catch { }
            }
            try { GCS.difficulty = Difficulty.Strict; } catch { }
            foreach(string f in DifficultyFields) SetUiActive(ui, f, false);
        } catch(Exception e) {
            MainCore.Log.Wrn($"[Nostalgia] ToggleDifficulty failed: {e.Message}");
        }
    }
    public static void ToggleNoFail(bool show) {
        try {
            scnEditor editor = scnEditor.instance;
            scrUIController ui = scrUIController.instance;
            if(show) {
                if(editor != null) SetGoActive(editor.buttonNoFail, true);
                if(ui != null) SetUiActive(ui, "noFailImage", true);
                return;
            }
            try { GCS.useNoFail = false; } catch { }
            if(scrController.instance != null) {
                try { scrController.instance.noFail = false; } catch { }
            }
            if(editor != null) {
                try {
                    editor.buttonNoFail.GetComponent<Image>().color =
                        new Color(0.42352942f, 0.42352942f, 0.42352942f);
                } catch { }
                SetGoActive(editor.buttonNoFail, false);
            }
            if(ui != null) SetUiActive(ui, "noFailImage", false);
        } catch(Exception e) {
            MainCore.Log.Wrn($"[Nostalgia] ToggleNoFail failed: {e.Message}");
        }
    }
    private static Vector2? diffOrigPos;
    private static Component repositionSelectorSource;
    private static Component repositionNofailSource;
    private static RectTransform repositionDiffRect;
    private static RectTransform repositionNofailRect;
    public static void RepositionDifficulty() {
        if(!ShouldHideNoFail && !ShouldHideDifficulty) return;
        try {
            scnEditor editor = scnEditor.instance;
            if(editor == null || editor.editorDifficultySelector == null || editor.buttonNoFail == null) return;
            if(repositionSelectorSource != editor.editorDifficultySelector || repositionNofailSource != editor.buttonNoFail) {
                repositionSelectorSource = editor.editorDifficultySelector;
                repositionNofailSource = editor.buttonNoFail;
                repositionDiffRect = editor.editorDifficultySelector.GetComponent<RectTransform>();
                repositionNofailRect = editor.buttonNoFail.GetComponent<RectTransform>();
                diffOrigPos = null;
            }
            RectTransform diff = repositionDiffRect;
            RectTransform nofail = repositionNofailRect;
            if(diff == null || nofail == null) return;
            diffOrigPos ??= diff.anchoredPosition;
            bool move = ShouldHideNoFail && !ShouldHideDifficulty;
            Vector2 target = move
                ? new Vector2(nofail.anchoredPosition.x, diffOrigPos.Value.y)
                : diffOrigPos.Value;
            if(diff.anchoredPosition != target) diff.anchoredPosition = target;
        } catch { }
    }
    public static void ToggleSign(bool show) {
        NewsSign[] newsSigns = Object.FindObjectsByType<NewsSign>(FindObjectsSortMode.None);
        foreach(NewsSign newsSign in newsSigns) {
            if(!newsSign) continue;
            if(!newsSign.button.button) newsSign.button.button = newsSign.button.GetComponent<Button>();
            newsSign.button.transform.parent.gameObject.SetActive(show);
            SpriteRenderer[] renderers =
                Traverse.Create(newsSign).Field("spriteRenderers").GetValue<SpriteRenderer[]>();
            if(renderers != null)
                foreach(SpriteRenderer renderer in renderers) renderer.enabled = show;
        }
    }
    public static void SetBackground(bool forceDefault = false) {
        if(ADOBase.levelSelect == null) return;
        GameObject bg = UnityEngine.SceneManagement.SceneManager
            .GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == "BG");
        if(!bg || bg.transform.childCount < 3) return;
        bool old = !forceDefault && Enabled && Conf.OldBackground;
        int idx = Conf.OldBackgroundIndex;
        for(int i = 0; i < 3; i++) {
            bool active = (!old && i == 2) || (old && idx == i);
            bg.transform.GetChild(i).gameObject.SetActive(active);
        }
    }
    private static bool ebInitialized;
    private static Vector2 ebAutoPos, ebNofailPos, ebDiffPos;
    private static Vector2 ebAutoMax, ebNofailMax, ebDiffMax;
    private static Vector2 ebAutoMin, ebNofailMin, ebDiffMin;
    private static Vector2 ebNofailPivot;
    private static Rect ebNofailRect;
    public static void ChangeEditorButtons(bool change) {
        scnEditor editor = scnEditor.instance;
        if(editor == null) return;
        RectTransform auto = editor.autoImage.GetComponent<RectTransform>();
        RectTransform nofail = editor.buttonNoFail.GetComponent<RectTransform>();
        RectTransform difficulty = editor.editorDifficultySelector.GetComponent<RectTransform>();
        if(change) {
            if(!ebInitialized) {
                ebAutoPos = auto.offsetMin;
                ebNofailPos = nofail.offsetMin;
                ebDiffPos = difficulty.offsetMin;
                ebAutoMax = auto.offsetMax;
                ebNofailMax = nofail.offsetMax;
                ebDiffMax = difficulty.offsetMax;
                ebAutoMin = auto.offsetMin;
                ebNofailMin = nofail.offsetMin;
                ebDiffMin = difficulty.offsetMin;
                ebNofailPivot = nofail.pivot;
                ebNofailRect = nofail.rect;
                ebInitialized = true;
            }
            nofail.pivot = new Vector2(0.5f, 0.5f);
            auto.anchoredPosition = new Vector2(-15, 15);
            nofail.anchoredPosition = new Vector2(-52, 125);
            difficulty.anchoredPosition = new Vector2(-15, 10);
            auto.offsetMax = new Vector2(-15, 95);
            nofail.offsetMax = new Vector2(-22.5f, 155);
            difficulty.offsetMax = new Vector2(-15, 118);
            auto.offsetMin = new Vector2(-95, 15);
            nofail.offsetMin = new Vector2(-81.5f, 95);
            difficulty.offsetMin = new Vector2(-200, 10);
        } else {
            if(!ebInitialized) return;
            nofail.pivot = ebNofailPivot;
            auto.anchoredPosition = ebAutoPos;
            nofail.anchoredPosition = ebNofailPos;
            difficulty.anchoredPosition = ebDiffPos;
            auto.offsetMax = ebAutoMax;
            nofail.offsetMax = ebNofailMax;
            difficulty.offsetMax = ebDiffMax;
            auto.offsetMin = ebAutoMin;
            nofail.offsetMin = ebNofailMin;
            difficulty.offsetMin = ebDiffMin;
        }
    }
    public static void RemoveShadowAddOutline(bool rsao) {
        scnEditor editor = scnEditor.instance;
        if(editor == null) return;
        GameObject auto = editor.autoImage.gameObject;
        GameObject nofail = editor.buttonNoFail.gameObject;
        if(auto.GetComponent<Shadow>() is Outline) return;
        if(rsao) {
            auto.GetComponent<Shadow>().enabled = false;
            auto.GetOrAddComponent<Outline>().effectColor = Color.black;
            nofail.GetComponent<Shadow>().enabled = false;
            nofail.GetOrAddComponent<Outline>().effectColor = Color.black;
        } else {
            Object.Destroy(auto.GetComponent<Outline>());
            Shadow autoShadow = auto.GetComponent<Shadow>();
            if(autoShadow != null) autoShadow.enabled = true;
            Object.Destroy(nofail.GetComponent<Outline>());
            Shadow nofailShadow = nofail.GetComponent<Shadow>();
            if(nofailShadow != null) nofailShadow.enabled = true;
        }
    }
}

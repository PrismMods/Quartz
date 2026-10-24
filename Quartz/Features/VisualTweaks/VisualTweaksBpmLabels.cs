using System.Globalization;
using Quartz.Compat.Game;
using Quartz.Core;
using Quartz.Resource;
using Quartz.UI.Utility;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;
namespace Quartz.Features.VisualTweaks;
public static partial class VisualTweaks {
    public const int SpeedBpmShowBpm = 0;
    public const int SpeedBpmShowMultiplier = 1;
    public const int SpeedBpmShowBoth = 2;
    public const int SpeedBpmAbove = 0;
    public const int SpeedBpmBelow = 1;
    private const string SpeedUpColor = "#ff7a7a";
    private const string SlowDownColor = "#7ab8ff";
    private const float BpmLabelOffset = 0.85f;
    private const float BpmLabelFontScale = 0.5f * 0.7f;
    private static readonly List<GameObject> bpmLabels = [];
    private static bool ShouldShowSpeedBpm => Enabled && Conf.ShowSpeedChangeBpm;
    public static void RefreshSpeedBpmLabels() {
        List<scrFloor> floors = null;
        float bpm = 0f;
        try {
            scnGame game = ADOBase.customLevel;
            if(game != null && game.levelData != null) {
                floors = scrLevelMaker.instance?.listFloors;
                bpm = game.levelData.bpm;
            }
        } catch(Exception e) { Diag.Ignore(e); }
        RebuildSpeedBpmLabels(floors, bpm);
    }
    private static void ClearSpeedBpmLabels() {
        foreach(GameObject label in bpmLabels) {
            if(label != null) Object.Destroy(label);
        }
        bpmLabels.Clear();
    }
    private static void RebuildSpeedBpmLabels(List<scrFloor> floors, float levelBpm) {
        ClearSpeedBpmLabels();
        if(!ShouldShowSpeedBpm || floors == null || floors.Count < 2 || levelBpm <= 0f) return;
        float pitch = 1f;
        try {
            scrConductor conductor = scrConductor.instance;
            if(conductor != null && conductor.song != null) pitch = conductor.song.pitch;
        } catch(Exception e) { Diag.Ignore(e); }
        float prevSpeed = 1f;
        for(int i = 0; i < floors.Count; i++) {
            scrFloor floor = floors[i];
            if(floor == null) continue;
            float speed = floor.speed;
            if(i > 0 && prevSpeed > 0f && Mathf.Abs(speed - prevSpeed) > prevSpeed * 1e-4f) {
                try { CreateSpeedBpmLabel(floor, FormatSpeedBpm(levelBpm * pitch * speed, speed / prevSpeed)); }
                catch(Exception e) { Diag.Ignore(e); }
            }
            prevSpeed = speed;
        }
    }
    private static string FormatSpeedBpm(float bpm, float multiplier) {
        string b = bpm.ToString("0.##", CultureInfo.InvariantCulture);
        string m = "×" + multiplier.ToString("0.###", CultureInfo.InvariantCulture);
        string text = Conf.SpeedBpmDisplay switch {
            SpeedBpmShowMultiplier => m,
            SpeedBpmShowBoth => $"{b} <size=75%>({m})</size>",
            _ => b,
        };
        return $"<color={(multiplier > 1f ? SpeedUpColor : SlowDownColor)}>{text}</color>";
    }
    private static void CreateSpeedBpmLabel(scrFloor floor, string text) {
        scrLetterPress src = floor.editorNumText;
        if(src == null) return;
        GameObject clone = Object.Instantiate(src.gameObject, src.transform.parent);
        clone.name = "QuartzSpeedBpm";
        clone.transform.localRotation = src.transform.localRotation;
        clone.transform.localScale = src.transform.localScale;
        float dir = Conf.SpeedBpmPosition == SpeedBpmBelow ? -1f : 1f;
        clone.transform.position = src.transform.position + new Vector3(0f, dir * BpmLabelOffset * floor.transform.lossyScale.y, 0f);
        float baseSize = 24f;
        GameObject textGo = clone;
        Text gameText = clone.GetComponentInChildren<Text>(true);
        if(gameText != null) {
            baseSize = gameText.fontSize;
            textGo = gameText.gameObject;
            Object.DestroyImmediate(gameText);
        }
        foreach(scrLetterPress lp in clone.GetComponentsInChildren<scrLetterPress>(true)) Object.DestroyImmediate(lp);
        foreach(BaseMeshEffect fx in clone.GetComponentsInChildren<BaseMeshEffect>(true)) Object.DestroyImmediate(fx);
        TextMeshProUGUI tmp = textGo.AddComponent<TextMeshProUGUI>();
        tmp.font = FontManager.Current;
        tmp.alignment = TextAlignmentOptions.Center;
        TextCompat.NoWrap(tmp);
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.raycastTarget = false;
        tmp.richText = true;
        tmp.color = Color.white;
        tmp.fontSize = Mathf.Max(1f, baseSize * BpmLabelFontScale);
        tmp.text = text;
        float shadow = tmp.fontSize * 0.12f;
        TMPTextShadow.Apply(tmp, true, shadow, -shadow, 0f, new Color(0f, 0f, 0f, 0.6f));
        clone.SetActive(true);
        bpmLabels.Add(clone);
    }
}

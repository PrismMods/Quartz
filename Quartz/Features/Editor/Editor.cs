using Quartz.Compat.Interface;
using Quartz.Core;
using Quartz.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using ADOFAI;
using Object = UnityEngine.Object;
namespace Quartz.Features.Editor;
public static partial class EditorFeature {
    public static SettingsFile<EditorSettings> ConfMgr { get; private set; }
    public static EditorSettings Conf => ConfMgr?.Data;
    public static void EnsureConf() => ConfMgr ??= SettingsFile<EditorSettings>.Loaded("Editor.json");
    public static void Save() => ConfMgr?.RequestSave();
    private static bool Enabled {
        get {
            EnsureConf();
            return MainCore.IsModEnabled;
        }
    }
    internal static bool ShouldUseHorizontalProperties => Enabled && Conf.HorizontalProperties;
    public static readonly IRuntimeTick Ticker = new TickImpl();
    private sealed class TickImpl : IRuntimeTick {
        public void Tick() {
            Reconcile();
            ReconcileFloorReadout();
            ReconcileBga();
        }
    }
    public static void Apply() {
        Reconcile();
        ReconcileFloorReadout();
        ReconcileBga();
    }
    public static void Restore() {
        if(applied) DisableHorizontal();
        ClearReadout();
        RestoreBga();
    }
    private static bool applied;
    private static bool captured;
    private static LayoutSnapshot snapshot;
    private struct LayoutSnapshot {
        public RectOffset padding;
        public float spacing;
        public TextAnchor alignment;
        public bool controlWidth, controlHeight, expandWidth, expandHeight;
    }
    private const float LabelMinWidth = 140f;
    private struct LeSnapshot {
        public LayoutElement le;
        public bool created;
        public float min, pref, flex;
    }
    private static LeSnapshot labelLe;
    private static LeSnapshot controlLe;
    private static LeSnapshot ApplyLe(GameObject go, float min, float pref, float flex) {
        LeSnapshot s = default;
        if(go == null) return s;
        LayoutElement le = go.GetComponent<LayoutElement>();
        if(le == null) {
            le = go.AddComponent<LayoutElement>();
            s.created = true;
        } else {
            s.min = le.minWidth;
            s.pref = le.preferredWidth;
            s.flex = le.flexibleWidth;
        }
        s.le = le;
        le.minWidth = min;
        le.preferredWidth = pref;
        le.flexibleWidth = flex;
        return s;
    }
    private static void RestoreLe(ref LeSnapshot s) {
        if(s.le != null) {
            if(s.created) {
                Object.DestroyImmediate(s.le);
            } else {
                s.le.minWidth = s.min;
                s.le.preferredWidth = s.pref;
                s.le.flexibleWidth = s.flex;
            }
        }
        s = default;
    }
    private static void Reconcile() {
        bool want;
        try { want = ShouldUseHorizontalProperties; }
        catch { return; }
        try {
            if(want && !applied) {
                EnableHorizontal();
            } else if(!want && applied) {
                DisableHorizontal();
            }
        } catch {
        }
    }
    private static void EnableHorizontal() {
        GameObject template = ADOBase.gc?.prefab_property;
        if(template == null) return;
        VerticalLayoutGroup vertical = template.GetComponent<VerticalLayoutGroup>();
        if(vertical != null) {
            if(!captured) {
                snapshot = new LayoutSnapshot {
                    padding = vertical.padding,
                    spacing = vertical.spacing,
                    alignment = vertical.childAlignment,
                    controlWidth = vertical.childControlWidth,
                    controlHeight = vertical.childControlHeight,
                    expandWidth = vertical.childForceExpandWidth,
                    expandHeight = vertical.childForceExpandHeight,
                };
                captured = true;
            }
            Object.DestroyImmediate(vertical);
        }
        HorizontalLayoutGroup horizontal = template.GetComponent<HorizontalLayoutGroup>()
            ?? template.AddComponent<HorizontalLayoutGroup>();
        if(captured) {
            horizontal.padding = snapshot.padding;
            horizontal.spacing = snapshot.spacing;
            horizontal.childAlignment = snapshot.alignment;
        }
        horizontal.childControlWidth = true;
        horizontal.childControlHeight = true;
        horizontal.childForceExpandWidth = true;
        horizontal.childForceExpandHeight = true;
        Property prop = template.GetComponent<Property>();
        if(prop != null) {
            labelLe = ApplyLe(prop.label != null ? prop.label.gameObject : null, LabelMinWidth, -1f, 1f);
            controlLe = ApplyLe(prop.controlContainer != null ? prop.controlContainer.gameObject : null, 0f, 0f, 1f);
        }
        applied = true;
        RebuildInspector();
    }
    private static void DisableHorizontal() {
        GameObject template = ADOBase.gc?.prefab_property;
        if(template != null) {
            HorizontalLayoutGroup horizontal = template.GetComponent<HorizontalLayoutGroup>();
            if(horizontal != null) Object.DestroyImmediate(horizontal);
            RestoreLe(ref labelLe);
            RestoreLe(ref controlLe);
            if(captured) {
                VerticalLayoutGroup vertical = template.GetComponent<VerticalLayoutGroup>()
                    ?? template.AddComponent<VerticalLayoutGroup>();
                vertical.padding = snapshot.padding;
                vertical.spacing = snapshot.spacing;
                vertical.childAlignment = snapshot.alignment;
                vertical.childControlWidth = snapshot.controlWidth;
                vertical.childControlHeight = snapshot.controlHeight;
                vertical.childForceExpandWidth = snapshot.expandWidth;
                vertical.childForceExpandHeight = snapshot.expandHeight;
            }
            applied = false;
            RebuildInspector();
        } else {
            applied = false;
        }
    }
    private static void RebuildInspector() {
        try {
            scnEditor editor = scnEditor.instance;
            if(editor == null) return;
            InspectorPanel settings = editor.settingsPanel;
            InspectorPanel events = editor.levelEventsPanel;
            if(settings == null || events == null) return;
            RebuildPanel(settings, GCS.settingsInfo, isLevelEvents: false);
            RebuildPanel(events, GCS.levelEventsInfo, isLevelEvents: true);
            settings.ShowPanel(settings.selectedEventType, events.cacheEventIndex);
            events.HideAllInspectorTabs();
            events.ShowInspector(false, false);
        } catch {
        }
    }
    private static void RebuildPanel(
        InspectorPanel panel,
        Dictionary<string, LevelEventInfo> infos,
        bool isLevelEvents
    ) {
        if(panel.panelsList != null)
            foreach(PropertiesPanel built in panel.panelsList)
                if(built != null) Object.DestroyImmediate(built.gameObject);
        RectTransform tabs = panel.tabs;
        if(tabs != null)
            for(int i = tabs.childCount - 1; i >= 0; i--) Object.DestroyImmediate(tabs.GetChild(i).gameObject);
        panel.Init(infos, isLevelEvents);
    }
}

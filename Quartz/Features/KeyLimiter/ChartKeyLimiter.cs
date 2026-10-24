using ADOFAI;
using Quartz.Core;
using Quartz.IO;
using Quartz.Resource;
using UnityEngine;
using PropertyInfo = ADOFAI.PropertyInfo;
namespace Quartz.Features.KeyLimiter;
public enum KeyExceedMethod {
    Ignore,
    Kill,
    RunEvent,
    IgnoreAndRunEvent,
    KillAndRunEvent,
}
public enum RunEventBehaviour {
    Once,
    Repeat,
}
public static partial class ChartKeyLimiter {
    public const int EventTypeId = 35793570;
    public const string EventName = "KeyLimiter";
    public const string RequiredModName = "KeyLimiter";
    public const string SpriteKey = "quartz.keylimiter.chartevent";
    public const string PropEnabled = "enabled";
    public const string PropLimit = "limit";
    public const string PropExceed = "exceedBehaviour";
    public const string PropTargetTag = "targetTag";
    public const string PropRunBehaviour = "runEventBehaviour";
    public const string PropMessage = "message";
    public static LevelEventType EventType => (LevelEventType)EventTypeId;
    public static SettingsFile<ChartKeyLimiterSettings> ConfMgr { get; private set; }
    public static ChartKeyLimiterSettings Conf => ConfMgr?.Data;
    public static void EnsureConf() {
        if(ConfMgr != null) return;
        ConfMgr = SettingsFile<ChartKeyLimiterSettings>.Loaded("ChartKeyLimiter.json");
    }
    public static void Save() => ConfMgr?.RequestSave();
    public static bool IsEnabled() {
        EnsureConf();
        return MainCore.IsModEnabled && Conf != null && Conf.Enabled;
    }
    private static bool registered;
    private static LevelEventInfo eventInfo;
    public static bool Registered => registered;
    public static void Apply() {
        if(IsEnabled()) Register();
        else Unregister();
    }
    public static void Register() {
        if(registered) return;
        if(GCS.levelEventsInfo == null || GCS.levelEventTypeString == null) return;
        try {
            if(!EnumToStringPatch.Apply()) return;
            eventInfo ??= BuildEventInfo();
            if(eventInfo == null) return;
            Quartz.Features.Interop.RequiredModsGate.Provide(RequiredModName);
            GCS.levelEventsInfo[EventName] = eventInfo;
            GCS.levelEventTypeString[EventType] = EventName;
            EditorConstants.soloTypes?.Add(EventType);
            registered = true;
        } catch(Exception e) {
            MainCore.Log.Err($"[KeyLimiter] chart event registration failed: {e}");
        }
    }
    public static void Unregister() {
        if(!registered) return;
        registered = false;
        ChartKeyLimiterState.Instance.Clear();
        Quartz.Features.Interop.RequiredModsGate.Unprovide(RequiredModName);
        try {
            GCS.levelEventsInfo?.Remove(EventName);
            GCS.levelEventTypeString?.Remove(EventType);
            EditorConstants.soloTypes?.Remove(EventType);
        } catch(Exception e) {
            Diag.Warn(e, "removing the chart key limiter event from the editor registry");
        }
        EnumToStringPatch.Remove();
    }
    public static void RegisterEditorIcon() {
        if(!registered) return;
        try {
            Sprite sprite = SpriteRegistry.Get(SpriteKey);
            if(sprite == null) return;
            GCS.levelEventIcons?.TryAdd(EventType, sprite);
        } catch(Exception e) {
            Diag.Warn(e, "installing the chart key limiter editor icon");
        }
    }
    private static LevelEventInfo BuildEventInfo() {
        LevelEventInfo info = new() {
            name = EventName,
            type = EventType,
            isDecoration = false,
            useGroups = false,
            categories = [LevelEventCategory.Gameplay],
            executionTime = LevelEventExecutionTime.OnPrebar,
            allowFirstFloor = true,
        };
        Dictionary<string, PropertyInfo> properties = [];
        Add(properties, info, new Dictionary<string, object> {
            ["name"] = PropEnabled,
            ["type"] = "Bool",
            ["default"] = true,
            ["key"] = ChartKeyLimiterStrings.KeyEnabled,
        }, null);
        Add(properties, info, new Dictionary<string, object> {
            ["name"] = PropLimit,
            ["type"] = "Int",
            ["default"] = 4,
            ["key"] = ChartKeyLimiterStrings.KeyLimit,
            ["enableIf"] = new List<object> { PropEnabled, "true" },
        }, null);
        Add(properties, info, new Dictionary<string, object> {
            ["name"] = PropExceed,
            ["type"] = "Enum:Ease",
            ["default"] = "Linear",
            ["key"] = ChartKeyLimiterStrings.KeyExceed,
            ["enableIf"] = new List<object> { PropEnabled, "true" },
        }, new EnumFixup(typeof(KeyExceedMethod), nameof(KeyExceedMethod), KeyExceedMethod.Ignore));
        Add(properties, info, new Dictionary<string, object> {
            ["name"] = PropTargetTag,
            ["type"] = "String",
            ["default"] = "",
            ["key"] = ChartKeyLimiterStrings.KeyTargetTag,
            ["enableIf"] = new List<object> { PropEnabled, "true" },
            ["showIf"] = new List<object> {
                PropExceed, nameof(KeyExceedMethod.RunEvent),
                PropExceed, nameof(KeyExceedMethod.KillAndRunEvent),
                PropExceed, nameof(KeyExceedMethod.IgnoreAndRunEvent),
            },
        }, null);
        Add(properties, info, new Dictionary<string, object> {
            ["name"] = PropRunBehaviour,
            ["type"] = "Enum:Ease",
            ["default"] = "Linear",
            ["key"] = ChartKeyLimiterStrings.KeyRunBehaviour,
            ["enableIf"] = new List<object> { PropEnabled, "true" },
            ["showIf"] = new List<object> {
                PropExceed, nameof(KeyExceedMethod.RunEvent),
                PropExceed, nameof(KeyExceedMethod.KillAndRunEvent),
                PropExceed, nameof(KeyExceedMethod.IgnoreAndRunEvent),
            },
        }, new EnumFixup(typeof(RunEventBehaviour), nameof(RunEventBehaviour), RunEventBehaviour.Once));
        Add(properties, info, new Dictionary<string, object> {
            ["name"] = PropMessage,
            ["type"] = "String",
            ["default"] = "",
            ["key"] = ChartKeyLimiterStrings.KeyMessage,
            ["enableIf"] = new List<object> { PropEnabled, "true" },
            ["showIf"] = new List<object> {
                PropExceed, nameof(KeyExceedMethod.Kill),
                PropExceed, nameof(KeyExceedMethod.KillAndRunEvent),
            },
        }, null);
        info.propertiesInfo = properties;
        return info;
    }
    private readonly struct EnumFixup(Type type, string typeName, object defaultValue) {
        public readonly Type Type = type;
        public readonly string TypeName = typeName;
        public readonly object DefaultValue = defaultValue;
    }
    private static void Add(
        Dictionary<string, PropertyInfo> into,
        LevelEventInfo info,
        Dictionary<string, object> dict,
        EnumFixup? fixup
    ) {
        PropertyInfo property = new(dict, info);
        if(fixup is EnumFixup fix) {
            property.enumType = fix.Type;
            property.enumTypeString = fix.TypeName;
            property.value_default = fix.DefaultValue;
            property.enumExceptions = null;
        }
        into[property.name] = property;
    }
}

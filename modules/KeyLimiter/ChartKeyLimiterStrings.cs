using UnityEngine;
namespace Quartz.Features.KeyLimiter;
internal static class ChartKeyLimiterStrings {
    internal const string KeyEventName = "editor." + ChartKeyLimiter.EventName;
    internal const string KeyEnabled = "quartz.keyLimiter.event.enabled";
    internal const string KeyLimit = "quartz.keyLimiter.event.limit";
    internal const string KeyExceed = "quartz.keyLimiter.event.exceedBehaviour";
    internal const string KeyTargetTag = "quartz.keyLimiter.event.targetTag";
    internal const string KeyRunBehaviour = "quartz.keyLimiter.event.runEventBehaviour";
    internal const string KeyMessage = "quartz.keyLimiter.event.message";
    private const SystemLanguage En = SystemLanguage.English;
    private const SystemLanguage Ko = SystemLanguage.Korean;
    private const SystemLanguage Ja = SystemLanguage.Japanese;
    private const SystemLanguage Zh = SystemLanguage.Chinese;
    private const SystemLanguage ZhHans = SystemLanguage.ChineseSimplified;
    private const SystemLanguage ZhHant = SystemLanguage.ChineseTraditional;
    private static readonly Dictionary<string, Dictionary<SystemLanguage, string>> table = Build();
    internal static bool TryGet(string key, out string value) {
        value = null;
        if(string.IsNullOrEmpty(key)) return false;
        if(!table.TryGetValue(key, out Dictionary<SystemLanguage, string> byLanguage)) return false;
        if(byLanguage.TryGetValue(RDString.language, out value)) return true;
        return byLanguage.TryGetValue(En, out value);
    }
    private static Dictionary<string, Dictionary<SystemLanguage, string>> Build() {
        Dictionary<string, Dictionary<SystemLanguage, string>> t = new(StringComparer.Ordinal) {
            [KeyEventName] = Row("Key Limiter", "키 제한", "最大入力キー数", "输入键限制"),
            [KeyEnabled] = Row("Set to", "설정", "設定", "环境"),
            [KeyLimit] = Row("Keys", "최대 허용 키", "最大許容キー数", "最大按键数"),
            [KeyExceed] = Row(
                "Behaviour When Extra Presses", "초과 입력 시 이벤트",
                "超過入力時のイベント", "额外按下时的行为"),
            [KeyTargetTag] = Row("Target Tag", "이벤트 태그", "タグ", "标签"),
            [KeyRunBehaviour] = Row("Run Event Behaviour", "작동 방식", "仕組み", "发挥作用"),
            [KeyMessage] = Row("Death Message", "게임 오버 메시지", "メッセージ", "信息"),
        };
        AddEnum(t, nameof(KeyExceedMethod), nameof(KeyExceedMethod.Ignore),
            Row("Ignore", "입력 무시", "入力を無視", "忽略输入"));
        AddEnum(t, nameof(KeyExceedMethod), nameof(KeyExceedMethod.Kill),
            Row("Kill", "게임 오버", "殺す", "游戏结束"));
        AddEnum(t, nameof(KeyExceedMethod), nameof(KeyExceedMethod.RunEvent),
            Row("Run Event", "이벤트 실행", "実行", "事件执行"));
        AddEnum(t, nameof(KeyExceedMethod), nameof(KeyExceedMethod.IgnoreAndRunEvent),
            Row("Ignore and Run Event", "입력 무시 및 이벤트 실행", "入力を無視して実行", "忽略输入和事件执行"));
        AddEnum(t, nameof(KeyExceedMethod), nameof(KeyExceedMethod.KillAndRunEvent),
            Row("Kill and Run Event", "게임 오버 및 이벤트 실행", "殺して実行", "游戏结束和事件执行"));
        AddEnum(t, nameof(RunEventBehaviour), nameof(RunEventBehaviour.Once),
            Row("Once", "한 번만", "一度だけ", "就一次"));
        AddEnum(t, nameof(RunEventBehaviour), nameof(RunEventBehaviour.Repeat),
            Row("Repeat", "누를 때마다", "繰り返し", "重复"));
        return t;
    }
    private static void AddEnum(
        Dictionary<string, Dictionary<SystemLanguage, string>> t,
        string typeName,
        string valueName,
        Dictionary<SystemLanguage, string> row
    ) => t["enum." + typeName + "." + valueName] = row;
    private static Dictionary<SystemLanguage, string> Row(string en, string ko, string ja, string zh) => new() {
        [En] = en,
        [Ko] = ko,
        [Ja] = ja,
        [Zh] = zh,
        [ZhHans] = zh,
        [ZhHant] = zh,
    };
}

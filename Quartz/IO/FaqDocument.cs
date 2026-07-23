using Newtonsoft.Json.Linq;
namespace Quartz.IO;
public sealed class FaqEntry {
    public string Category;
    public string Question;
    public string Answer;
}
public static class FaqDocument {
    public const string FallbackLanguage = "en-US";
    public static List<FaqEntry> Parse(string json, string language) => Parse(JToken.Parse(json), language);
    public static List<FaqEntry> Parse(JToken root, string language) {
        List<FaqEntry> list = [];
        JArray entries = root as JArray ?? root?["entries"] as JArray;
        if(entries == null) return list;
        foreach(JToken token in entries) {
            if(token is not JObject obj) continue;
            string question = Text(obj["question"], language);
            string answer = Text(obj["answer"], language);
            if(string.IsNullOrWhiteSpace(question) && string.IsNullOrWhiteSpace(answer)) continue;
            list.Add(new FaqEntry {
                Category = Text(obj["category"], language)?.Trim(),
                Question = string.IsNullOrWhiteSpace(question) ? "?" : question.Trim(),
                Answer = answer?.Trim() ?? "",
            });
        }
        return list;
    }
    public static string Text(JToken token, string language) {
        switch(token) {
            case null:
            case JValue { Value: null }:
                return null;
            case JArray array: {
                List<string> lines = [];
                foreach(JToken line in array) {
                    string text = Text(line, language);
                    if(text != null) lines.Add(text);
                }
                return string.Join("\n", lines);
            }
            case JObject obj: {
                if(!string.IsNullOrEmpty(language) && obj[language] != null) return Text(obj[language], language);
                if(obj[FallbackLanguage] != null) return Text(obj[FallbackLanguage], language);
                foreach(JProperty property in obj.Properties()) return Text(property.Value, language);
                return null;
            }
            default:
                return token.ToString();
        }
    }
    public const string Default = """
{
  "_readme": [
    "This file fills the FAQ under Quartz's Help tab. Edit it, then press Reload on that tab.",
    "Each entry takes 'question', 'answer', and an optional 'category' that groups entries under a heading.",
    "'question', 'answer' and 'category' can each be plain text, a list of lines, or an object keyed by language: { \"en-US\": \"...\", \"ko-KR\": \"...\" }.",
    "Answers accept rich text: <b>bold</b>, <i>italic</i>, <color=#FF9999>color</color>, <size=20>size</size>.",
    "Delete this file and press Reload to bring the defaults back."
  ],
  "entries": [
    {
      "category": { "en-US": "Getting started", "ko-KR": "시작하기" },
      "question": { "en-US": "What is Quartz?", "ko-KR": "Quartz가 뭔가요?" },
      "answer": {
        "en-US": "An all-in-one mod for A Dance of Fire and Ice. It adds an in-game overlay (key viewer, progress bar, combo, judgement, song title, stat panels), gameplay and visual options, editor tools, and a built-in browser for TUF levels and packs.",
        "ko-KR": "A Dance of Fire and Ice용 올인원 모드입니다. 인게임 오버레이(키뷰어, 진행 바, 콤보, 판정, 곡 제목, 스탯 패널)와 게임플레이·비주얼 설정, 에디터 도구, TUF 레벨·팩 브라우저를 제공합니다."
      }
    },
    {
      "category": { "en-US": "Getting started", "ko-KR": "시작하기" },
      "question": { "en-US": "How do I open the Quartz menu?", "ko-KR": "Quartz 메뉴는 어떻게 여나요?" },
      "answer": {
        "en-US": "Press Alt + K in game. The shortcut lives in the Settings tab, and 'Show Quartz Settings at Startup' there opens the menu as soon as the game launches.",
        "ko-KR": "게임에서 Alt + K를 누르세요. 단축키는 설정 탭에서 바꿀 수 있고, 같은 탭의 '시작 시 Quartz 설정 창 열기'를 켜면 게임을 켤 때 메뉴가 바로 열립니다."
      }
    },
    {
      "category": { "en-US": "Getting started", "ko-KR": "시작하기" },
      "question": { "en-US": "Do I need MelonLoader or Unity Mod Manager?", "ko-KR": "MelonLoader와 Unity Mod Manager 중 뭐가 필요한가요?" },
      "answer": {
        "en-US": "Either one. Releases ship <b>Quartz.zip</b> for MelonLoader (recommended) and <b>QuartzUmm.zip</b> for UMM. Both are the same mod with the same menu — the UMM build does not use UMM's own settings panel. Install only one of them.",
        "ko-KR": "둘 중 하나면 됩니다. 릴리스에는 MelonLoader용 <b>Quartz.zip</b>(권장)과 UMM용 <b>QuartzUmm.zip</b>이 함께 올라갑니다. 같은 모드에 같은 메뉴이며, UMM 빌드도 UMM 자체 설정 패널을 쓰지 않습니다. 둘 중 하나만 설치하세요."
      }
    },
    {
      "category": { "en-US": "Files and settings", "ko-KR": "파일과 설정" },
      "question": { "en-US": "Where are my settings stored?", "ko-KR": "설정은 어디에 저장되나요?" },
      "answer": {
        "en-US": "In Quartz's data folder, next to this FAQ file — Settings.json, languages, fonts, profiles, addons and downloaded TUF levels all live there. Press <b>Open Folder</b> above to jump straight to it. Deleting Settings.json resets Quartz to defaults.",
        "ko-KR": "이 FAQ 파일과 같은 Quartz 데이터 폴더에 저장됩니다. Settings.json, 언어, 폰트, 프로필, 애드온, 내려받은 TUF 레벨이 모두 여기에 있습니다. 위의 <b>폴더 열기</b>를 누르면 바로 열립니다. Settings.json을 지우면 Quartz가 기본값으로 돌아갑니다."
      }
    },
    {
      "category": { "en-US": "Files and settings", "ko-KR": "파일과 설정" },
      "question": { "en-US": "How do I change the language?", "ko-KR": "언어는 어떻게 바꾸나요?" },
      "answer": {
        "en-US": "Settings tab → Language. Translations are community-maintained and Quartz refreshes them on launch, so a language can gain strings without a mod update.",
        "ko-KR": "설정 탭 → 언어에서 바꿉니다. 번역은 커뮤니티가 관리하며 Quartz가 실행할 때마다 최신 번역을 받아오기 때문에, 모드를 업데이트하지 않아도 번역이 채워집니다."
      }
    },
    {
      "category": { "en-US": "Files and settings", "ko-KR": "파일과 설정" },
      "question": { "en-US": "How do I install an addon?", "ko-KR": "애드온은 어떻게 설치하나요?" },
      "answer": {
        "en-US": "Addons tab → Add Addon, or drop a .qaddon / .dll into the Addons folder and press Reload Addons. Addons can add their own tabs to this menu.",
        "ko-KR": "애드온 탭 → 애드온 추가를 누르거나, Addons 폴더에 .qaddon / .dll 파일을 넣고 애드온 새로고침을 누르세요. 애드온은 이 메뉴에 자체 탭을 추가할 수 있습니다."
      }
    },
    {
      "category": { "en-US": "Troubleshooting", "ko-KR": "문제 해결" },
      "question": { "en-US": "Something broke — where do I look first?", "ko-KR": "뭔가 고장났어요. 어디부터 봐야 하나요?" },
      "answer": {
        "en-US": "The log. MelonLoader writes <b>MelonLoader/Latest.log</b> in your game folder; UMM writes <b>Player.log</b>. Errors from Quartz are tagged, and they usually name the exact feature that failed.",
        "ko-KR": "로그부터 보세요. MelonLoader는 게임 폴더의 <b>MelonLoader/Latest.log</b>에, UMM은 <b>Player.log</b>에 기록합니다. Quartz의 오류에는 태그가 붙어 있어서 어떤 기능이 실패했는지 바로 알 수 있습니다."
      }
    },
    {
      "category": { "en-US": "Troubleshooting", "ko-KR": "문제 해결" },
      "question": { "en-US": "How do I report a bug or ask for a feature?", "ko-KR": "버그 제보나 기능 제안은 어디서 하나요?" },
      "answer": {
        "en-US": "Open an issue at github.com/PrismMods/Quartz, or say so in the Discord (discord.gg/mAzAghu5Xq). Attach the log and the level you were playing — that answers most questions on its own.",
        "ko-KR": "github.com/PrismMods/Quartz에 이슈를 올리거나 디스코드(discord.gg/mAzAghu5Xq)에 알려주세요. 로그 파일과 플레이 중이던 레벨을 함께 올려주시면 대부분 바로 원인을 찾을 수 있습니다."
      }
    },
    {
      "category": { "en-US": "This page", "ko-KR": "이 페이지" },
      "question": { "en-US": "How do I edit this FAQ?", "ko-KR": "이 FAQ는 어떻게 수정하나요?" },
      "answer": {
        "en-US": "Press <b>Open File</b> above to edit FAQ.json, add or change entries, then press <b>Reload</b>. If the file has a syntax error Quartz keeps showing the defaults and prints the problem here.",
        "ko-KR": "위의 <b>파일 열기</b>로 FAQ.json을 연 뒤 항목을 고치거나 추가하고 <b>새로고침</b>을 누르세요. 파일에 문법 오류가 있으면 Quartz는 기본 FAQ를 계속 보여주고 이 화면에 오류를 표시합니다."
      }
    }
  ]
}
""";
}

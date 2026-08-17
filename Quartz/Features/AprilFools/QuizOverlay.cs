using System;
using GTweens.Builders;
using GTweens.Easings;
using GTweens.Tweens;
using Quartz.Core;
using Quartz.Resource;
using Quartz.Tween;
using Quartz.UI;
using Quartz.UI.Utility;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace Quartz.Features.AprilFools;
public static class QuizOverlay {
    private const int SortOrder = 32767;
    private const float CardWidth = 680f;
    private const float CardHeight = 480f;
    private const int MercyThreshold = 3;
    private const int SpamCount = 130;
    private const int StackCount = 8;
    private static readonly System.Random Rng = new();
    private static GameObject canvasObj;
    private static GameObject ownedEventSystem;
    private static RectTransform cardRect;
    private static TextMeshProUGUI titleText;
    private static TextMeshProUGUI chipText;
    private static TextMeshProUGUI questionText;
    private static TextMeshProUGUI counterText;
    private static readonly Image[] optionBgs = new Image[4];
    private static readonly TextMeshProUGUI[] optionLabels = new TextMeshProUGUI[4];
    private static readonly GameObject[] optionObjs = new GameObject[4];
    private static QuizQuestion current;
    private static QuizDifficulty difficulty;
    private static Action onPassed;
    private static int wrongCount;
    private static int shownIndex;
    private static bool busy;
    private static GTween pendingSeq;
    private static GTween shakeSeq;
    public static bool IsOpen => canvasObj != null;
    public static void Show(QuizDifficulty level, Action passed) {
        if(IsOpen) {
            onPassed = passed;
            return;
        }
        UICore.Close(true);
        difficulty = level;
        onPassed = passed;
        wrongCount = 0;
        shownIndex = 1;
        busy = false;
        Build();
        SetQuestion(QuizBank.Pick(difficulty));
        cardRect.localScale = Vector3.one * 0.8f;
        GTween pop = cardRect.GTScale(Vector3.one, 0.35f).SetEasing(Easing.OutBack);
        MainCore.TC.Play(pop);
        UnityEngine.SceneManagement.SceneManager.sceneUnloaded += OnSceneUnloaded;
    }
    private static void OnSceneUnloaded(UnityEngine.SceneManagement.Scene scene) => Close();
    public static void Close() {
        UnityEngine.SceneManagement.SceneManager.sceneUnloaded -= OnSceneUnloaded;
        pendingSeq?.Kill();
        shakeSeq?.Kill();
        pendingSeq = null;
        shakeSeq = null;
        if(canvasObj != null) UnityEngine.Object.Destroy(canvasObj);
        if(ownedEventSystem != null) UnityEngine.Object.Destroy(ownedEventSystem);
        canvasObj = null;
        ownedEventSystem = null;
        cardRect = null;
        titleText = null;
        chipText = null;
        questionText = null;
        counterText = null;
        Array.Clear(optionBgs, 0, optionBgs.Length);
        Array.Clear(optionLabels, 0, optionLabels.Length);
        Array.Clear(optionObjs, 0, optionObjs.Length);
        current = null;
        onPassed = null;
        busy = false;
    }
    private static void Build() {
        canvasObj = UnityUtils.CreateOverlayCanvas(
            "QuartzQuizCanvas", MainCore.Root.transform, SortOrder, out GraphicRaycaster raycaster);
        raycaster.enabled = true;
        if(EventSystem.current == null) {
            ownedEventSystem = new GameObject(
                "QuartzQuizEventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            UnityEngine.Object.DontDestroyOnLoad(ownedEventSystem);
        }
        GameObject dim = new("Dim");
        dim.transform.SetParent(canvasObj.transform, false);
        RectTransform dimRect = dim.AddComponent<RectTransform>();
        dimRect.anchorMin = Vector2.zero;
        dimRect.anchorMax = Vector2.one;
        dimRect.offsetMin = Vector2.zero;
        dimRect.offsetMax = Vector2.zero;
        Image dimImg = dim.AddComponent<Image>();
        dimImg.color = new Color(0f, 0f, 0f, 0.78f);
        dimImg.raycastTarget = true;
        if(difficulty == QuizDifficulty.Impossible) BuildSpamLayer();
        GameObject card = new("Card");
        card.transform.SetParent(canvasObj.transform, false);
        cardRect = card.AddComponent<RectTransform>();
        cardRect.anchorMin = new(0.5f, 0.5f);
        cardRect.anchorMax = new(0.5f, 0.5f);
        cardRect.pivot = new(0.5f, 0.5f);
        cardRect.sizeDelta = new(CardWidth, CardHeight);
        Image cardBg = card.AddComponent<Image>();
        cardBg.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P1024);
        cardBg.type = Image.Type.Sliced;
        cardBg.color = UIColors.PanelBG;
        {
            GameObject border = new("Border");
            border.transform.SetParent(card.transform, false);
            RectTransform rect = border.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new(-2f, -2f);
            rect.offsetMax = new(2f, 2f);
            Image img = border.AddComponent<Image>();
            img.sprite = MainCore.Spr.GetRing(14.5f, 3f);
            img.type = Image.Type.Sliced;
            img.color = UIColors.ObjectActive;
            img.raycastTarget = false;
        }
        titleText = MakeText(card.transform, "Title", new(0f, 190f), new(620f, 48f), 32f, Color.white);
        titleText.fontStyle = FontStyles.Bold;
        titleText.text = Tr("QUIZ_TITLE", "POP QUIZ!");
        chipText = MakeText(card.transform, "Chip", new(0f, 150f), new(400f, 26f), 15f,
            UIColors.ObjectActiveBright);
        chipText.text = DifficultyLabel();
        questionText = MakeText(card.transform, "Question", new(0f, 45f), new(600f, 160f), 26f, Color.white);
        questionText.enableAutoSizing = true;
        questionText.fontSizeMax = 26f;
        questionText.fontSizeMin = 12f;
        counterText = MakeText(card.transform, "Counter", new(0f, -216f), new(620f, 22f), 12f,
            new Color(1f, 1f, 1f, 0.45f));
        counterText.text = CounterLabel();
        for(int i = 0; i < 4; i++) BuildOption(card.transform, i);
        if(difficulty == QuizDifficulty.Impossible) BuildQuestionStack(card.transform);
    }
    private static void BuildOption(Transform parent, int index) {
        GameObject option = new("Option" + index);
        option.transform.SetParent(parent, false);
        RectTransform rect = option.AddComponent<RectTransform>();
        rect.anchorMin = new(0.5f, 0.5f);
        rect.anchorMax = new(0.5f, 0.5f);
        rect.pivot = new(0.5f, 0.5f);
        rect.anchoredPosition = new(index % 2 == 0 ? -160f : 160f, index < 2 ? -90f : -162f);
        rect.sizeDelta = new(300f, 62f);
        Image bg = option.AddComponent<Image>();
        bg.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P1024);
        bg.type = Image.Type.Sliced;
        bg.color = UIColors.ObjectBG;
        TextMeshProUGUI label = MakeText(option.transform, "Label", Vector2.zero, new(280f, 54f), 20f, Color.white);
        label.enableAutoSizing = true;
        label.fontSizeMax = 20f;
        label.fontSizeMin = 10f;
        int captured = index;
        EventTrigger trigger = option.AddComponent<EventTrigger>();
        UnityUtils.AddClickEvent(trigger, _ => OnOption(captured));
        UnityUtils.AddEvent(EventTriggerType.PointerEnter, _ => {
            if(!busy) bg.color = UIColors.ObjectButton;
        }, trigger);
        UnityUtils.AddEvent(EventTriggerType.PointerExit, _ => {
            if(!busy) bg.color = UIColors.ObjectBG;
        }, trigger);
        optionObjs[index] = option;
        optionBgs[index] = bg;
        optionLabels[index] = label;
    }
    private static TextMeshProUGUI MakeText(
        Transform parent, string name, Vector2 pos, Vector2 size, float fontSize, Color color) {
        GameObject obj = new(name);
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.AddComponent<RectTransform>();
        rect.anchorMin = new(0.5f, 0.5f);
        rect.anchorMax = new(0.5f, 0.5f);
        rect.pivot = new(0.5f, 0.5f);
        rect.anchoredPosition = pos;
        rect.sizeDelta = size;
        TextMeshProUGUI text = obj.AddComponent<TextMeshProUGUI>();
        text.font = FontManager.Current;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = TextAlignmentOptions.Center;
        text.verticalAlignment = VerticalAlignmentOptions.Middle;
        text.characterSpacing = -2f;
        text.raycastTarget = false;
        return text;
    }
    private static void BuildSpamLayer() {
        GameObject layer = new("Spam");
        layer.transform.SetParent(canvasObj.transform, false);
        RectTransform layerRect = layer.AddComponent<RectTransform>();
        layerRect.anchorMin = Vector2.zero;
        layerRect.anchorMax = Vector2.one;
        layerRect.offsetMin = Vector2.zero;
        layerRect.offsetMax = Vector2.zero;
        QuizQuestion[] pool = QuizBank.PoolFor(QuizDifficulty.Impossible);
        for(int i = 0; i < SpamCount; i++) {
            TextMeshProUGUI spam = MakeText(layer.transform, "Spam" + i,
                new((float)(Rng.NextDouble() * 1800.0 - 900.0), (float)(Rng.NextDouble() * 1000.0 - 500.0)),
                new(420f, 60f), (float)(Rng.NextDouble() * 10.0 + 8.0),
                new Color(1f, 1f, 1f, (float)(Rng.NextDouble() * 0.4 + 0.1)));
            spam.text = pool[Rng.Next(pool.Length)].Prompt;
            spam.rectTransform.localRotation =
                Quaternion.Euler(0f, 0f, (float)(Rng.NextDouble() * 80.0 - 40.0));
        }
    }
    private static void BuildQuestionStack(Transform card) {
        QuizQuestion[] pool = QuizBank.PoolFor(QuizDifficulty.Impossible);
        for(int i = 0; i < StackCount; i++) {
            TextMeshProUGUI ghost = MakeText(card, "Stack" + i,
                new((float)(Rng.NextDouble() * 40.0 - 20.0), 45f + (float)(Rng.NextDouble() * 40.0 - 20.0)),
                new(600f, 160f), 11f, new Color(1f, 1f, 1f, 0.4f));
            ghost.text = pool[Rng.Next(pool.Length)].Prompt;
            ghost.rectTransform.localRotation =
                Quaternion.Euler(0f, 0f, (float)(Rng.NextDouble() * 12.0 - 6.0));
        }
    }
    private static void SetQuestion(QuizQuestion question) {
        current = question;
        if(difficulty == QuizDifficulty.Impossible) {
            questionText.fontSizeMax = 10f;
            questionText.fontSizeMin = 6f;
            questionText.characterSpacing = -6f;
            questionText.text = question.Prompt + " " + question.Prompt + " " + question.Prompt;
        } else questionText.text = question.Prompt;
        for(int i = 0; i < 4; i++) {
            optionLabels[i].text = question.Options[i];
            optionBgs[i].color = UIColors.ObjectBG;
        }
        chipText.text = DifficultyLabel() + " · " + SubjectLabel(question.Subject);
        counterText.text = CounterLabel();
    }
    private static string SubjectLabel(QuizSubject subject) => subject switch {
        QuizSubject.Math => Tr("QUIZ_SUBJ_MATH", "Math"),
        QuizSubject.Language => Tr("QUIZ_SUBJ_LANGUAGE", "Language"),
        QuizSubject.Science => Tr("QUIZ_SUBJ_SCIENCE", "Science"),
        QuizSubject.Social => Tr("QUIZ_SUBJ_SOCIAL", "Social Studies"),
        QuizSubject.Arts => Tr("QUIZ_SUBJ_ARTS", "Arts"),
        _ => Tr("QUIZ_SUBJ_VOID", "???"),
    };
    private static void OnOption(int index) {
        if(busy || current == null) return;
        if(difficulty == QuizDifficulty.Impossible) {
            GradeImpossible(index);
            return;
        }
        if(index == current.CorrectIndex) {
            busy = true;
            optionBgs[index].color = UIColors.ObjectActiveMathOk;
            titleText.text = Tr("QUIZ_CORRECT", "Correct!");
            Delay(0.6f, Pass);
            return;
        }
        wrongCount++;
        shownIndex++;
        optionBgs[index].color = UIColors.ObjectActiveMathErr;
        Shake();
        if(wrongCount >= MercyThreshold) {
            busy = true;
            titleText.text = Tr("QUIZ_MERCY_TITLE", "...");
            questionText.text = Tr("QUIZ_MERCY", "Fine. The quiz gives up on you. Go in.");
            foreach(GameObject option in optionObjs)
                if(option != null) option.SetActive(false);
            Delay(1.2f, Pass);
            return;
        }
        busy = true;
        titleText.text = Tr("QUIZ_WRONG", "Wrong! Try this one.");
        Delay(0.55f, () => {
            busy = false;
            titleText.text = Tr("QUIZ_TITLE", "POP QUIZ!");
            SetQuestion(QuizBank.PickOther(difficulty, current));
        });
    }
    private static void GradeImpossible(int index) {
        busy = true;
        optionBgs[index].color = UIColors.ObjectActiveMathWarn;
        titleText.text = Tr("QUIZ_GRADING", "Grading 1,999,999 other submissions…");
        Delay(2.4f, () => {
            titleText.text = Tr("QUIZ_CLOSE_ENOUGH", "Close enough.");
            foreach(GameObject option in optionObjs)
                if(option != null) option.SetActive(false);
            Delay(0.8f, Pass);
        });
    }
    private static void Pass() {
        Action callback = onPassed;
        Close();
        try {
            callback?.Invoke();
        } catch(Exception e) {
            Diag.Warn(e, "AprilFools/QuizPass");
        }
    }
    private static void Shake() {
        if(cardRect == null) return;
        shakeSeq?.Kill();
        shakeSeq = GTweenSequenceBuilder.New()
            .Append(cardRect.GTAnchorPos(new Vector2(14f, 0f), 0.04f))
            .Append(cardRect.GTAnchorPos(new Vector2(-12f, 0f), 0.05f))
            .Append(cardRect.GTAnchorPos(new Vector2(8f, 0f), 0.05f))
            .Append(cardRect.GTAnchorPos(new Vector2(-5f, 0f), 0.04f))
            .Append(cardRect.GTAnchorPos(Vector2.zero, 0.04f))
            .Build();
        MainCore.TC.Play(shakeSeq);
    }
    private static void Delay(float seconds, Action action) {
        pendingSeq?.Kill();
        pendingSeq = GTweenSequenceBuilder.New()
            .AppendTime(seconds)
            .AppendCallback(() => action())
            .Build();
        MainCore.TC.Play(pendingSeq);
    }
    private static string CounterLabel() => difficulty == QuizDifficulty.Impossible
        ? string.Format(Tr("QUIZ_COUNTER", "Question {0} of {1}"), shownIndex, "2,147,483,647")
        : string.Format(Tr("QUIZ_COUNTER", "Question {0} of {1}"), shownIndex, "2,000,000");
    private static string DifficultyLabel() => difficulty switch {
        QuizDifficulty.Grade1to3 => Tr("QUIZ_DIFF_G1_3", "Difficulty: P1–P10"),
        QuizDifficulty.Grade4to5 => Tr("QUIZ_DIFF_G4_5", "Difficulty: P11–P20"),
        QuizDifficulty.Grade6to7 => Tr("QUIZ_DIFF_G6_7", "Difficulty: G1–G10"),
        QuizDifficulty.Grade8to9 => Tr("QUIZ_DIFF_G8_9", "Difficulty: G11–G20"),
        QuizDifficulty.Grade10to11 => Tr("QUIZ_DIFF_G10_11", "Difficulty: U1–U10"),
        QuizDifficulty.Grade12 => Tr("QUIZ_DIFF_G12", "Difficulty: U11–U20"),
        _ => Tr("QUIZ_DIFF_IMPOSSIBLE", "Difficulty: Impossible"),
    };
    private static string Tr(string key, string fallback) => MainCore.Tr.Get(key, fallback);
}

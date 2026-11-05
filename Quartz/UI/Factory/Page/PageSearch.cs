using GTweens.Builders;
using GTweens.Easings;
using GTweens.Extensions;
using Quartz.Core;
using Quartz.Localization;
using Quartz.Resource;
using Quartz.UI.Generator;
using Quartz.UI.Search;
using Quartz.UI.Utility;
using Quartz.Utility;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using TMPro;
namespace Quartz.UI.Factory.Page;
internal static class PageSearch {
    private sealed class Entry {
        public int State;
        public string Text;
        public string English;
        public string Section;
        public string SectionEnglish;
        public RectTransform Target;
        public SearchTip Tip;
        public bool IsCategory;
        public SearchText[] Label;
        public SearchText[] Context;
        public SearchText Detail;
        public int Score;
    }
    private const int MAX_RESULTS = 40;
    private const float INDEX_TTL_SECONDS = 10f;
    private const int MAX_LABEL_LENGTH = 60;
    private static List<Entry> cachedIndex;
    private static float cachedIndexTime;
    private static string cachedIndexLang;
    private static Entry topMatch;
    private sealed class ResultRow {
        public RectTransform Row;
        public UI.Objects.Impl.UIButton Button;
    }
    private static readonly List<ResultRow> resultRows = [];
    private static RectTransform resultsContainer;
    private static TextMeshProUGUI statusText;
    public static void Create(RectTransform parent) {
        cachedIndex = null;
        topMatch = null;
        resultRows.Clear();
        RectTransform content = Quartz.UI.Factory.PageFactory.CreateScrollablePage(parent);
        var inputRow = GenerateUI.Row(content.transform);
        var input = GenerateUI.Input(
            inputRow,
            "",
            "",
            RunSearch,
            "Search",
            MainCore.Spr.Get(UISprite.MagnifyingGlass128),
            "search_query"
        );
        input.Placeholder.gameObject.AddComponent<TextLocalization>().Init("SEARCH", "Search");
        input.InputField.characterLimit = 40;
        input.InputField.onSubmit.AddListener(_ => {
            if(topMatch != null) Navigate(topMatch);
        });
        var statusRow = GenerateUI.Row(content.transform, 32f);
        statusText = GenerateUI.AddMutedText(statusRow, 18f);
        GameObject results = new("Results");
        results.transform.SetParent(content.transform, false);
        resultsContainer = results.AddComponent<RectTransform>();
        GenerateUI.FitVertical(results, 8f);
        RunSearch("");
    }
    private static string TabName(int state) {
        Quartz.UI.Nav.NavPage page = Quartz.UI.Nav.NavRegistry.ByState(state);
        if(page == null) return "?";
        return page.SearchLabel != null ? page.SearchLabel() : GenerateUI.Tr(page.LocaleKey, page.Title);
    }
    private static void RunSearch(string query) {
        if(resultsContainer == null) return;
        topMatch = null;
        string[][] tokens = SearchMatcher.Tokens(query ?? "", StringUtils.Normalize);
        if(tokens.Length == 0) {
            statusText.text = GenerateUI.Tr("SEARCH_HINT", "Type to search every tab — categories, toggles, buttons, everything.");
            HideRowsFrom(0);
            return;
        }
        string phrase = StringUtils.Normalize(query);
        List<Entry> index = Index();
        List<Entry> matches = Match(index, tokens, phrase, false);
        if(matches.Count == 0) matches = Match(index, tokens, phrase, true);
        if(matches.Count == 0) {
            statusText.text = GenerateUI.Tr("SEARCH_NO_RESULTS", "No results.");
            HideRowsFrom(0);
            return;
        }
        matches.Sort((a, b) => {
            if(a.Score != b.Score) return b.Score.CompareTo(a.Score);
            if(a.IsCategory != b.IsCategory) return a.IsCategory ? -1 : 1;
            if(a.Text.Length != b.Text.Length) return a.Text.Length.CompareTo(b.Text.Length);
            return string.Compare(a.Text, b.Text, StringComparison.OrdinalIgnoreCase);
        });
        topMatch = matches[0];
        int shown = Mathf.Min(matches.Count, MAX_RESULTS);
        string count = matches.Count > MAX_RESULTS
            ? string.Format(GenerateUI.Tr("SEARCH_RESULTS_CAPPED", "{0} results (showing first {1})"), matches.Count, MAX_RESULTS)
            : string.Format(GenerateUI.Tr("SEARCH_RESULTS", "{0} result(s)"), matches.Count);
        statusText.text = count + " · " + GenerateUI.Tr("SEARCH_ENTER_HINT", "Press Enter to jump to the top result.");
        for(int i = 0; i < shown; i++)
            ShowResultRow(i, matches[i]);
        HideRowsFrom(shown);
    }
    private static List<Entry> Match(List<Entry> index, string[][] tokens, string phrase, bool fuzzy) {
        List<Entry> matches = [];
        foreach(Entry e in index) {
            e.Score = SearchMatcher.Score(tokens, phrase, e.Label, e.Context, e.Detail, fuzzy);
            if(e.Score > 0) matches.Add(e);
        }
        return matches;
    }
    private static void ShowResultRow(int index, Entry e) {
        while(resultRows.Count <= index) {
            var row = GenerateUI.Row(resultsContainer);
            var btn = GenerateUI.Button(row, null, "", "search_result").SetSecondary();
            btn.Label.overflowMode = TextOverflowModes.Ellipsis;
            btn.Label.fontSize = 19f;
            TextLocalization loc = btn.Label.GetComponent<TextLocalization>();
            if(loc != null) Object.Destroy(loc);
            resultRows.Add(new ResultRow { Row = row, Button = btn });
        }
        ResultRow slot = resultRows[index];
        string text = e.Text.Length > 60 ? e.Text[..57] + "…" : e.Text;
        string path = string.IsNullOrEmpty(e.Section)
            ? TabName(e.State)
            : $"{TabName(e.State)} › {e.Section}";
        slot.Button.OnClick = () => Navigate(e);
        slot.Button.Label.text = $"<alpha=#77>{path} ›<alpha=#FF> {text}";
        slot.Row.gameObject.SetActive(true);
    }
    private static void HideRowsFrom(int from) {
        for(int i = from; i < resultRows.Count; i++)
            if(resultRows[i].Row != null) resultRows[i].Row.gameObject.SetActive(false);
    }
    private static List<Entry> Index() {
        if(cachedIndex != null
            && cachedIndexLang == MainCore.Conf.Language
            && Time.unscaledTime - cachedIndexTime < INDEX_TTL_SECONDS) {
            return cachedIndex;
        }
        cachedIndex = BuildIndex();
        bool korean = MainCore.Conf.Language == "ko-KR";
        Dictionary<int, SearchText[]> pages = [];
        foreach(Entry e in cachedIndex) {
            if(!pages.TryGetValue(e.State, out SearchText[] page))
                pages[e.State] = page = Texts(korean, TabName(e.State), Quartz.UI.Nav.NavRegistry.ByState(e.State)?.Title);
            // Label in the UI language and in English, so a Korean menu still answers "note rain".
            e.Label = Texts(korean, e.Text, e.English);
            e.Context = [.. Texts(korean, e.Section, e.SectionEnglish), .. page];
            string tip = null;
            if(e.Tip != null && e.Tip.Text != null) {
                try { tip = e.Tip.Text(); } catch(Exception ex) { Diag.Ignore(ex); }
            }
            e.Detail = string.IsNullOrWhiteSpace(tip) ? null : new SearchText(tip, StringUtils.Normalize);
        }
        cachedIndexTime = Time.unscaledTime;
        cachedIndexLang = MainCore.Conf.Language;
        return cachedIndex;
    }
    // Each distinct string, plus its 초성 form in Korean so "ㄴㅌ" still finds 노트 while full syllables match exactly.
    private static SearchText[] Texts(bool korean, params string[] raws) {
        List<SearchText> list = [];
        HashSet<string> seen = [];
        foreach(string raw in raws) {
            if(string.IsNullOrWhiteSpace(raw) || !seen.Add(raw)) continue;
            list.Add(new SearchText(raw, StringUtils.Normalize));
            if(!korean) continue;
            string chosung = StringUtils.NormalizeToHangulChosung(raw);
            if(chosung != raw) list.Add(new SearchText(chosung, StringUtils.Normalize));
        }
        return [.. list];
    }
    private static List<Entry> BuildIndex() {
        Quartz.UI.Factory.PageFactory.EnsureAllBuilt();
        List<Entry> list = [];
        foreach(KeyValuePair<int, RectTransform> page in UICore.Pages) {
            if(page.Key == Quartz.UI.Nav.NavRegistry.StateFor(Quartz.UI.Nav.CorePages.SearchPageKey) || page.Value == null) continue;
            Walk(page.Value, page.Key, null, null, list);
        }
        return list;
    }
    private static void Walk(Transform t, int state, string section, string sectionEnglish, List<Entry> list) {
        for(int i = 0; i < t.childCount; i++) {
            Transform child = t.GetChild(i);
            string name = child.name;
            if(name == "List") continue;
            if(name.StartsWith("Section_")) {
                Transform header = child.Find("Header/Bar/Label");
                TMP_Text headerLabel = header != null ? header.GetComponent<TMP_Text>() : null;
                TextLocalization headerLoc = header != null ? header.GetComponent<TextLocalization>() : null;
                string title = headerLabel != null ? headerLabel.text : name["Section_".Length..];
                string titleEnglish = headerLoc != null ? headerLoc.Default : null;
                list.Add(new Entry {
                    State = state,
                    Text = title,
                    English = titleEnglish,
                    Section = section,
                    SectionEnglish = sectionEnglish,
                    Target = (RectTransform)child,
                    IsCategory = true,
                });
                Transform body = child.Find("Body");
                if(body != null) Walk(body, state, title, titleEnglish, list);
                continue;
            }
            // Any localized label, whatever the control named it (ColorLabel, BindLabel…); long ones are hints, not settings.
            if(child.TryGetComponent(out TMP_Text tmp) && child.TryGetComponent(out TextLocalization loc)) {
                string text = tmp.text;
                if(!string.IsNullOrWhiteSpace(text) && text.Length <= MAX_LABEL_LENGTH) {
                    RectTransform target = RowTarget(child, state);
                    if(target != null) {
                        list.Add(new Entry {
                            State = state,
                            Text = text,
                            English = loc.Default,
                            Section = section,
                            SectionEnglish = sectionEnglish,
                            Target = target,
                            Tip = target.GetComponentInChildren<SearchTip>(true),
                        });
                    }
                }
            }
            Walk(child, state, section, sectionEnglish, list);
        }
    }
    private static RectTransform RowTarget(Transform label, int state) {
        RectTransform page = UICore.Pages[state];
        Transform cur = label;
        while(cur.parent != null && cur != page) {
            if(cur.parent.GetComponent<VerticalLayoutGroup>() != null) return cur as RectTransform;
            cur = cur.parent;
        }
        return null;
    }
    private static void Navigate(Entry e) {
        if(e.Target == null) return;
        // Open what the row sits in, outermost first: a tab that only shows it on demand, then collapsed sections.
        SearchReveal[] reveals = e.Target.GetComponentsInParent<SearchReveal>(true);
        for(int i = reveals.Length - 1; i >= 0; i--) {
            try { reveals[i].Reveal?.Invoke(); } catch(Exception ex) { Diag.Warn(ex, "Search/Reveal"); }
        }
        foreach(GenerateUI.CollapsibleSection s in GenerateUI.Sections) {
            if(s.Body == null) continue;
            if(e.Target == s.Section || e.Target.IsChildOf(s.Body)) s.SetExpanded(true, false, false);
        }
        MenuFactory.SetState(e.State);
        // Innermost scroller brings the row into view, each outer one brings the inner scroller's viewport.
        RectTransform focus = e.Target;
        foreach(UIScrollController scroller in e.Target.GetComponentsInParent<UIScrollController>(true)) {
            if(scroller.content == null) continue;
            LayoutRebuilder.ForceRebuildLayoutImmediate(scroller.content);
            Vector3 worldCenter = focus.TransformPoint(focus.rect.center);
            float localY = scroller.content.InverseTransformPoint(worldCenter).y;
            scroller.ScrollTo(-localY - focus.rect.height * 0.5f - 8f);
            if(scroller.viewport != null) focus = scroller.viewport;
        }
        Flash(e.Target);
    }
    // Blink, hold, fade: a single soft fade was easy to miss.
    private static void Flash(RectTransform target) {
        GameObject flash = new("SearchFlash");
        flash.transform.SetParent(target, false);
        RectTransform rect = flash.AddComponent<RectTransform>();
        // Rows with a layout group would otherwise lay the flash out as one more cell.
        flash.AddComponent<LayoutElement>().ignoreLayout = true;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        Image img = flash.AddComponent<Image>();
        img.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P2048);
        img.type = Image.Type.Sliced;
        Color accent = UIColors.ObjectActive;
        img.color = new Color(accent.r, accent.g, accent.b, 0.55f);
        img.raycastTarget = false;
        GTweens.Tweens.GTween Alpha(float to, float duration) => GTweenExtensions.Tween(
            () => img == null ? 0f : img.color.a,
            a => {
                if(img == null) return;
                Color c = img.color;
                c.a = a;
                img.color = c;
            },
            to,
            duration
        ).SetEasing(Easing.OutSine);
        var seq = GTweenSequenceBuilder.New()
            .AppendTime(0.2f)
            .Append(Alpha(0.1f, 0.18f))
            .Append(Alpha(0.55f, 0.18f))
            .AppendTime(0.9f)
            .Append(Alpha(0f, 1f))
            .AppendCallback(() => {
                if(flash != null) Object.Destroy(flash);
            })
            .Build();
        MainCore.TC.Play(seq);
    }
}

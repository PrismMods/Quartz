#if DEBUG && !IL2CPP
using System.Collections;
using Facet;
using UnityEngine;
using UnityEngine.UIElements;
namespace Quartz.UI.FacetUi;
/// <summary>
/// The third round of the player's look notes: Korean draws from one face (SUIT, no OS fallback), the inspector's Back is an
/// underlined muted text link, and Profiles keeps its name field and its tools on one row. Runs from Polish.
/// </summary>
internal static partial class FacetSelfTestChecks {
    // The menu bar, the tab rails and the sidebar the player reported uneven, syllable for syllable.
    private const string HangulSample = "레이아웃파일편집삽입정렬보기일반키뷰어진행도바콤보판정노래제목패널보정홈기능오버레이게임플레이비주얼트윅에디터프로필가져오기설정";

    private static IEnumerator Round3(Action<string, bool, string> check) {
        UnityEngine.TextCore.Text.FontAsset sans = Fonts.Regular(QuartzHost.Host);
        string missing = sans == null ? "no face" : new string([.. HangulSample.Where(c => !sans.HasCharacter(c, false, true))]);
        check("round 3: every Korean syllable of the menu bar, tab rails and sidebar draws from the sans face itself, not a fallback",
            sans != null && missing.Length == 0, $"face={sans?.name} missing='{missing}'");

        QuartzShell.Go(KeyViewerPage);
        yield return Frames(2);
        Button back = Body.Q<Button>("KEYVIEWER_BACK");
        Theme th = back != null ? Theme.For(back) : null;
        check("round 3: the inspector's Back is an underlined '← ' text link in the muted colour with no box",
            back != null && back.text.StartsWith("<u>←") && back.text.EndsWith("</u>")
            && back.style.color.value == th![Role.TextMuted] && back.resolvedStyle.backgroundColor.a < 0.01f,
            $"text={back?.text} colour={back?.style.color.value} bg={back?.resolvedStyle.backgroundColor}");

        QuartzShell.Go("profiles.main");
        yield return Frames(2);
        VisualElement field = PageContent().Query<TextField>().ToList().FirstOrDefault(),
            import = PageContent().Query<Button>().ToList().FirstOrDefault(b => b.name?.StartsWith("PROFILE_IMPORT") == true);
        bool oneRow = field != null && import != null && Mathf.Abs(field.worldBound.center.y - import.worldBound.center.y) <= 2f;
        check("round 3: Profiles keeps the name field and Import on one row", oneRow,
            $"field={field?.worldBound} import={import?.worldBound}");
    }
}
#endif

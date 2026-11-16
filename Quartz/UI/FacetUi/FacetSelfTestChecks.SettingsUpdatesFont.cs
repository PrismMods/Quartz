#if DEBUG && !IL2CPP
using System.Collections;
using System.Reflection;
using Quartz.Core;
using Quartz.Resource;
using Quartz.UI.Nav;
using Quartz.Update;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;
namespace Quartz.UI.FacetUi;
/// <summary>
/// Settings' Updates, Font and Settings Window Font cards against the classic page's sections: rows, search hits, Korean
/// text, the rename and delete rows, and a font size write reaching the setting and its file. It never changes the font or
/// the update channel and never presses Check for updates.
/// </summary>
internal static partial class FacetSelfTestChecks {
    private static readonly string[] UpdateStateButtons = ["UPDATE_NOTES", "UPDATE_SKIP", "UPDATE_INSTALL", "UPDATE_UNDO", "UPDATE_RESTART"];

    private static IEnumerator SettingsUpdatesFont(Action<string, bool, string> check, Action<string> info) {
        const string page = CorePages.SettingsPageKey;
        string[] rows = ["UPDATE_CHANNEL_LABEL", "CHECK_FOR_UPDATES", "FONT_DROPDOWN", "FONT_ADD", "FONT_SCALE", "FONT_LINE_SPACING", "SETTINGS_FONT_DROPDOWN"];
        Dictionary<string, (float Scale, float LineSpacing)> adjust = null;
        yield return FormPage("overlay", page, () => adjust = new(MainCore.Conf.FontAdjust), () => {
            if(adjust == null) return;
            string font = FontManager.CurrentName;
            MainCore.Conf.FontAdjust.Clear();
            foreach(var kv in adjust) MainCore.Conf.FontAdjust[kv.Key] = kv.Value;
            var (scale, line) = FontManager.GetAdjust(font);
            FontManager.SetAdjust(font, scale, line);   // the face follows the restored values
            MainCore.ConfMgr.Save();
        }, () => Checks());

        IEnumerator Checks() {
            UpdateStatus status = UpdateService.Status;
            bool states = status is UpdateStatus.Available or UpdateStatus.Skipped or UpdateStatus.Installed;
            if(states) info($"{page}: the update-state buttons' hiding: not run, status={status}");
            yield return Parity(check, page, rows, states ? null : UpdateStateButtons);
            Searched(check, page, ["UPDATE_CHANNEL_LABEL", "CHECK_FOR_UPDATES", "FONT_ADD", "FONT_SCALE", "FONT_LINE_SPACING"]);
            Translated(check, page, [.. rows.Where(k => !k.EndsWith("_DROPDOWN")), .. UpdateStateButtons, "FONT_NAME", "FONT_RENAME", "FONT_DELETE", "UPDATES", "FONT", "SETTINGS_FONT"]);

            bool custom = FontManager.IsCustomFont(FontManager.CurrentName);
            string[] manage = ["FONT_NAME", "FONT_RENAME", "FONT_DELETE"];
            string[] wrong = [.. manage.Where(id => ShownRow(id) != custom)];
            check($"{page}: the font rename and delete rows show exactly for a custom font",
                manage.All(id => PageRow(id) != null) && wrong.Length == 0, $"custom={custom} font={FontManager.CurrentName} wrong={string.Join(",", wrong)}");

            yield return DetachedWithShell();
            yield return FontScaleWrite();
        }

        // The menu is open, so unlike the throwaway builds this one makes every card, the Updates card's hook included. A
        // face whose font FontManager destroyed (renamed or deleted) must go at the build.
        IEnumerator DetachedWithShell() {
            var faces = (Dictionary<TMP_FontAsset, FontAsset>)typeof(ShellSettings).GetField("Faces", Static | BindingFlags.NonPublic).GetValue(null);
            TMP_FontAsset gone = ScriptableObject.CreateInstance<TMP_FontAsset>();
            FontAsset face = FontAsset.CreateFontAsset(FontManager.UiFont.sourceFontFile);
            faces[gone] = face;
            UnityEngine.Object.DestroyImmediate(gone);
            int[] before = Listeners();
            var detached = new VisualElement();
            ShellSettings.Build(detached);
            int[] after = Listeners();
            bool built = detached.Q(name: "CHECK_FOR_UPDATES") != null && detached.Q(name: "FONT_DROPDOWN") != null, kept = faces.ContainsKey(gone);
            check($"{page}: a detached build with the menu open makes the Updates card without subscribing to an event", built && before.SequenceEqual(after),
                $"built={built} listeners {string.Join("/", before)} -> {string.Join("/", after)}");
            yield return null;
            check($"{page}: a build drops the face of a font that was renamed or deleted", !kept && face == null, $"kept={kept} faceAlive={face != null}");
            if(kept) faces.Remove(gone);
            if(face != null) UnityEngine.Object.Destroy(face);
        }

        IEnumerator FontScaleWrite() {
            string font = FontManager.CurrentName, path = MainCore.ConfMgr.Path;
            float was = FontManager.GetAdjust(font).Scale, v = Mathf.Round((was >= 1.5f ? was - 0.25f : was + 0.25f) * 100f) / 100f;
            byte[] before = Bytes(path);
            bool slid = SlideTo("FONT_SCALE", FontManager.MinAdjust, FontManager.MaxAdjust, v), moved = false, saved = false;
            for(float t0 = Time.realtimeSinceStartup; slid && Time.realtimeSinceStartup - t0 < 2f;) {
                yield return null;
                moved = Mathf.Abs(FontManager.GetAdjust(font).Scale - v) < 0.006f;
                saved = !SameBytes(Bytes(path), before);
                if(moved && saved) break;
            }
            check($"{page} FONT_SCALE: a write here moves the font adjust and saves", slid && moved && saved,
                $"slider={slid} font={font} was={was} want={v} now={FontManager.GetAdjust(font).Scale} saved={saved}");
        }
    }

    private static VisualElement PageRow(string id) => PageContent().Q(name: id) ?? PageContent().Q(name: id + "-input");

    // Present, and displayed up to the page.
    private static bool ShownRow(string id) {
        VisualElement page = PageContent(), e = PageRow(id);
        for(; e != null && e != page; e = e.parent) if(e.resolvedStyle.display != DisplayStyle.Flex) return false;
        return e == page;
    }
}
#endif

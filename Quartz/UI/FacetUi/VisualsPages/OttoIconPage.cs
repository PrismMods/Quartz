#if !IL2CPP
using Facet;
using Quartz.Core;
using Quartz.Features.OttoIcon;
using UnityEngine.UIElements;
namespace Quartz.UI.FacetUi.VisualsPages;
/// <summary>Visuals ▸ Otto Icon in the new menu, row for row as PageOttoIcon. The offsets apply on every write and save on
/// release.</summary>
internal static class OttoIconPage {
    private static OttoIconSettings C => OttoIcon.Conf;
    private static readonly OttoIconSettings Def = new();

    internal static void Build(VisualElement p) {
        OttoIcon.EnsureConf();
        ShellPage.Feature(p, "ottoicon", "SECTION_OTTO_ICON", "OTTOICON_DESC", null, Cards);
    }

    private static void Cards(VisualElement p) {
        Action apply = OttoIcon.Refresh, save = OttoIcon.Save;
        VisualElement card = Controls.Card(p, null);
        ShellRows.FilePick(card, "OTTO_IMAGE", "OTTO_IMAGE_CLEAR",
            () => OttoIcon.HasCustomImage ? Path.GetFileName(C.ImagePath) : MainCore.Tr.Get("OTTO_IMAGE_DEFAULT", "Using the built-in cat"), () => {
                UICore.ClassicStale = true;   // the classic page reads the file name at build
                OttoIcon.ImportImage(out _);
            }, () => {
                UICore.ClassicStale = true;
                OttoIcon.ClearImage();
            }, "DESC_OTTO_IMAGE");
        ShellRows.Toggle(card, "OTTO_TINT_IMAGE", "otto_tint_image", Bind.To(() => C.TintImage, v => C.TintImage = v), apply, save,
            o => { o.Default = Def.TintImage; o.Tooltip = "DESC_OTTO_TINT_IMAGE"; });
        ShellRows.Colour(card, "OTTO_COLOR", () => C.GetColor(), c => C.SetColor(c), apply, save, configure: o => o.Default = Def.GetColor());
        ShellRows.Toggle(card, "OTTO_HIGHBPM_ON", "otto_highbpm_on", Bind.To(() => C.UseHighBpmColor, v => C.UseHighBpmColor = v), apply, save,
            o => { o.Default = Def.UseHighBpmColor; o.Tooltip = "DESC_OTTO_HIGHBPM_ON"; });
        ShellRows.Colour(card, "OTTO_HIGHBPM_COLOR", () => C.GetHighBpmColor(), c => C.SetHighBpmColor(c), apply, save, configure: o => {
            o.Default = Def.GetHighBpmColor();
            o.VisibleWhen = () => C.UseHighBpmColor;
        });
        ShellRows.ReleaseSlider(card, "OTTO_OFFSET_X", Bind.To(() => C.OffsetX, v => C.OffsetX = v), -100f, 100f, 1f, "0", apply, save,
            o => o.Default = Def.OffsetX);
        ShellRows.ReleaseSlider(card, "OTTO_OFFSET_Y", Bind.To(() => C.OffsetY, v => C.OffsetY = v), -100f, 100f, 1f, "0", apply, save,
            o => o.Default = Def.OffsetY);
    }
}
#endif

#if !IL2CPP
using Facet;
using Quartz.Core;
using Quartz.Prism;
using Quartz.Resource;
using UnityEngine.UIElements;
namespace Quartz.UI.FacetUi;
/// <summary>
/// The small menu the menu hotkey opens while the new menu is on: the Quartz logo, then Settings, Edit layout and Close.
/// Centred on its own surface, so it wears the suite look whenever the new menu does; it keeps no geometry. With
/// the shared UI library it wears the hotkey-menu look every Prism mod shares (a wash behind it, no card, centred labels,
/// and, where the library has them, the old hub's large buttons, logo and wordmark); without it, a card.
/// </summary>
internal static class QuartzShortcut {
    private static readonly string[] Names = ["quartz-shortcut-settings", "quartz-shortcut-layout", "quartz-shortcut-close"];
    internal static Surface Surface { get; private set; }
    /// <summary>The surface exists and shows.</summary>
    internal static bool Visible => Surface?.Visible == true;
    /// <summary>It wears the shared hotkey-menu look.</summary>
    internal static bool Dressed { get; private set; }
    /// <summary>Its buttons, logo and wordmark are the shared hub ones too.</summary>
    internal static bool HubButtons { get; private set; }
    private static VisualElement card;

    /// <summary>Builds the window once, then shows it.</summary>
    internal static void Open() {
        Surface ??= Build();
        Surface.BlocksGameInput = MainCore.Conf.BlockInputsWhileMenuOpen;
        Surface.Visible = true;
        if(Dressed) PrismBridge.FadeHubIn(Surface);
    }

    /// <summary>Hides the window; true when it was showing.</summary>
    internal static bool Hide() {
        if(!Visible) return false;
        Surface.Visible = false;
        return true;
    }

    /// <summary>Before QuartzHost disposes its surfaces, which include this one.</summary>
    internal static void Shutdown() {
        Surface = null;
        card = null;
    }

    /// <summary>The classic fills follow the accent: every accent change dresses the hub buttons in the new ones.</summary>
    internal static void Recolour() {
        if(HubButtons && card != null) PrismBridge.RefillHubButtons(card, UIColors.ObjectButton, UIColors.ObjectBG);
    }

    private static Surface Build() {
        Surface s = QuartzHost.NewSurface("QuartzShortcut", Layer.Window);
        s.Root.style.alignItems = Align.Center;
        s.Root.style.justifyContent = Justify.Center;
        card = new VisualElement { name = "quartz-shortcut" };
        s.Root.Add(card);
        Sizes z = Theme.For(card).Sizes;
        card.style.width = 240f;
        Ui.SetPadding(card, z.Gap);
        Ui.SetRadius(card, z.Radius);
        Ui.SetBorderWidth(card, z.Line);
        Paint.Look(card, Theme.For(card).Looks.Card);

        var logo = new VisualElement { name = "quartz-shortcut-logo" };
        logo.style.height = 56f;
        logo.style.marginBottom = z.Gap;
        logo.style.backgroundImage = Background.FromSprite(MainCore.Spr.Get(UISprite.QuartzLogo));
        logo.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
        card.Add(logo);

        Action[] clicks = [Settings, EditLayout, Close];
        for(int i = 0; i < Names.Length; i++) {
            Button b = Ui.Btn(card, PrismRules.ShortcutKeys[i], clicks[i]);
            b.name = Names[i];
            b.style.marginRight = 0f;
            if(i > 0) b.style.marginTop = z.Gap / 2f;
        }
        // The classic hub's own fills: the accent's button shade, Close the neutral one.
        Dressed = PrismBridge.WearHubLook(s, card, logo, Info.DisplayName.ToUpperInvariant(), UIColors.ObjectButton, UIColors.ObjectBG, out bool buttons);
        HubButtons = buttons;
        if(buttons) {
            // The shared buttons set the width; the card only holds them.
            card.style.width = StyleKeyword.Auto;
            Ui.SetPadding(card, 0f);
        }
        return s;
    }

    // A fresh open of the new menu on its last page; closing it does not come back here. If it cannot open, the classic menu does.
    private static void Settings() {
        Hide();
        MenuHandOff.FromHub = false;
        QuartzShell.Open();
        if(QuartzShell.Surface?.Visible != true) UICore.Open();
    }

    private static void EditLayout() {
        Hide();
        LayoutHotkey.EditLayout();
    }

    private static void Close() {
        LayoutHotkey.Trip.Cancel();
        Hide();
    }
}
#endif

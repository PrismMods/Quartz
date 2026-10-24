using Quartz.Core;
using Quartz.Resource;
using UnityEngine;
using UnityEngine.UI;
namespace Quartz.UI.Utility;
// The Quartz logo as four stacked white masks tinted from the accent, so it follows the palette.
internal static class QuartzLogo {
    private static readonly List<Image[]> logos = [];
    public static void Create(RectTransform parent) {
        if(AprilFools.Active) {
            Layer(parent, UISprite.QuartzLogo).color = Color.white;
            return;
        }
        Image[] layers = [
            Layer(parent, UISprite.QuartzLogoFace),
            Layer(parent, UISprite.QuartzLogoShade),
            Layer(parent, UISprite.QuartzLogoTint),
            Layer(parent, UISprite.QuartzLogoShard)
        ];
        logos.Add(layers);
        Apply(layers);
    }
    public static void Refresh() {
        logos.RemoveAll(layers => layers[0] == null);
        foreach(Image[] layers in logos) Apply(layers);
    }
    private static void Apply(Image[] layers) {
        layers[0].color = UIColors.LogoFace;
        layers[1].color = UIColors.ObjectButton;
        layers[2].color = UIColors.ObjectActiveBright;
        layers[3].color = Color.white;
    }
    private static Image Layer(RectTransform parent, UISprite sprite) {
        GameObject obj = new(sprite.ToString());
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        Image image = obj.AddComponent<Image>();
        image.sprite = MainCore.Spr.Get(sprite);
        image.preserveAspect = true;
        image.raycastTarget = false;
        return image;
    }
}

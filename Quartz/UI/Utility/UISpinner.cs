using Quartz.Resource;
using UnityEngine;
using UnityEngine.UI;
namespace Quartz.UI.Utility;
public sealed class UISpinner : MonoBehaviour {
    private const float Speed = 320f;
    private static Sprite ring;
    private RectTransform rect;
    public static UISpinner Attach(Transform parent, float size, Color color) {
        GameObject obj = new("Spinner");
        obj.transform.SetParent(parent, false);
        RectTransform rt = obj.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(size, size);
        Image image = obj.AddComponent<Image>();
        if(ring == null) ring = SpriteManager.Create(ProceduralTexture.CircleOutline(48, 6));
        image.sprite = ring;
        image.color = color;
        image.raycastTarget = false;
        image.type = Image.Type.Filled;
        image.fillMethod = Image.FillMethod.Radial360;
        image.fillOrigin = (int)Image.Origin360.Top;
        image.fillAmount = 0.78f;
        return obj.AddComponent<UISpinner>();
    }
    private void Awake() => rect = (RectTransform)transform;
    private void Update() =>
        rect.localRotation = Quaternion.Euler(0f, 0f, -(Time.unscaledTime * Speed) % 360f);
}

using Quartz.Core;
using Quartz.Localization;
using Quartz.Resource;
using Quartz.UI.Utility;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using static UnityEngine.EventSystems.PointerEventData;
using TMPro;
namespace Quartz.UI.Generator;
public static partial class GenerateUI {
    public static TextMeshProUGUI KeyBind(
        Transform parent,
        Keybind.KeyModifier modifier,
        KeyCode key,
        System.Action<Keybind.KeyModifier, KeyCode> onChanged,
        string text,
        string id
    ) {
        RectTransform rect = BackGround();
        rect.SetParent(parent, false);
        rect.name = "KeyBind_" + id;
        TextMeshProUGUI label = AddText(rect);
        label.text = text;
        LocalizeById(label, id, text);
        GameObject box = new("KeybindBox");
        box.transform.SetParent(rect, false);
        RectTransform boxRect = box.AddComponent<RectTransform>();
        boxRect.anchorMin = new Vector2(1f, 0.5f);
        boxRect.anchorMax = new Vector2(1f, 0.5f);
        boxRect.pivot = new Vector2(1f, 0.5f);
        boxRect.anchoredPosition = new Vector2(-16f, 0f);
        boxRect.sizeDelta = new Vector2(220f, 36f);
        Image boxImg = box.AddComponent<Image>();
        boxImg.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P2048);
        boxImg.type = Image.Type.Sliced;
        boxImg.color = UIColors.ObjectButton;
        boxImg.raycastTarget = false;
        TextMeshProUGUI display = AddText(box.transform, true);
        display.alignment = TextAlignmentOptions.Center;
        display.raycastTarget = false;
        display.text = Keybind.Format(modifier, key);
        KeyCapture capture = rect.gameObject.AddComponent<KeyCapture>();
        capture.Display = display;
        capture.Modifier = modifier;
        capture.Key = key;
        capture.OnChanged = onChanged;
        AddButton(rect.gameObject, btn => {
            if(btn == InputButton.Left) capture.Begin();
        });
        return label;
    }
}
internal sealed class KeyCapture : MonoBehaviour {
    public TextMeshProUGUI Display;
    public Keybind.KeyModifier Modifier;
    public KeyCode Key;
    public System.Action<Keybind.KeyModifier, KeyCode> OnChanged;
    private bool listening;
    private static readonly KeyCode[] AllKeys = (KeyCode[])System.Enum.GetValues(typeof(KeyCode));
    public void Begin() {
        if(listening) return;
        listening = true;
        Keybind.Capturing = true;
        Display.text = MainCore.Tr.Get("PRESS_A_KEY", "Press a key...");
    }
    private void Refresh() => Display.text = Keybind.Format(Modifier, Key);
    private void Cancel() {
        listening = false;
        Keybind.Capturing = false;
        Refresh();
    }
    private void OnDisable() {
        if(listening) Cancel();
    }
    private void Update() {
        if(!listening) return;
        if(Input.GetKeyDown(KeyCode.Escape)) {
            Cancel();
            return;
        }
        for(int i = 0; i < AllKeys.Length; i++) {
            KeyCode kc = AllKeys[i];
            if(kc == KeyCode.None) continue;
            if((int)kc >= (int)KeyCode.Mouse0) continue;
            if(Keybind.IsModifier(kc)) continue;
            if(!Input.GetKeyDown(kc)) continue;
            Modifier = Keybind.HeldModifier();
            Key = kc;
            listening = false;
            Keybind.Capturing = false;
            Refresh();
            OnChanged?.Invoke(Modifier, Key);
            return;
        }
    }
}

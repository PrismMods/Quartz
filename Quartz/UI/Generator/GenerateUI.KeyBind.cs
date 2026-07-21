using Quartz.Core;
using Quartz.Localization;
using Quartz.Resource;
using Quartz.UI.Objects.Impl;
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
    internal static KeyCapture AttachToggleBind(RectTransform rect, UIToggle toggle, string id) {
        if(rect == null || toggle == null || string.IsNullOrEmpty(id)) return null;
        ToggleBinds.Register(id, toggle);
        bool bound = ToggleBinds.TryGet(id, out Keybind.KeyModifier modifier, out KeyCode key);
        GameObject box = new("BindLabel");
        box.transform.SetParent(rect, false);
        RectTransform boxRect = box.AddComponent<RectTransform>();
        boxRect.anchorMin = new Vector2(1f, 0.5f);
        boxRect.anchorMax = new Vector2(1f, 0.5f);
        boxRect.pivot = new Vector2(1f, 0.5f);
        boxRect.anchoredPosition = new Vector2(-46f, 0f);
        boxRect.sizeDelta = new Vector2(240f, 30f);
        TextMeshProUGUI display = AddMutedText(box.transform, 16f, 0.5f, true);
        display.alignment = TextAlignmentOptions.Right;
        display.raycastTarget = false;
        display.text = bound ? Keybind.Format(modifier, key) : "";
        KeyCapture capture = rect.gameObject.AddComponent<KeyCapture>();
        capture.Display = display;
        capture.Modifier = modifier;
        capture.Key = key;
        capture.Bound = bound;
        capture.AllowClear = true;
        capture.OnChanged = (mod, k) => ToggleBinds.Set(id, mod, k);
        capture.OnCleared = () => ToggleBinds.Clear(id);
        return capture;
    }
}
internal sealed class KeyCapture : MonoBehaviour {
    public TextMeshProUGUI Display;
    public Keybind.KeyModifier Modifier;
    public KeyCode Key;
    public System.Action<Keybind.KeyModifier, KeyCode> OnChanged;
    public bool Bound = true;
    public bool AllowClear;
    public System.Action OnCleared;
    private bool listening;
    private static readonly KeyCode[] AllKeys = (KeyCode[])System.Enum.GetValues(typeof(KeyCode));
    private void Awake() => enabled = false;
    public void Begin() {
        if(listening) return;
        listening = true;
        enabled = true;
        Keybind.Capturing = true;
        Display.text = MainCore.Tr.Get("PRESS_A_KEY", "Press a key...");
    }
    private void Refresh() => Display.text = Bound ? Keybind.Format(Modifier, Key) : "";
    private void Stop() {
        listening = false;
        enabled = false;
        Keybind.Capturing = false;
    }
    private void Cancel() {
        Stop();
        Refresh();
    }
    private void OnDisable() {
        if(listening) Cancel();
    }
    private void Update() {
        if(!listening) return;
        if(Input.GetKeyDown(KeyCode.Escape)
            || (AllowClear && (Input.GetKeyDown(KeyCode.Backspace) || Input.GetKeyDown(KeyCode.Delete)))) {
            if(!AllowClear) {
                Cancel();
                return;
            }
            Bound = false;
            Key = KeyCode.None;
            Modifier = Keybind.KeyModifier.None;
            Stop();
            Refresh();
            OnCleared?.Invoke();
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
            Bound = true;
            Stop();
            Refresh();
            OnChanged?.Invoke(Modifier, Key);
            return;
        }
    }
}

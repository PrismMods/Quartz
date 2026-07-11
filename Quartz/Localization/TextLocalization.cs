using Quartz.Core;
using UnityEngine;
using TMPro;
namespace Quartz.Localization;
public class TextLocalization : MonoBehaviour {
    public string Key;
    public string Default;
    public string Value => tr?.Get(Key, Default) ?? Default;
    private Translator tr;
    private TMP_Text tmp;
    private static readonly HashSet<TextLocalization> instances = [];
    public TextLocalization Init(string key, string defaultValue, Translator translator = null) {
        foreach(TextLocalization other in GetComponents<TextLocalization>()) {
            if(other == this) continue;
            instances.Remove(other);
            Destroy(other);
        }
        tr = translator ?? MainCore.Tr;
        Key = key;
        Default = defaultValue;
        UpdateText();
        return this;
    }
    // Registered for the component's whole lifetime, not just while enabled: hidden
    // settings pages are fully deactivated now, and their labels must still pick up
    // language changes (RefreshAll) so e.g. the search index reads current text.
    // Setting .text on a disabled TMP is cheap — the mesh regenerates on enable.
    void Awake() {
        tmp = GetComponent<TMP_Text>();
        instances.Add(this);
    }
    void OnEnable() {
        instances.Add(this);
        UpdateText();
    }
    void OnDestroy() => instances.Remove(this);
    public void UpdateText() {
        if(tmp == null) tmp = GetComponent<TMP_Text>();
        if(tmp == null || tr == null || string.IsNullOrEmpty(Key)) return;
        tmp.text = Value;
    }
    public static void RefreshAll() {
        foreach(TextLocalization t in instances) t?.UpdateText();
    }
}

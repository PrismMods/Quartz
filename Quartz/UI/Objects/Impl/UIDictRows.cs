using Quartz.UI.Generator;
using UnityEngine;
using UnityEngine.UI;
namespace Quartz.UI.Objects.Impl;
public sealed class UIDictRows : UIObject {
    public List<KeyValuePair<string, string>> Pairs { get; } = [];
    public Action<List<KeyValuePair<string, string>>> OnChanged;
    public string KeyPlaceholder { get; set; }
    public string ValuePlaceholder { get; set; }
    private readonly RectTransform rowsContainer;
    private readonly List<UIObject> rowObjects = [];
    internal UIDictRows(
        string id,
        RectTransform rect,
        RectTransform rowsContainer,
        IEnumerable<KeyValuePair<string, string>> value,
        Action<List<KeyValuePair<string, string>>> onChanged,
        string keyPlaceholder,
        string valuePlaceholder
    ) : base(id, rect) {
        this.rowsContainer = rowsContainer;
        OnChanged = onChanged;
        KeyPlaceholder = keyPlaceholder;
        ValuePlaceholder = valuePlaceholder;
        if(value != null) Pairs.AddRange(value);
        Rebuild();
    }
    public void Set(IEnumerable<KeyValuePair<string, string>> pairs, bool invoke = false) {
        if(IsDisposed) return;
        Pairs.Clear();
        if(pairs != null) Pairs.AddRange(pairs);
        Rebuild();
        if(invoke) Notify();
    }
    public void AddRow() {
        if(IsDisposed) return;
        Pairs.Add(new("", ""));
        Rebuild();
        Notify();
    }
    public void RemoveRow(int index) {
        if(IsDisposed || index < 0 || index >= Pairs.Count) return;
        Pairs.RemoveAt(index);
        Rebuild();
        Notify();
    }
    private void Notify() => OnChanged?.Invoke(Pairs);
    private void Rebuild() {
        if(rowsContainer == null) return;
        foreach(UIObject o in rowObjects) o.Dispose();
        rowObjects.Clear();
        GenerateUI.ClearChildren(rowsContainer);
        for(int i = 0; i < Pairs.Count; i++) BuildRow(i);
    }
    private void BuildRow(int index) {
        RectTransform row = GenerateUI.Row(rowsContainer);
        GenerateUI.ButtonRow(row);
        UIInput key = CellInput(
            row,
            Pairs[index].Key,
            KeyPlaceholder,
            Id == null ? null : Id + "_key",
            v => Pairs[index] = new(v, Pairs[index].Value)
        );
        UIInput value = CellInput(
            row,
            Pairs[index].Value,
            ValuePlaceholder,
            Id == null ? null : Id + "_value",
            v => Pairs[index] = new(Pairs[index].Key, v)
        );
        UIButton remove = GenerateUI.Button(row, () => RemoveRow(index), "×", null).SetSecondary();
        GenerateUI.FixWidth(remove, 64f);
        rowObjects.Add(key);
        rowObjects.Add(value);
        rowObjects.Add(remove);
    }
    private UIInput CellInput(Transform row, string value, string placeholder, string id, Action<string> apply) {
        UIInput input = GenerateUI.Input(row, null, value, apply, placeholder, null, id, 0f);
        input.OnComplete = _ => Notify();
        LayoutElement le = input.Rect.gameObject.AddComponent<LayoutElement>();
        le.flexibleWidth = 1f;
        return input;
    }
    public override void Dispose() {
        if(IsDisposed) return;
        foreach(UIObject o in rowObjects) o.Dispose();
        rowObjects.Clear();
        OnChanged = null;
        base.Dispose();
    }
}

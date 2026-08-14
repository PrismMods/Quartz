using Quartz.UI.Objects.Impl;
using UnityEngine;
namespace Quartz.UI.Generator;
public static partial class GenerateUI {
    public static UIDictRows DictRows(
        Transform parent,
        IEnumerable<KeyValuePair<string, string>> value,
        Action<List<KeyValuePair<string, string>>> onChanged,
        string id,
        string keyPlaceholder = "Key",
        string valuePlaceholder = "Value",
        string addText = "Add Row"
    ) {
        GameObject obj = new("DictRows");
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.AddComponent<RectTransform>();
        GameObject rows = new("Rows");
        rows.transform.SetParent(obj.transform, false);
        RectTransform rowsRect = rows.AddComponent<RectTransform>();
        FitVertical(obj, 8f);
        FitVertical(rows, 8f);
        UIDictRows dict = new(id, rect, rowsRect, value, onChanged, keyPlaceholder, valuePlaceholder);
        RectTransform addRow = Row(obj.transform);
        ButtonRow(addRow);
        UIButton addBtn = Button(addRow, dict.AddRow, addText, id == null ? null : id + "_add").SetSecondary();
        FixWidth(addBtn, 160f);
        dict.OnDisposed += addBtn.Dispose;
        return dict;
    }
}

using Quartz.UI.Generator;
using Quartz.UI.Objects.Impl;
using UnityEngine;
namespace Quartz.UI.Editor;
internal static partial class KvWidgets {
    internal static UIColorPicker ColorPicker(
        Transform parent,
        Color defaultValue,
        Color value,
        Action<Color> onChanged,
        Action<Color> onComplete,
        string text,
        string id,
        bool showAlpha
    ) => GenerateUI.ColorPicker(
        parent, defaultValue, value, onChanged, onComplete, text, id, showAlpha, 0f
    );
}

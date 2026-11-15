using System.Globalization;
using System.Text;
using Quartz.Core;
namespace Quartz.Addons;
/// <summary>The row an addon settings member gets, as AddonSettingsUI picks it; BCL only, so the offline tests pin it.</summary>
internal enum AddonFieldKind { Toggle, Enum, Choices, Slider, IntSlider, Number, NullableNumber, Text, Colour, Dict, Pairs, Unsupported }

internal static class AddonFieldRules {
    /// <summary>The member's row kind. slider: it has a [Slider]; choices: a [Choices] with at least one value. Only int, float and
    /// double are numbers; a nullable number is always typed; UnityEngine.Color is matched by its full name.</summary>
    internal static AddonFieldKind Kind(Type type, bool slider, bool choices) {
        if(type == typeof(bool)) return AddonFieldKind.Toggle;
        if(type.IsEnum) return AddonFieldKind.Enum;
        if(type == typeof(string) && choices) return AddonFieldKind.Choices;
        if(IsNumber(type) && slider) return type == typeof(int) ? AddonFieldKind.IntSlider : AddonFieldKind.Slider;
        if(IsNumber(type)) return AddonFieldKind.Number;
        if(Nullable.GetUnderlyingType(type) is { } of && IsNumber(of)) return AddonFieldKind.NullableNumber;
        if(type == typeof(string)) return AddonFieldKind.Text;
        if(type.FullName == "UnityEngine.Color") return AddonFieldKind.Colour;
        if(type == typeof(Dictionary<string, string>)) return AddonFieldKind.Dict;
        if(type == typeof(List<KeyValuePair<string, string>>)) return AddonFieldKind.Pairs;
        return AddonFieldKind.Unsupported;
    }

    /// <summary>[Name]'s label, else the member's name split as AddonSettingsUI's SplitPascal splits it.</summary>
    internal static string Label(string member, string nameAttribute) {
        if(nameAttribute != null) return nameAttribute;
        StringBuilder sb = new(member.Length + 8);
        for(int i = 0; i < member.Length; i++) {
            char c = member[i];
            if(c == '_') {
                sb.Append(' ');
                continue;
            }
            if(i > 0 && char.IsUpper(c) && (char.IsLower(member[i - 1]) || char.IsDigit(member[i - 1])))
                sb.Append(' ');
            sb.Append(c);
        }
        return sb.ToString();
    }

    /// <summary>Typed text as a number of type, as the classic input reads it: blank on a nullable is null; else an invariant
    /// double converted to the type. False when the text is no number or does not fit the type.</summary>
    internal static bool ParseNumber(string text, Type type, out object value) {
        value = null;
        Type of = Nullable.GetUnderlyingType(type);
        if(of != null && string.IsNullOrWhiteSpace(text)) return true;
        if(!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed)) return false;
        try {
            value = Convert.ChangeType(parsed, of ?? type, CultureInfo.InvariantCulture);
            return true;
        } catch(Exception e) when(e is OverflowException or InvalidCastException) {
            Diag.Ignore(e);
            return false;
        }
    }

    private static bool IsNumber(Type type) => type == typeof(int) || type == typeof(float) || type == typeof(double);
}

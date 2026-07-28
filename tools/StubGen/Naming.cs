using Mono.Cecil;
using System.Text;

namespace Quartz.StubGen;

/// <summary>Renders Cecil type references as C# the compiler will accept.</summary>
static class Naming {
    static readonly Dictionary<string, string> Primitives = new(StringComparer.Ordinal) {
        ["System.Void"] = "void",
        ["System.Boolean"] = "bool",
        ["System.Byte"] = "byte",
        ["System.SByte"] = "sbyte",
        ["System.Char"] = "char",
        ["System.Int16"] = "short",
        ["System.UInt16"] = "ushort",
        ["System.Int32"] = "int",
        ["System.UInt32"] = "uint",
        ["System.Int64"] = "long",
        ["System.UInt64"] = "ulong",
        ["System.Single"] = "float",
        ["System.Double"] = "double",
        ["System.Decimal"] = "decimal",
        ["System.String"] = "string",
        ["System.Object"] = "object",
        ["System.IntPtr"] = "global::System.IntPtr",
        ["System.UIntPtr"] = "global::System.UIntPtr",
    };

    static readonly HashSet<string> Keywords = new(StringComparer.Ordinal) {
        "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked",
        "class", "const", "continue", "decimal", "default", "delegate", "do", "double", "else",
        "enum", "event", "explicit", "extern", "false", "finally", "fixed", "float", "for",
        "foreach", "goto", "if", "implicit", "in", "int", "interface", "internal", "is", "lock",
        "long", "namespace", "new", "null", "object", "operator", "out", "override", "params",
        "private", "protected", "public", "readonly", "ref", "return", "sbyte", "sealed", "short",
        "sizeof", "stackalloc", "static", "string", "struct", "switch", "this", "throw", "true",
        "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort", "using", "virtual",
        "void", "volatile", "while",
    };

    public static string Escape(string identifier) =>
        Keywords.Contains(identifier) ? "@" + identifier : identifier;

    /// <summary>The bare, arity-stripped name a type is declared under.</summary>
    public static string DeclName(TypeDefinition def) {
        string name = def.Name;
        int tick = name.LastIndexOf('`');
        if(tick >= 0) name = name[..tick];
        return Escape(name);
    }

    public static string DeclNameWithParams(TypeDefinition def) {
        string name = DeclName(def);
        // Only the parameters this type introduces; a nested type's list also carries
        // its enclosing type's, and re-declaring those is a compile error.
        int inherited = def.DeclaringType?.GenericParameters.Count ?? 0;
        if(def.GenericParameters.Count <= inherited) return name;
        IEnumerable<string> own = def.GenericParameters.Skip(inherited).Select(g => Escape(g.Name));
        return $"{name}<{string.Join(", ", own)}>";
    }

    /// <summary>A by-ref return has to keep its `ref`, or a ref-returning delegate stops
    /// being assignable at the call site (`fieldRef(obj) = value`).</summary>
    public static string Return(TypeReference tr) =>
        tr is ByReferenceType byref ? "ref " + Ref(byref.ElementType) : Ref(tr);

    public static string Ref(TypeReference tr) {
        switch(tr) {
            case null:
                return "object";
            case GenericParameter g:
                return Escape(g.Name);
            case ArrayType array:
                return Ref(array.ElementType) + "[" + new string(',', array.Rank - 1) + "]";
            case PointerType pointer:
                return Ref(pointer.ElementType) + "*";
            case ByReferenceType byref:
                return Ref(byref.ElementType);
            case RequiredModifierType req:
                return Ref(req.ElementType);
            case OptionalModifierType opt:
                return Ref(opt.ElementType);
            case PinnedType pinned:
                return Ref(pinned.ElementType);
            case GenericInstanceType instance:
                return Instance(instance);
            case FunctionPointerType:
                // A stub never calls through one; the size and calling convention are all
                // that a signature carrying it needs to keep.
                return "global::System.IntPtr";
        }
        if(Primitives.TryGetValue(tr.FullName, out string primitive)) return primitive;
        return "global::" + Qualified(tr, Array.Empty<TypeReference>(), 0, out _);
    }

    static string Instance(GenericInstanceType instance) {
        TypeReference element = instance.ElementType;
        if(Primitives.TryGetValue(element.FullName, out string primitive)) return primitive;
        IList<TypeReference> args = instance.GenericArguments;
        return "global::" + Qualified(element, args, 0, out _);
    }

    /// <summary>
    /// Walks outward through nested types so generic arguments land on the type that
    /// actually declares them: `Outer&lt;T&gt;.Inner&lt;U&gt;`, never `Outer.Inner&lt;T, U&gt;`.
    /// </summary>
    static string Qualified(TypeReference tr, IList<TypeReference> args, int consumed, out int used) {
        string simple = tr.Name;
        int tick = simple.LastIndexOf('`');
        int arity = 0;
        if(tick >= 0) {
            arity = int.Parse(simple[(tick + 1)..]);
            simple = simple[..tick];
        }
        simple = Escape(simple);

        string prefix;
        int consumedByOuter = 0;
        if(tr.DeclaringType != null) {
            prefix = Qualified(tr.DeclaringType, args, consumed, out consumedByOuter) + ".";
        } else {
            prefix = string.IsNullOrEmpty(tr.Namespace)
                ? ""
                : string.Join(".", tr.Namespace.Split('.').Select(Escape)) + ".";
        }

        int own = arity - consumedByOuter;
        used = consumedByOuter;
        if(own <= 0 || args.Count == 0) return prefix + simple;

        StringBuilder sb = new();
        sb.Append(prefix).Append(simple).Append('<');
        for(int i = 0; i < own; i++) {
            if(i > 0) sb.Append(", ");
            int index = consumedByOuter + i;
            sb.Append(index < args.Count ? Ref(args[index]) : "object");
        }
        sb.Append('>');
        used = consumedByOuter + own;
        return sb.ToString();
    }

    /// <summary>C# operator syntax for the IL name, or null when it is an ordinary method.</summary>
    public static string Operator(string ilName) => ilName switch {
        "op_Implicit" => "implicit",
        "op_Explicit" => "explicit",
        "op_Addition" => "+",
        "op_Subtraction" => "-",
        "op_Multiply" => "*",
        "op_Division" => "/",
        "op_Modulus" => "%",
        "op_BitwiseAnd" => "&",
        "op_BitwiseOr" => "|",
        "op_ExclusiveOr" => "^",
        "op_LeftShift" => "<<",
        "op_RightShift" => ">>",
        "op_UnaryNegation" => "-",
        "op_UnaryPlus" => "+",
        "op_LogicalNot" => "!",
        "op_OnesComplement" => "~",
        "op_Increment" => "++",
        "op_Decrement" => "--",
        "op_True" => "true",
        "op_False" => "false",
        "op_Equality" => "==",
        "op_Inequality" => "!=",
        "op_LessThan" => "<",
        "op_GreaterThan" => ">",
        "op_LessThanOrEqual" => "<=",
        "op_GreaterThanOrEqual" => ">=",
        _ => null,
    };

    /// <summary>
    /// A stand-in for a constant's value, NOT the value itself.
    ///
    /// A compile gate needs the constant to exist with the right type; it never needs
    /// the real number. Emitting the real one would publish game data rather than game
    /// API — the hit windows, judgement thresholds and speedrun-validation limits are
    /// all `const` fields, and those are design values, not an interface.
    ///
    /// Placeholders stay distinct per declaring type so constants used as `switch`
    /// labels do not collide into a duplicate-case error.
    /// </summary>
    public static string Placeholder(FieldDefinition field, int ordinal) {
        TypeReference declared = field.FieldType;
        TypeDefinition def = Safe(declared);
        if(def != null && def.IsEnum) return "(" + Ref(declared) + ")(" + ordinal + ")";
        return declared.FullName switch {
            "System.String" => "\"" + field.Name + "\"",
            "System.Boolean" => "false",
            "System.Char" => "'\\u" + (0x41 + ordinal % 26).ToString("x4") + "'",
            "System.Single" => ordinal + "f",
            "System.Double" => ordinal + "d",
            "System.Decimal" => ordinal + "m",
            _ => ordinal.ToString(),
        };
    }

    public static string Literal(object value, TypeReference declared) {
        if(value == null) return "default";
        switch(value) {
            case bool b: return b ? "true" : "false";
            case string s: return "\"" + s
                .Replace("\\", "\\\\").Replace("\"", "\\\"")
                .Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t") + "\"";
            case char c: return "'\\u" + ((int)c).ToString("x4") + "'";
            case float f:
                if(float.IsNaN(f)) return "float.NaN";
                if(float.IsPositiveInfinity(f)) return "float.PositiveInfinity";
                if(float.IsNegativeInfinity(f)) return "float.NegativeInfinity";
                return f.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "f";
            case double d:
                if(double.IsNaN(d)) return "double.NaN";
                if(double.IsPositiveInfinity(d)) return "double.PositiveInfinity";
                if(double.IsNegativeInfinity(d)) return "double.NegativeInfinity";
                return d.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "d";
            case decimal m: return m.ToString(System.Globalization.CultureInfo.InvariantCulture) + "m";
        }
        string raw = Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);
        TypeDefinition def = Safe(declared);
        // An enum-typed default must be cast back; the metadata only keeps the number.
        if(def != null && def.IsEnum) return "(" + Ref(declared) + ")(" + raw + ")";
        return raw;
    }

    static TypeDefinition Safe(TypeReference tr) {
        try { return tr?.Resolve(); } catch { return null; }
    }
}

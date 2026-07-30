using Mono.Cecil;
using System.Text;

namespace Quartz.StubGen;

/// <summary>
/// Writes one C# file per stub assembly. Bodies are `throw null;` — the standard
/// reference-assembly shape. Nothing here carries game logic, only the declarations
/// our own code names.
///
/// Two deliberate departures from mirroring the real metadata, both because a compile
/// gate needs the declarations to *exist* rather than to enforce anything:
/// members that are abstract in the game are emitted `virtual` (so a stub subclass
/// owes nothing), and BCL interfaces are declared only when our code names them.
/// </summary>
sealed class Emitter {
    readonly StringBuilder sb = new();
    readonly IReadOnlySet<string> named;
    readonly AssemblyReq asm;
    TypeDefinition enclosing;
    int constOrdinal;
    int depth;

    Emitter(AssemblyReq asm, IReadOnlySet<string> named) {
        this.asm = asm;
        this.named = named;
    }

    public static string Render(AssemblyReq asm, IReadOnlySet<string> named, string header) {
        Emitter e = new(asm, named);
        e.sb.Append(header);
        e.Line("#pragma warning disable CS0067, CS0169, CS0649, CS0108, CS0109, CS0114, CS0465, CS0824");
        e.Line();

        IEnumerable<IGrouping<string, TypeReq>> byNamespace = asm.Types.Values
            .Where(t => t.Def.DeclaringType == null)
            .GroupBy(t => t.Def.Namespace ?? "")
            .OrderBy(g => g.Key, StringComparer.Ordinal);

        foreach(IGrouping<string, TypeReq> group in byNamespace) {
            bool global = string.IsNullOrEmpty(group.Key);
            if(!global) {
                e.Line($"namespace {string.Join(".", group.Key.Split('.').Select(Naming.Escape))} {{");
                e.depth++;
            }
            foreach(TypeReq req in group.OrderBy(t => t.Def.FullName, StringComparer.Ordinal))
                e.Type(req);
            if(!global) {
                e.depth--;
                e.Line("}");
            }
        }
        return e.sb.ToString();
    }

    void Type(TypeReq req) {
        TypeDefinition def = req.Def;
        if(def.IsEnum) { Enum(def); return; }
        if(IsDelegate(def)) { Delegate(def); return; }

        TypeDefinition outer = enclosing;
        enclosing = def;
        try { Body(req, def); } finally { enclosing = outer; }
    }

    void Body(TypeReq req, TypeDefinition def) {
        bool isStatic = def.IsAbstract && def.IsSealed && !def.IsInterface && !def.IsValueType;
        string kind = def.IsInterface ? "interface" : def.IsValueType ? "struct" : "class";
        StringBuilder head = new("public ");
        if(!def.IsInterface && !def.IsValueType) {
            if(isStatic) head.Append("static ");
            else {
                if(def.IsAbstract) head.Append("abstract ");
                if(def.IsSealed) head.Append("sealed ");
            }
        }
        if(def.IsValueType && IsReadOnly(def)) head.Append("readonly ");
        if(def.IsValueType && IsByRefLike(def)) head.Append("ref ");
        head.Append("unsafe partial ").Append(kind).Append(' ').Append(Naming.DeclNameWithParams(def));

        List<string> bases = new();
        if(!isStatic && !def.IsValueType && !def.IsInterface && def.BaseType != null
            && def.BaseType.FullName != "System.Object" && Declarable(def.BaseType))
            bases.Add(Naming.Ref(def.BaseType));

        List<TypeReference> interfaces = new();
        if(def.IsInterface) {
            // An interface's base list is part of its identity rather than an obligation,
            // so it is always complete — filtering it would change what implementers owe.
            foreach(InterfaceImplementation i in def.Interfaces)
                if(Declarable(i.InterfaceType)) bases.Add(Naming.Ref(i.InterfaceType));
        } else if(!isStatic) {
            // An interface an emitted base class already declares is inherited, so
            // re-declaring it here would only oblige this type to implement the same
            // closure a second time.
            HashSet<string> inherited = InheritedInterfaces(def);
            // The metadata lists inherited interfaces alongside the ones a type really
            // introduces, and C# rejects the same interface twice in one list.
            foreach(TypeReference i in req.ExplicitInterfaces) {
                if(inherited.Contains(i.FullName)) continue;
                if(ImpliedByAnother(i, req.ExplicitInterfaces)) continue;
                interfaces.Add(i);
                bases.Add(Naming.Ref(i));
            }
        }
        if(bases.Count > 0) head.Append(" : ").Append(string.Join(", ", bases));

        Line(head + Constraints(def.GenericParameters, def.DeclaringType?.GenericParameters.Count ?? 0) + " {");
        depth++;

        foreach(FieldDefinition f in req.Fields.OrderBy(f => f.Name, StringComparer.Ordinal)) Field(f);
        foreach(PropertyDefinition p in req.Properties.OrderBy(p => p.Name, StringComparer.Ordinal)) Property(p, def);
        foreach(EventDefinition ev in req.Events.OrderBy(x => x.Name, StringComparer.Ordinal)) Event(ev, def);

        HashSet<string> emitted = new(StringComparer.Ordinal);
        bool hasParameterless = false;
        bool hasEqualityOperator = false;
        bool hasObjectEquals = false;
        bool hasGetHashCode = false;
        foreach(MethodDefinition m in req.Methods
            .OrderBy(m => m.Name, StringComparer.Ordinal)
            .ThenBy(m => m.Parameters.Count)) {
            if(IsAccessor(m)) continue;
            if(m.IsConstructor && !m.IsStatic && m.Parameters.Count == 0) hasParameterless = true;
            if(m.IsStatic && m.Name is "op_Equality" or "op_Inequality") hasEqualityOperator = true;
            if(m.Name == "Equals" && m.Parameters.Count == 1
                && m.Parameters[0].ParameterType.FullName == "System.Object") hasObjectEquals = true;
            if(m.Name == "GetHashCode" && m.Parameters.Count == 0) hasGetHashCode = true;
            if(!emitted.Add(Signature(m))) continue;
            Method(m, def);
        }

        // C# warns (CS0660/CS0661) on any type that declares == or != without also
        // overriding Equals/GetHashCode. We only emit the members the mod names, and
        // the mod never names Equals on a Vector3 — so the stub declared the operators
        // alone and every COLD build carried 10 warnings. They were invisible locally
        // because an incremental build skips CoreCompile, and CI builds cold.
        // The real Unity types do override both, so emitting them is also more faithful.
        if(hasEqualityOperator && !def.IsInterface) {
            if(!hasObjectEquals) Line("public override bool Equals(object obj) => throw null;");
            if(!hasGetHashCode) Line("public override int GetHashCode() => throw null;");
        }

        // Without an accessible parameterless constructor a derived stub cannot chain,
        // and any stub class may end up derived from. Kept non-public so it never widens
        // what our own code is allowed to construct.
        if(!isStatic && !def.IsValueType && !def.IsInterface && !hasParameterless)
            Line($"{(def.IsSealed ? "private" : "protected")} {Naming.DeclName(def)}() {{ }}");

        if(!def.IsInterface && interfaces.Count > 0) ExplicitImplementations(interfaces);

        foreach(TypeDefinition nested in def.NestedTypes) {
            if(!asm.Types.TryGetValue(nested.FullName, out TypeReq nestedReq)) continue;
            Type(nestedReq);
        }

        depth--;
        Line("}");
    }

    /// <summary>
    /// Satisfies a declared BCL interface without widening the type's own surface.
    /// The stub only emits the members our code references, so an interface it declares
    /// would otherwise be left unimplemented; explicit implementations close that gap
    /// and stay invisible unless the call site actually casts to the interface.
    /// </summary>
    void ExplicitImplementations(List<TypeReference> declaredInterfaces) {
        // Interface closures overlap (IList<T> and ICollection<T> both drag in
        // IEnumerable<T>), and a member may only be explicitly implemented once, so the
        // whole type's set is deduped by interface + signature before anything is written.
        Dictionary<string, string> lines = new(StringComparer.Ordinal);
        foreach(TypeReference root in declaredInterfaces) {
            if(Safe(root) == null) continue;
            foreach((TypeReference declared, TypeDefinition resolved) in InterfaceClosure(root)) {
                string qualified = Naming.Ref(declared);
                Dictionary<string, TypeReference> map = Substitution(declared, resolved);

                foreach(PropertyDefinition p in resolved.Properties) {
                    string type = Naming.Ref(Substitute(p.PropertyType, map));
                    string name = p.HasParameters
                        ? $"this[{string.Join(", ", p.Parameters.Select(x => Naming.Ref(Substitute(x.ParameterType, map)) + " " + Naming.Escape(x.Name ?? "index")))}]"
                        : Naming.Escape(p.Name);
                    StringBuilder body = new(" { ");
                    if(p.GetMethod != null) body.Append("get => throw null; ");
                    if(p.SetMethod != null) body.Append("set { } ");
                    body.Append('}');
                    lines[$"P|{declared.FullName}|{name}"] = $"{type} {qualified}.{name}{body}";
                }
                foreach(EventDefinition e in resolved.Events)
                    lines[$"E|{declared.FullName}|{e.Name}"] =
                        $"event {Naming.Ref(Substitute(e.EventType, map))} {qualified}.{Naming.Escape(e.Name)} {{ add {{ }} remove {{ }} }}";
                foreach(MethodDefinition m in resolved.Methods) {
                    if(m.IsStatic || IsAccessor(m) || m.IsConstructor) continue;
                    string generics = m.HasGenericParameters
                        ? "<" + string.Join(", ", m.GenericParameters.Select(g => Naming.Escape(g.Name))) + ">"
                        : "";
                    string parameters = string.Join(", ", m.Parameters.Select(p => {
                        StringBuilder one = new();
                        if(p.ParameterType is ByReferenceType) one.Append(p.IsOut ? "out " : p.IsIn ? "in " : "ref ");
                        one.Append(Naming.Ref(Substitute(p.ParameterType, map))).Append(' ').Append(Naming.Escape(p.Name ?? "arg"));
                        return one.ToString();
                    }));
                    lines[$"M|{declared.FullName}|{Signature(m)}"] =
                        $"{Naming.Ref(Substitute(m.ReturnType, map))} {qualified}.{Naming.Escape(m.Name)}{generics}({parameters})"
                        + Constraints(m.GenericParameters, 0) + " => throw null;";
                }
            }
        }
        foreach(string line in lines.OrderBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => kv.Value)) Line(line);
    }

    /// <summary>
    /// `override` is only legal when an ancestor the stub also emits declares the same
    /// member; the game's own inheritance chain often runs through types no call site of
    /// ours touches, so the base declaration simply is not there.
    /// </summary>
    string Inheritance(MethodDefinition m) {
        // A sealed type has nothing to override it, and C# rejects a new virtual member there.
        if(enclosing != null && enclosing.IsSealed) return "";
        if(m.IsNewSlot) return "virtual ";
        string wanted = Signature(m);
        for(TypeDefinition t = Safe(enclosing?.BaseType); t != null; t = Safe(t.BaseType)) {
            if(!asm.Types.TryGetValue(t.FullName, out TypeReq baseReq)) break;
            foreach(MethodDefinition candidate in baseReq.Methods)
                if(Signature(candidate) == wanted) return "override ";
            foreach(PropertyDefinition p in baseReq.Properties)
                if(p.GetMethod != null && Signature(p.GetMethod) == wanted
                    || p.SetMethod != null && Signature(p.SetMethod) == wanted) return "override ";
            foreach(EventDefinition e in baseReq.Events)
                if(e.AddMethod != null && Signature(e.AddMethod) == wanted
                    || e.RemoveMethod != null && Signature(e.RemoveMethod) == wanted) return "override ";
        }
        return "virtual ";
    }

    static bool ImpliedByAnother(TypeReference candidate, List<TypeReference> all) {
        foreach(TypeReference other in all) {
            if(other.FullName == candidate.FullName) continue;
            foreach((TypeReference declared, _) in InterfaceClosure(other))
                if(declared.FullName == candidate.FullName) return true;
        }
        return false;
    }

    /// <summary>Interfaces this type gets for free from an emitted base class.</summary>
    HashSet<string> InheritedInterfaces(TypeDefinition def) {
        HashSet<string> result = new(StringComparer.Ordinal);
        for(TypeDefinition current = Safe(def.BaseType); current != null; current = Safe(current.BaseType)) {
            if(!asm.Types.ContainsKey(current.FullName)) break;
            foreach(InterfaceImplementation i in current.Interfaces)
                foreach((TypeReference declared, _) in InterfaceClosure(i.InterfaceType))
                    result.Add(declared.FullName);
        }
        return result;
    }

    /// <summary>An interface plus everything it inherits — all of it must be implemented.</summary>
    static IEnumerable<(TypeReference Declared, TypeDefinition Resolved)> InterfaceClosure(TypeReference root) {
        HashSet<string> seen = new(StringComparer.Ordinal);
        Queue<TypeReference> queue = new();
        queue.Enqueue(root);
        while(queue.Count > 0) {
            TypeReference current = queue.Dequeue();
            TypeDefinition def = Safe(current);
            if(def == null || !seen.Add(current.FullName)) continue;
            yield return (current, def);
            Dictionary<string, TypeReference> map = Substitution(current, def);
            foreach(InterfaceImplementation i in def.Interfaces) queue.Enqueue(Substitute(i.InterfaceType, map));
        }
    }

    static Dictionary<string, TypeReference> Substitution(TypeReference declared, TypeDefinition resolved) {
        Dictionary<string, TypeReference> map = new(StringComparer.Ordinal);
        if(declared is not GenericInstanceType instance) return map;
        for(int i = 0; i < resolved.GenericParameters.Count && i < instance.GenericArguments.Count; i++)
            map[resolved.GenericParameters[i].Name] = instance.GenericArguments[i];
        return map;
    }

    static TypeReference Substitute(TypeReference tr, Dictionary<string, TypeReference> map) {
        if(map.Count == 0 || tr == null) return tr;
        switch(tr) {
            case GenericParameter g:
                return map.TryGetValue(g.Name, out TypeReference replacement) ? replacement : tr;
            case ArrayType array: {
                ArrayType copy = new(Substitute(array.ElementType, map), array.Rank);
                return copy;
            }
            case ByReferenceType byref:
                return new ByReferenceType(Substitute(byref.ElementType, map));
            case PointerType pointer:
                return new PointerType(Substitute(pointer.ElementType, map));
            case GenericInstanceType instance: {
                GenericInstanceType copy = new(instance.ElementType);
                foreach(TypeReference arg in instance.GenericArguments) copy.GenericArguments.Add(Substitute(arg, map));
                return copy;
            }
            default:
                return tr;
        }
    }

    void Enum(TypeDefinition def) {
        FieldDefinition backing = def.Fields.FirstOrDefault(f => f.Name == "value__");
        string underlying = backing != null ? Naming.Ref(backing.FieldType) : "int";
        Line($"public enum {Naming.DeclName(def)} : {underlying} {{");
        depth++;
        foreach(FieldDefinition f in def.Fields) {
            if(!f.IsStatic || !f.HasConstant) continue;
            Line($"{Naming.Escape(f.Name)} = {Convert.ToString(f.Constant, System.Globalization.CultureInfo.InvariantCulture)},");
        }
        depth--;
        Line("}");
    }

    void Delegate(TypeDefinition def) {
        MethodDefinition invoke = def.Methods.FirstOrDefault(m => m.Name == "Invoke");
        if(invoke == null) return;
        Line($"public unsafe delegate {Naming.Return(invoke.ReturnType)} {Naming.DeclNameWithParams(def)}({Parameters(invoke)})"
            + Constraints(def.GenericParameters, def.DeclaringType?.GenericParameters.Count ?? 0) + ";");
    }

    void Field(FieldDefinition f) {
        if(f.Name == "value__") return;
        string access = Access(f);
        if(access == null) return;
        StringBuilder sb = new(access + " ");
        if(f.HasConstant && f.IsLiteral) {
            sb.Append("const ").Append(Naming.Ref(f.FieldType)).Append(' ').Append(Naming.Escape(f.Name));
            sb.Append(" = ").Append(Naming.Placeholder(f, constOrdinal++)).Append(';');
            Line(sb.ToString());
            return;
        }
        if(f.IsStatic) sb.Append("static ");
        if(f.IsInitOnly) sb.Append("readonly ");
        sb.Append(Naming.Ref(f.FieldType)).Append(' ').Append(Naming.Escape(f.Name)).Append(';');
        Line(sb.ToString());
    }

    void Property(PropertyDefinition p, TypeDefinition owner) {
        MethodDefinition any = p.GetMethod ?? p.SetMethod;
        if(any == null) return;
        string access = Access(any);
        if(access == null) return;

        StringBuilder sb = new();
        if(!owner.IsInterface) {
            sb.Append(access).Append(' ');
            if(any.IsStatic) sb.Append("static ");
            else if(any.IsVirtual) sb.Append(Inheritance(any));
        }
        sb.Append(Naming.Return(p.PropertyType)).Append(' ');
        sb.Append(p.HasParameters
            ? $"this[{Parameters(p.GetMethod ?? p.SetMethod, p.Parameters.Count)}]"
            : Naming.Escape(p.Name));
        sb.Append(" { ");
        if(p.GetMethod != null && Access(p.GetMethod) != null) sb.Append(owner.IsInterface ? "get; " : "get => throw null; ");
        if(p.SetMethod != null && Access(p.SetMethod) != null) sb.Append(owner.IsInterface ? "set; " : "set { } ");
        sb.Append('}');
        Line(sb.ToString());
    }

    void Event(EventDefinition e, TypeDefinition owner) {
        MethodDefinition any = e.AddMethod ?? e.RemoveMethod;
        if(any == null) return;
        string access = Access(any);
        if(access == null) return;
        StringBuilder sb = new();
        if(!owner.IsInterface) {
            sb.Append(access).Append(' ');
            if(any.IsStatic) sb.Append("static ");
            else if(any.IsVirtual) sb.Append(Inheritance(any));
        }
        sb.Append("event ").Append(Naming.Ref(e.EventType)).Append(' ').Append(Naming.Escape(e.Name));
        sb.Append(owner.IsInterface ? ";" : " { add { } remove { } }");
        Line(sb.ToString());
    }

    void Method(MethodDefinition m, TypeDefinition owner) {
        string access = Access(m);
        if(access == null) return;

        if(m.IsConstructor) {
            if(m.IsStatic) return;
            Line($"{access} {Naming.DeclName(owner)}({Parameters(m)}) => throw null;");
            return;
        }

        string op = Naming.Operator(m.Name);
        if(op != null && m.IsStatic && m.Parameters.Count > 0) {
            Line(op is "implicit" or "explicit"
                ? $"public static {op} operator {Naming.Ref(m.ReturnType)}({Parameters(m)}) => throw null;"
                : $"public static {Naming.Return(m.ReturnType)} operator {op}({Parameters(m)}) => throw null;");
            return;
        }

        StringBuilder sb = new();
        if(!owner.IsInterface) {
            sb.Append(access).Append(' ');
            if(m.IsStatic) sb.Append("static ");
            else if(m.IsVirtual) sb.Append(Inheritance(m));
        }
        sb.Append(Naming.Return(m.ReturnType)).Append(' ').Append(Naming.Escape(m.Name));
        if(m.HasGenericParameters)
            sb.Append('<').Append(string.Join(", ", m.GenericParameters.Select(g => Naming.Escape(g.Name)))).Append('>');
        sb.Append('(').Append(Parameters(m)).Append(')');
        sb.Append(Constraints(m.GenericParameters, 0));
        sb.Append(owner.IsInterface ? ";" : " => throw null;");
        Line(sb.ToString());
    }

    // --- shared bits ----------------------------------------------------------

    static string Parameters(MethodDefinition m, int take = -1) {
        IEnumerable<ParameterDefinition> list = m.Parameters;
        if(take >= 0) list = list.Take(take);
        // An extension method has to be emitted AS one: our call sites use instance
        // syntax (`token.Value<string>()`, `tex.LoadImage(bytes)`), which will not bind
        // to a plain static method however identical the signature.
        string prefix = IsExtension(m) ? "this " : "";
        return prefix + string.Join(", ", list.Select(Parameter));
    }

    static bool IsExtension(MethodDefinition m) =>
        m.IsStatic && m.Parameters.Count > 0 && m.CustomAttributes.Any(a =>
            a.AttributeType.FullName == "System.Runtime.CompilerServices.ExtensionAttribute");

    static string Parameter(ParameterDefinition p) {
        StringBuilder sb = new();
        if(p.ParameterType is ByReferenceType) sb.Append(p.IsOut ? "out " : p.IsIn ? "in " : "ref ");
        if(p.CustomAttributes.Any(a => a.AttributeType.FullName == "System.ParamArrayAttribute")) sb.Append("params ");
        sb.Append(Naming.Ref(p.ParameterType)).Append(' ').Append(Naming.Escape(p.Name ?? "arg"));
        if(p.IsOptional) sb.Append(" = default");
        return sb.ToString();
    }

    static string Constraints(IEnumerable<GenericParameter> parameters, int skip) {
        StringBuilder sb = new();
        foreach(GenericParameter g in parameters.Skip(skip)) {
            List<string> parts = new();
            if(g.HasReferenceTypeConstraint) parts.Add("class");
            if(g.HasNotNullableValueTypeConstraint) parts.Add("struct");
            foreach(GenericParameterConstraint c in g.Constraints) {
                string name = c.ConstraintType.FullName;
                if(name is "System.ValueType" or "System.Object" or "System.Enum") continue;
                parts.Add(Naming.Ref(c.ConstraintType));
            }
            if(g.HasDefaultConstructorConstraint && !g.HasNotNullableValueTypeConstraint) parts.Add("new()");
            if(parts.Count > 0) sb.Append($" where {Naming.Escape(g.Name)} : {string.Join(", ", parts)}");
        }
        return sb.ToString();
    }

    /// <summary>Public and protected surface only, so code that would not compile against
    /// the real assembly does not compile against the stub either.</summary>
    static string Access(MethodDefinition m) =>
        m.IsPublic ? "public" : m.IsFamily || m.IsFamilyOrAssembly ? "protected" : null;

    static string Access(FieldDefinition f) =>
        f.IsPublic ? "public"
        : f.IsFamily || f.IsFamilyOrAssembly ? "protected"
        : f.DeclaringType.IsValueType && !f.IsStatic ? "private"
        : null;

    static bool IsAccessor(MethodDefinition m) =>
        m.IsGetter || m.IsSetter || m.IsAddOn || m.IsRemoveOn || m.IsFire;

    /// <summary>
    /// Conversion operators are the one place C# overloads on return type alone, so
    /// their identity has to include it or `(string)token` and `(bool)token` collapse
    /// into a single emitted member.
    /// </summary>
    static string Signature(MethodDefinition m) =>
        m.Name + "`" + m.GenericParameters.Count + "(" +
        string.Join(",", m.Parameters.Select(p => p.ParameterType.FullName)) + ")" +
        (m.Name is "op_Implicit" or "op_Explicit" ? ":" + m.ReturnType.FullName : "");

    static bool IsDelegate(TypeDefinition def) =>
        def.BaseType != null && def.BaseType.FullName is "System.MulticastDelegate" or "System.Delegate";

    static bool IsReadOnly(TypeDefinition def) =>
        def.CustomAttributes.Any(a => a.AttributeType.Name == "IsReadOnlyAttribute");

    static bool IsByRefLike(TypeDefinition def) =>
        def.CustomAttributes.Any(a => a.AttributeType.Name == "IsByRefLikeAttribute");

    /// <summary>
    /// Whether a base type or interface may appear in a stub's declaration. Stub-set
    /// types always may. A BCL type may only when our own code names it, because the
    /// stub then owes explicit implementations for its whole member closure and there
    /// is no reason to take that on for surface nothing calls.
    /// </summary>
    bool Declarable(TypeReference tr) {
        TypeDefinition def = Safe(tr);
        if(def == null) return false;
        if(IsStubbed(def)) return asm.Name == def.Module.Assembly.Name.Name || asm.DependsOn.Contains(def.Module.Assembly.Name.Name);
        return named.Contains(def.FullName);
    }

    bool IsStubbed(TypeDefinition def) {
        string owner = def.Module.Assembly.Name.Name;
        return asm.Name == owner || asm.DependsOn.Contains(owner);
    }

    static TypeDefinition Safe(TypeReference tr) {
        try { return tr?.Resolve(); } catch { return null; }
    }

    void Line(string text = "") {
        if(text.Length > 0) sb.Append(new string(' ', depth * 4));
        sb.Append(text).Append('\n');
    }
}

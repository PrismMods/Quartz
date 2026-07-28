using Mono.Cecil;

namespace Quartz.StubGen;

/// <summary>
/// Reads the compiled mod and works out exactly which game API surface it depends on.
///
/// The input is the built IL rather than the C# sources on purpose: the metadata
/// reference tables ARE the compile-time dependency set, already resolved by Roslyn.
/// Nothing is guessed, and a member we only reach through Refl (late binding) never
/// appears — correctly, since it is not a compile dependency.
/// </summary>
sealed class Collector {
    readonly HashSet<string> targets;
    readonly Dictionary<string, AssemblyReq> output = new(StringComparer.OrdinalIgnoreCase);
    readonly Queue<TypeDefinition> pending = new();
    readonly HashSet<string> queued = new(StringComparer.Ordinal);
    readonly HashSet<string> externallyNamed = new(StringComparer.Ordinal);
    readonly HashSet<string> namedTargets = new(StringComparer.Ordinal);

    readonly Dictionary<string, HashSet<string>> sourceTargets;

    public Collector(IEnumerable<string> targetAssemblyNames, Dictionary<string, HashSet<string>> sourceTargets) {
        targets = new HashSet<string>(targetAssemblyNames, StringComparer.OrdinalIgnoreCase);
        this.sourceTargets = sourceTargets ?? new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
    }

    /// <summary>Member names a string-based reference asked of this exact type.</summary>
    HashSet<string> NamedFromSource(TypeDefinition def) {
        if(sourceTargets.TryGetValue(def.Name, out HashSet<string> byName)) return byName;
        sourceTargets.TryGetValue(def.FullName, out HashSet<string> byFullName);
        return byFullName;
    }

    public IReadOnlyDictionary<string, AssemblyReq> Output => output;

    /// <summary>
    /// Non-stubbed (BCL) types our own IL names directly. A stub only declares a BCL
    /// interface when it shows up here — `using(reader)` leaves an IDisposable.Dispose
    /// reference behind, `foreach` leaves IEnumerable&lt;T&gt;.GetEnumerator, and so on.
    /// Declaring the rest would force the stub to implement interface surface no call
    /// site of ours ever touches.
    /// </summary>
    public IReadOnlySet<string> ExternallyNamed => externallyNamed;

    /// <summary>
    /// Guarantees a stub assembly exists even with no types in it. UnityEngine.dll is
    /// pure type-forwarders: nothing resolves into it, so it would never be discovered,
    /// yet every project references it by name and fails to resolve without it.
    /// </summary>
    public void Require(string assemblyName) {
        if(targets.Contains(assemblyName)) AsmFor(assemblyName);
    }

    public void Add(ModuleDefinition module) {
        foreach(AssemblyNameReference reference in module.AssemblyReferences) Require(reference.Name);
        foreach(TypeReference tr in module.GetTypeReferences()) {
            Want(tr);
            NoteExternal(tr);
        }
        foreach(MemberReference mr in module.GetMemberReferences()) {
            WantMember(mr);
            NoteExternal(mr.DeclaringType);
        }
        foreach(TypeDefinition ours in AllTypes(module)) InspectOurType(ours);
    }

    void NoteExternal(TypeReference tr) {
        foreach(TypeReference part in Walk(tr)) {
            TypeDefinition def = Resolve(part);
            if(def == null) continue;
            (IsTarget(def) ? namedTargets : externallyNamed).Add(def.FullName);
        }
    }

    /// <summary>
    /// Members our own types inherit or implement. An `override` or an implicit
    /// interface implementation emits no MemberReference, so walking the reference
    /// tables alone would miss the base declaration and our override would not compile.
    /// </summary>
    void InspectOurType(TypeDefinition ours) {
        TypeDefinition baseDef = Resolve(ours.BaseType);
        if(baseDef != null && IsTarget(baseDef)) {
            ReqFor(baseDef).Subclassed = true;
            foreach(MethodDefinition m in ours.Methods) {
                if(!m.IsVirtual || m.IsNewSlot) continue;
                MethodDefinition based = FindInBaseChain(baseDef, m);
                if(based != null) Want(based);
            }
        }
        foreach(InterfaceImplementation impl in ours.Interfaces) {
            TypeDefinition iface = Resolve(impl.InterfaceType);
            if(iface == null || !IsTarget(iface)) continue;
            ReqFor(iface).Implemented = true;
            Want(iface);
        }
    }

    static MethodDefinition FindInBaseChain(TypeDefinition start, MethodDefinition wanted) {
        for(TypeDefinition t = start; t != null; t = Resolve(t.BaseType)) {
            foreach(MethodDefinition m in t.Methods) {
                if(m.Name != wanted.Name) continue;
                if(m.Parameters.Count != wanted.Parameters.Count) continue;
                if(!m.IsVirtual) continue;
                bool same = true;
                for(int i = 0; i < m.Parameters.Count; i++) {
                    if(!SameType(m.Parameters[i].ParameterType, wanted.Parameters[i].ParameterType)) {
                        same = false;
                        break;
                    }
                }
                if(same) return m;
            }
        }
        return null;
    }

    static IEnumerable<TypeDefinition> AllTypes(ModuleDefinition module) {
        foreach(TypeDefinition t in module.Types)
            foreach(TypeDefinition n in Nested(t))
                yield return n;
    }

    static IEnumerable<TypeDefinition> Nested(TypeDefinition t) {
        yield return t;
        foreach(TypeDefinition c in t.NestedTypes)
            foreach(TypeDefinition n in Nested(c))
                yield return n;
    }

    // --- wanting things -------------------------------------------------------

    public void Want(TypeReference tr) {
        TypeDefinition def = Resolve(tr);
        if(def == null || !IsTarget(def)) return;
        ReqFor(def);
    }

    void Want(MethodDefinition m) {
        TypeDefinition owner = m.DeclaringType;
        if(!IsTarget(owner)) return;
        TypeReq req = ReqFor(owner);
        if(!req.Methods.Add(m)) return;
        Want(m.ReturnType);
        foreach(ParameterDefinition p in m.Parameters) Want(p.ParameterType);
        foreach(GenericParameter g in m.GenericParameters)
            foreach(GenericParameterConstraint c in g.Constraints)
                Want(c.ConstraintType);
        AttachAccessor(req, m);
        WantOperatorPartner(owner, m);
    }

    static readonly Dictionary<string, string> OperatorPairs = new(StringComparer.Ordinal) {
        ["op_Equality"] = "op_Inequality",
        ["op_Inequality"] = "op_Equality",
        ["op_LessThan"] = "op_GreaterThan",
        ["op_GreaterThan"] = "op_LessThan",
        ["op_LessThanOrEqual"] = "op_GreaterThanOrEqual",
        ["op_GreaterThanOrEqual"] = "op_LessThanOrEqual",
        ["op_True"] = "op_False",
        ["op_False"] = "op_True",
    };

    /// <summary>
    /// C# rejects a type that declares one half of an operator pair without the other,
    /// so requiring `==` implicitly requires `!=` even where our code never calls it.
    /// </summary>
    void WantOperatorPartner(TypeDefinition owner, MethodDefinition m) {
        if(!OperatorPairs.TryGetValue(m.Name, out string partnerName)) return;
        foreach(MethodDefinition candidate in owner.Methods) {
            if(candidate.Name != partnerName) continue;
            if(candidate.Parameters.Count != m.Parameters.Count) continue;
            bool same = true;
            for(int i = 0; i < candidate.Parameters.Count; i++) {
                if(candidate.Parameters[i].ParameterType.FullName != m.Parameters[i].ParameterType.FullName) {
                    same = false;
                    break;
                }
            }
            if(same) { Want(candidate); return; }
        }
    }

    /// <summary>
    /// A property or event accessor must be emitted as the property/event itself,
    /// or C# call sites (`x.Foo = 1`, `x.Bar += h`) will not bind to a bare method.
    /// </summary>
    static void AttachAccessor(TypeReq req, MethodDefinition m) {
        foreach(PropertyDefinition p in m.DeclaringType.Properties)
            if(p.GetMethod == m || p.SetMethod == m) {
                req.Properties.Add(p);
                return;
            }
        foreach(EventDefinition e in m.DeclaringType.Events)
            if(e.AddMethod == m || e.RemoveMethod == m || e.InvokeMethod == m) {
                req.Events.Add(e);
                return;
            }
    }

    void WantMember(MemberReference mr) {
        TypeDefinition owner = Resolve(mr.DeclaringType);
        if(owner == null || !IsTarget(owner)) return;
        // A closed generic call (`Instantiate<GameObject>(...)`) keeps its type parameters
        // on the open method, so matching against the instance would compare an arity of
        // zero and fall through to the wrong overload.
        if(mr is GenericInstanceMethod instance) mr = instance.ElementMethod;
        switch(mr) {
            case MethodReference method: {
                MethodDefinition def = ResolveMethod(owner, method);
                if(def != null) Want(def);
                break;
            }
            case FieldReference field: {
                FieldDefinition def = owner.Fields.FirstOrDefault(f => f.Name == field.Name);
                if(def == null) break;
                TypeReq req = ReqFor(owner);
                if(req.Fields.Add(def)) Want(def.FieldType);
                break;
            }
        }
    }

    /// <summary>
    /// Return type is part of the match, not just the parameters: conversion operators
    /// overload on it alone, so `(string)token` and `(bool)token` are different members
    /// with identical parameter lists and would otherwise collapse into whichever one
    /// the metadata happens to list first.
    /// </summary>
    static MethodDefinition ResolveMethod(TypeDefinition owner, MethodReference reference) {
        MethodDefinition nearest = null;
        foreach(MethodDefinition m in owner.Methods) {
            if(m.Name != reference.Name) continue;
            if(m.Parameters.Count != reference.Parameters.Count) continue;
            if(m.GenericParameters.Count != reference.GenericParameters.Count) continue;
            nearest ??= m;
            bool same = SameType(m.ReturnType, reference.ReturnType);
            for(int i = 0; same && i < m.Parameters.Count; i++)
                same = SameType(m.Parameters[i].ParameterType, reference.Parameters[i].ParameterType);
            if(same) return m;
        }
        return nearest;
    }

    /// <summary>
    /// Structural type comparison. FullName is not enough: a generic parameter is named
    /// `T` in the definition it came from but positionally (`!!0`) in a reference out of
    /// another assembly, so comparing the strings makes every generic overload look
    /// different and resolution falls back to whichever one is listed first.
    /// </summary>
    static bool SameType(TypeReference a, TypeReference b) {
        if(a == null || b == null) return a == b;
        if(a is GenericParameter ga)
            return b is GenericParameter gb && ga.Position == gb.Position && ga.Type == gb.Type;
        if(b is GenericParameter) return false;
        switch(a) {
            case ArrayType aa:
                return b is ArrayType ab && aa.Rank == ab.Rank && SameType(aa.ElementType, ab.ElementType);
            case ByReferenceType ra:
                return b is ByReferenceType rb && SameType(ra.ElementType, rb.ElementType);
            case PointerType pa:
                return b is PointerType pb && SameType(pa.ElementType, pb.ElementType);
            case GenericInstanceType ia: {
                if(b is not GenericInstanceType ib) return false;
                if(ia.GenericArguments.Count != ib.GenericArguments.Count) return false;
                if(!SameType(ia.ElementType, ib.ElementType)) return false;
                for(int i = 0; i < ia.GenericArguments.Count; i++)
                    if(!SameType(ia.GenericArguments[i], ib.GenericArguments[i])) return false;
                return true;
            }
            default:
                return a.FullName == b.FullName;
        }
    }

    /// <summary>
    /// Registers a type and everything its declaration needs to be legal C#: the base
    /// chain, interfaces, generic constraints, and — for enums and structs — the fields
    /// that give the type its values and its size.
    /// </summary>
    TypeReq ReqFor(TypeDefinition def) {
        AssemblyReq asm = AsmFor(def);
        TypeReq req = asm.For(def);
        if(queued.Add(def.FullName)) pending.Enqueue(def);
        return req;
    }

    public void Close() {
        Drain();
        if(SatisfyConstraints()) Drain();
        RecordDependencies();
    }

    void Drain() {
        while(pending.Count > 0) {
            TypeDefinition def = pending.Dequeue();
            AssemblyReq asm = AsmFor(def);
            TypeReq req = asm.For(def);

            if(def.DeclaringType != null) ReqFor(def.DeclaringType);
            Want(def.BaseType);
            foreach(InterfaceImplementation i in def.Interfaces) {
                Want(i.InterfaceType);
                // Declared in the stub only when our own code names the interface — and if
                // it is declared, the stub owes its whole member closure, so pull that in.
                TypeDefinition iface = Resolve(i.InterfaceType);
                if(iface == null || !externallyNamed.Contains(iface.FullName) && !IsNamedTarget(iface)) continue;
                if(req.ExplicitInterfaces.Any(x => x.FullName == i.InterfaceType.FullName)) continue;
                req.ExplicitInterfaces.Add(i.InterfaceType);
                if(IsTarget(iface)) {
                    ReqFor(iface).Implemented = true;
                    if(queued.Contains(iface.FullName)) pending.Enqueue(iface);
                }
            }
            foreach(GenericParameter g in def.GenericParameters)
                foreach(GenericParameterConstraint c in g.Constraints)
                    Want(c.ConstraintType);

            // Members named only from a string — nameof(), or Harmony's string form — leave
            // no reference either. SourceScan recovered which type each name was asked of;
            // match every overload, since a name alone cannot say which one was meant.
            HashSet<string> fromSource = NamedFromSource(def);
            if(fromSource != null) {
                foreach(MethodDefinition m in def.Methods)
                    if(fromSource.Contains(m.Name) && (m.IsPublic || m.IsFamily)) Want(m);
                foreach(FieldDefinition f in def.Fields)
                    if(fromSource.Contains(f.Name) && (f.IsPublic || f.IsFamily)) {
                        req.Fields.Add(f);
                        Want(f.FieldType);
                    }
                foreach(PropertyDefinition p in def.Properties)
                    if(fromSource.Contains(p.Name)) {
                        req.Properties.Add(p);
                        Want(p.PropertyType);
                    }
            }

            // Roslyn folds a const or enum member straight into the call site, so neither
            // leaves a reference behind and neither can be discovered from the metadata
            // like everything else here. They are the one category that has to be taken
            // wholesale: every public const on a required type, and every nested enum.
            foreach(FieldDefinition f in def.Fields)
                if(f.IsLiteral && f.HasConstant && f.IsPublic && !def.IsEnum) {
                    req.Fields.Add(f);
                    Want(f.FieldType);
                }
            foreach(TypeDefinition nested in def.NestedTypes)
                if(nested.IsEnum && (nested.IsNestedPublic || nested.IsNestedFamily)) ReqFor(nested);

            if(def.IsEnum) {
                foreach(FieldDefinition f in def.Fields) {
                    req.Fields.Add(f);
                    Want(f.FieldType);
                }
            } else if(def.IsValueType) {
                // A struct's instance fields determine its size and definite-assignment
                // rules, so a stub that omits them is not a drop-in for `default(T)`
                // or for anything that embeds it by value.
                foreach(FieldDefinition f in def.Fields) {
                    if(f.IsStatic) continue;
                    req.Fields.Add(f);
                    Want(f.FieldType);
                }
            }

            if(req.Implemented) {
                foreach(MethodDefinition m in def.Methods) Want(m);
                foreach(PropertyDefinition p in def.Properties) req.Properties.Add(p);
                foreach(EventDefinition e in def.Events) req.Events.Add(e);
                foreach(InterfaceImplementation i in def.Interfaces) {
                    TypeDefinition inherited = Resolve(i.InterfaceType);
                    if(inherited == null || !IsTarget(inherited)) continue;
                    TypeReq up = ReqFor(inherited);
                    if(up.Implemented) continue;
                    up.Implemented = true;
                    pending.Enqueue(inherited);
                }
            }

            if(req.Subclassed) {
                // Abstract members must be present or our override has nothing to override,
                // and an accessible constructor must exist or the derived type cannot chain.
                foreach(MethodDefinition m in def.Methods)
                    if(m.IsAbstract || m.IsConstructor && !m.IsStatic && !m.IsPrivate) Want(m);
            }

            foreach(FieldDefinition f in req.Fields) Want(f.FieldType);
            foreach(PropertyDefinition p in req.Properties) {
                Want(p.PropertyType);
                foreach(ParameterDefinition ip in p.Parameters) Want(ip.ParameterType);
            }
            foreach(EventDefinition e in req.Events) Want(e.EventType);
            foreach(MethodDefinition m in req.Methods.ToArray()) {
                Want(m.ReturnType);
                foreach(ParameterDefinition p in m.Parameters) Want(p.ParameterType);
            }
        }
    }

    /// <summary>
    /// A stub used as a generic argument still has to satisfy the type parameter's
    /// constraints, so an interface that only ever appears in a `where` clause must be
    /// declared on the types that implement it even though no call site names it.
    /// </summary>
    bool SatisfyConstraints() {
        HashSet<string> constrained = new(StringComparer.Ordinal);
        foreach(AssemblyReq asm in output.Values)
            foreach(TypeReq req in asm.Types.Values) {
                Constraints(req.Def.GenericParameters, constrained);
                foreach(MethodDefinition m in req.Methods) Constraints(m.GenericParameters, constrained);
            }
        if(constrained.Count == 0) return false;

        bool added = false;
        foreach(AssemblyReq asm in output.Values.ToArray())
            foreach(TypeReq req in asm.Types.Values.ToArray()) {
                if(req.Def.IsInterface) continue;
                foreach(InterfaceImplementation i in req.Def.Interfaces) {
                    TypeDefinition iface = Resolve(i.InterfaceType);
                    if(iface == null || !constrained.Contains(iface.FullName)) continue;
                    if(req.ExplicitInterfaces.Any(x => x.FullName == i.InterfaceType.FullName)) continue;
                    req.ExplicitInterfaces.Add(i.InterfaceType);
                    added = true;
                    if(!IsTarget(iface)) continue;
                    TypeReq target = ReqFor(iface);
                    if(target.Implemented) continue;
                    target.Implemented = true;
                    pending.Enqueue(iface);
                }
            }
        return added;
    }

    static void Constraints(IEnumerable<GenericParameter> parameters, HashSet<string> into) {
        foreach(GenericParameter g in parameters)
            foreach(GenericParameterConstraint constraint in g.Constraints) {
                TypeDefinition def = Resolve(constraint.ConstraintType);
                if(def != null && def.IsInterface) into.Add(def.FullName);
            }
    }

    /// <summary>Which stub assemblies reference which, so the generated projects can
    /// carry the right ProjectReferences.</summary>
    void RecordDependencies() {
        foreach(AssemblyReq asm in output.Values) {
            foreach(TypeReq req in asm.Types.Values) {
                Note(asm, req.Def.BaseType);
                foreach(InterfaceImplementation i in req.Def.Interfaces) Note(asm, i.InterfaceType);
                if(req.Def.DeclaringType != null) Note(asm, req.Def.DeclaringType);
                foreach(FieldDefinition f in req.Fields) Note(asm, f.FieldType);
                foreach(PropertyDefinition p in req.Properties) Note(asm, p.PropertyType);
                foreach(EventDefinition e in req.Events) Note(asm, e.EventType);
                foreach(MethodDefinition m in req.Methods) {
                    Note(asm, m.ReturnType);
                    foreach(ParameterDefinition p in m.Parameters) Note(asm, p.ParameterType);
                }
            }
            asm.DependsOn.Remove(asm.Name);
        }
    }

    void Note(AssemblyReq from, TypeReference tr) {
        foreach(TypeReference part in Walk(tr)) {
            TypeDefinition def = Resolve(part);
            if(def == null || !IsTarget(def)) continue;
            asmName(def, out string name);
            from.DependsOn.Add(name);
        }
    }

    static IEnumerable<TypeReference> Walk(TypeReference tr) {
        if(tr == null) yield break;
        yield return tr;
        if(tr is GenericInstanceType git)
            foreach(TypeReference arg in git.GenericArguments)
                foreach(TypeReference inner in Walk(arg))
                    yield return inner;
        if(tr is TypeSpecification spec)
            foreach(TypeReference inner in Walk(spec.ElementType))
                yield return inner;
    }

    // --- plumbing -------------------------------------------------------------

    AssemblyReq AsmFor(TypeDefinition def) {
        asmName(def, out string name);
        return AsmFor(name);
    }

    AssemblyReq AsmFor(string name) {
        if(!output.TryGetValue(name, out AssemblyReq asm)) output[name] = asm = new AssemblyReq(name);
        return asm;
    }

    static void asmName(TypeDefinition def, out string name) =>
        name = def.Module.Assembly.Name.Name;

    bool IsTarget(TypeDefinition def) =>
        def != null && targets.Contains(def.Module.Assembly.Name.Name);

    /// <summary>A stub-set type our own IL names directly, as opposed to one that only
    /// got pulled in because it appeared in somebody else's signature.</summary>
    bool IsNamedTarget(TypeDefinition def) =>
        IsTarget(def) && namedTargets.Contains(def.FullName);

    static TypeDefinition Resolve(TypeReference tr) {
        if(tr == null) return null;
        while(tr is TypeSpecification spec) {
            if(tr is GenericInstanceType git) { tr = git.ElementType; break; }
            tr = spec.ElementType;
        }
        if(tr is GenericParameter) return null;
        try { return tr.Resolve(); } catch { return null; }
    }
}

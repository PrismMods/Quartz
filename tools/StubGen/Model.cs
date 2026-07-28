using Mono.Cecil;

namespace Quartz.StubGen;

/// <summary>
/// Everything the generator decided one game type owes us: the type itself plus the
/// exact members our compiled IL names. A type can be "required" with zero members —
/// that happens when it only ever appears in a signature we touch.
/// </summary>
sealed class TypeReq {
    public TypeDefinition Def;
    public readonly HashSet<MethodDefinition> Methods = new();
    public readonly HashSet<FieldDefinition> Fields = new();
    public readonly HashSet<PropertyDefinition> Properties = new();
    public readonly HashSet<EventDefinition> Events = new();

    /// <summary>We derive from this type, so it needs an accessible constructor and
    /// every abstract member we override.</summary>
    public bool Subclassed;

    /// <summary>We implement this interface, so it needs its full member list —
    /// an implementing type must satisfy all of it, and `override`/implicit
    /// implementations leave no MemberReference behind to discover them from.</summary>
    public bool Implemented;

    /// <summary>Interfaces this type declares in the emitted stub. Only the ones our own
    /// code names: an interface nothing casts to is surface the stub would have to
    /// implement for no one's benefit.</summary>
    public readonly List<TypeReference> ExplicitInterfaces = new();

    public TypeReq(TypeDefinition def) => Def = def;
}

/// <summary>One stub assembly to emit, named exactly like the game assembly it stands in for.</summary>
sealed class AssemblyReq {
    public string Name;
    public readonly Dictionary<string, TypeReq> Types = new(StringComparer.Ordinal);

    /// <summary>Other stub assemblies whose types appear in this one's signatures.</summary>
    public readonly HashSet<string> DependsOn = new(StringComparer.OrdinalIgnoreCase);

    public AssemblyReq(string name) => Name = name;

    public TypeReq For(TypeDefinition def) {
        string key = def.FullName;
        if(!Types.TryGetValue(key, out TypeReq req)) Types[key] = req = new TypeReq(def);
        return req;
    }
}

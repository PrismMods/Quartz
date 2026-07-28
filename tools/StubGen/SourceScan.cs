using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Quartz.StubGen;

/// <summary>
/// Recovers the members that never reach the compiled metadata.
///
/// `nameof(scrController.Awake)` and `[HarmonyPatch(typeof(X), "Method")]` both become
/// plain string literals, so a member named only that way leaves no MemberReference to
/// find — and that is how nearly every Harmony patch here names its target. Without
/// this pass the stub set would be missing exactly the API the mod leans on hardest.
///
/// Results are (type name -> member names) pairs rather than a flat name list. A bare
/// list would match "Start" or "Update" against every type in the set and drag in
/// unrelated API; the attribute already tells us which type was meant, so use it.
/// </summary>
static class SourceScan {
    public static Dictionary<string, HashSet<string>> Targets(IEnumerable<string> roots) {
        Dictionary<string, HashSet<string>> found = new(StringComparer.Ordinal);
        foreach(string root in roots) {
            if(!Directory.Exists(root)) continue;
            foreach(string file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)) {
                if(Skipped(file)) continue;
                SyntaxNode tree;
                try { tree = CSharpSyntaxTree.ParseText(File.ReadAllText(file)).GetRoot(); } catch { continue; }
                Walk(tree, found);
            }
        }
        return found;
    }

    static bool Skipped(string file) {
        string sep = Path.DirectorySeparatorChar.ToString();
        return file.Contains(sep + "obj" + sep, StringComparison.Ordinal)
            || file.Contains(sep + "bin" + sep, StringComparison.Ordinal);
    }

    static void Walk(SyntaxNode root, Dictionary<string, HashSet<string>> found) {
        // nameof(Type.Member) — the receiver's own text is the type name in every
        // Harmony patch here. When it happens to be a local instead, the key simply
        // matches no type and costs nothing.
        foreach(InvocationExpressionSyntax invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>()) {
            if(invocation.Expression is not IdentifierNameSyntax { Identifier.ValueText: "nameof" }) continue;
            if(invocation.ArgumentList.Arguments.Count != 1) continue;
            if(invocation.ArgumentList.Arguments[0].Expression is not MemberAccessExpressionSyntax access) continue;
            string owner = access.Expression switch {
                IdentifierNameSyntax id => id.Identifier.ValueText,
                MemberAccessExpressionSyntax outer => outer.Name.Identifier.ValueText,
                _ => null,
            };
            if(owner != null) Add(found, owner, access.Name.Identifier.ValueText);
        }

        // [HarmonyPatch(typeof(X), "Member")] and the split form, where typeof(X) sits on
        // the class and the member name on a nested method attribute.
        foreach(AttributeSyntax attribute in root.DescendantNodes().OfType<AttributeSyntax>()) {
            if(!attribute.Name.ToString().Contains("Harmony", StringComparison.Ordinal)) continue;
            List<string> literals = Literals(attribute);
            if(literals.Count == 0) continue;
            foreach(string owner in OwnersInScope(attribute))
                foreach(string member in literals)
                    Add(found, owner, member);
        }
    }

    static List<string> Literals(AttributeSyntax attribute) {
        List<string> values = new();
        if(attribute.ArgumentList == null) return values;
        foreach(AttributeArgumentSyntax argument in attribute.ArgumentList.Arguments)
            if(argument.Expression is LiteralExpressionSyntax literal
                && literal.IsKind(SyntaxKind.StringLiteralExpression))
                values.Add(literal.Token.ValueText);
        return values;
    }

    /// <summary>
    /// Every `typeof(X)` a Harmony attribute could be talking about: the ones on the
    /// attribute itself, then any on the enclosing declarations, since the convention is
    /// `[HarmonyPatch(typeof(X))]` on the class and `[HarmonyPatch("Member")]` inside it.
    /// </summary>
    static IEnumerable<string> OwnersInScope(AttributeSyntax attribute) {
        HashSet<string> owners = new(StringComparer.Ordinal);
        foreach(string own in TypeOfNames(attribute)) owners.Add(own);
        for(SyntaxNode node = attribute.Parent; node != null; node = node.Parent) {
            if(node is not MemberDeclarationSyntax declaration) continue;
            foreach(AttributeListSyntax list in declaration.AttributeLists)
                foreach(AttributeSyntax other in list.Attributes) {
                    if(!other.Name.ToString().Contains("Harmony", StringComparison.Ordinal)) continue;
                    foreach(string own in TypeOfNames(other)) owners.Add(own);
                }
        }
        return owners;
    }

    static IEnumerable<string> TypeOfNames(AttributeSyntax attribute) {
        if(attribute.ArgumentList == null) yield break;
        foreach(AttributeArgumentSyntax argument in attribute.ArgumentList.Arguments) {
            if(argument.Expression is not TypeOfExpressionSyntax typeOf) continue;
            string name = typeOf.Type switch {
                IdentifierNameSyntax id => id.Identifier.ValueText,
                QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText,
                GenericNameSyntax generic => generic.Identifier.ValueText,
                _ => null,
            };
            if(name != null) yield return name;
        }
    }

    static void Add(Dictionary<string, HashSet<string>> found, string owner, string member) {
        if(!found.TryGetValue(owner, out HashSet<string> members))
            found[owner] = members = new HashSet<string>(StringComparer.Ordinal);
        members.Add(member);
    }
}

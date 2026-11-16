// Derived from ILSpy (MIT): see LICENSE-ILSpy.txt

using ICSharpCode.Decompiler.TypeSystem;

namespace ILSight.Core.AnalysisArea;

/// <summary>Collects the type definitions a member's signature names, as <see cref="TypeExposedByAnalyzer"/> reads it: its
/// return type's and its parameters', of a generic type only the generic type itself. The library's walk, a read; only
/// collecting, it decides nothing.</summary>
internal sealed class ExposedTypeCollector : TypeVisitor
{
    private readonly List<DefinitionKey> _named = new();

    /// <summary>The type definitions <paramref name="member"/>'s signature names, in the order the walk meets them.</summary>
    public static IReadOnlyList<DefinitionKey> Collect(IMember member)
    {
        var collector = new ExposedTypeCollector();
        member.ReturnType.AcceptVisitor(collector);
        if (member is IParameterizedMember parameterized)
        {
            foreach (var parameter in parameterized.Parameters)
            {
                parameter.Type.AcceptVisitor(collector);
            }
        }
        return collector._named;
    }

    public override IType VisitTypeDefinition(ITypeDefinition type)
    {
        _named.Add(DefinitionKey.Of(type));
        return base.VisitTypeDefinition(type);
    }

    public override IType VisitParameterizedType(ParameterizedType type)
    {
        return type.GenericType.AcceptVisitor(this);
    }
}

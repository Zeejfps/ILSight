// Derived from ILSpy (MIT): see LICENSE-ILSpy.txt

using ICSharpCode.Decompiler.TypeSystem;

namespace ILSight.Core.Analysis;

/// <summary>Collects the type definitions a type is made of, as <see cref="MethodUsesAnalyzer"/> visits the types a
/// method's code uses.</summary>
internal sealed class TypeDefinitionCollector : TypeVisitor
{
    private readonly List<ITypeDefinition> _usedTypes = new();

    public IReadOnlyList<ITypeDefinition> UsedTypes => _usedTypes;

    public override IType VisitTypeDefinition(ITypeDefinition type)
    {
        _usedTypes.Add(type);
        return base.VisitTypeDefinition(type);
    }
}

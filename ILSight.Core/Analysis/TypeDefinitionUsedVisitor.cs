// Derived from ILSpy (MIT): see LICENSE-ILSpy.txt

using ICSharpCode.Decompiler.TypeSystem;

namespace ILSight.Core.Analysis;

/// <summary>Tells whether a member's signature names one type definition, for <see cref="TypeExposedByAnalyzer"/>.</summary>
internal sealed class TypeDefinitionUsedVisitor(ITypeDefinition definition) : TypeVisitor
{
    private bool _found;

    public bool IsExposedBy(IMember member)
    {
        if (member.Accessibility == Accessibility.Private && !member.IsExplicitInterfaceImplementation)
        {
            return false;
        }

        _found = false;
        member.ReturnType.AcceptVisitor(this);
        if (member is IParameterizedMember parameterized)
        {
            foreach (var parameter in parameterized.Parameters)
            {
                parameter.Type.AcceptVisitor(this);
            }
        }
        return _found;
    }

    public override IType VisitTypeDefinition(ITypeDefinition type)
    {
        _found |= Members.IsSame(type, definition);
        return base.VisitTypeDefinition(type);
    }

    public override IType VisitParameterizedType(ParameterizedType type)
    {
        return type.GenericType.AcceptVisitor(this);
    }
}

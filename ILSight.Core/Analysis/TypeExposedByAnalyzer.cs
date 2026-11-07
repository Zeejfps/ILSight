// Derived from ILSpy (MIT): see LICENSE-ILSpy.txt

using ICSharpCode.Decompiler.TypeSystem;

namespace ILSight.Core.Analysis;

/// <summary>Finds the visible members whose signature exposes a type.</summary>
internal static class TypeExposedByAnalyzer
{
    public static AnalysisRun? Prepare(ISymbol? symbol)
    {
        if (symbol is not ITypeDefinition type)
            return null;
        return context => Analyze(type, context);
    }

    private static IEnumerable<ISymbol> Analyze(ITypeDefinition analyzed, AnalyzerContext context)
    {
        var visitor = new TypeDefinitionUsedVisitor(analyzed);
        foreach (var type in context.GetScopeOf(analyzed).GetTypesInScope(context.CancellationToken))
        {
            if (analyzed.Kind == TypeKind.Enum && AnalyzerHelpers.IsSame(type, analyzed) || Members.IsHidden(type))
                continue;
            foreach (var member in type.Fields.Concat<IMember>(type.Properties).Concat(type.Events).Concat(type.Methods))
            {
                if (visitor.IsExposedBy(member))
                    yield return member;
            }
        }
    }

    private sealed class TypeDefinitionUsedVisitor(ITypeDefinition definition) : TypeVisitor
    {
        private bool _found;

        public bool IsExposedBy(IMember member)
        {
            if (member.Accessibility == Accessibility.Private && !member.IsExplicitInterfaceImplementation)
                return false;
            _found = false;
            member.ReturnType.AcceptVisitor(this);
            if (member is IParameterizedMember parameterized)
            {
                foreach (var parameter in parameterized.Parameters)
                    parameter.Type.AcceptVisitor(this);
            }
            return _found;
        }

        public override IType VisitTypeDefinition(ITypeDefinition type)
        {
            _found |= AnalyzerHelpers.IsSame(type, definition);
            return base.VisitTypeDefinition(type);
        }

        public override IType VisitParameterizedType(ParameterizedType type) => type.GenericType.AcceptVisitor(this);
    }
}

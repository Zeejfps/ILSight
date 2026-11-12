using ICSharpCode.Decompiler.TypeSystem;

namespace ILSight.Core.Analysis;

/// <summary>Shows the base members a member overrides, up its override chain.</summary>
internal static class MemberOverridesAnalyzer
{
    public static AnalysisRun? Prepare(ISymbol? symbol)
    {
        if (symbol is not IMember { SymbolKind: SymbolKind.Event or SymbolKind.Indexer or SymbolKind.Method or SymbolKind.Property, IsOverride: true } member)
        {
            return null;
        }

        return _ => Analyze(member);
    }

    private static IEnumerable<ISymbol> Analyze(IMember member)
    {
        var visited = new HashSet<IMember>();
        while (member.IsOverride)
        {
            member = member.MemberDefinition;
            if (!visited.Add(member))
            {
                break;
            }

            var baseMember = InheritanceHelper.GetBaseMember(member);
            if (baseMember is null)
            {
                break;
            }

            yield return baseMember;
            member = baseMember;
        }
    }
}

namespace ILSight.App.Tests;

internal static class FoundAnalyses
{
    /// <summary>The analyzer's roots whose symbols were found.</summary>
    public static List<AnalyzerSymbolNode> Found(this AnalyzerTree analyzer)
    {
        return analyzer.Roots
            .OfType<AnalyzerSymbolNode>()
            .ToList();
    }
}

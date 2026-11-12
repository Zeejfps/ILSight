using ILSight.Core;

namespace ILSight.AssemblyHandling;

/// <summary>The assemblies on the open list and the analyses of them, which keep one rule: an analysis is of an
/// assembly on the list. The list is ILSight.Core's and the analyses the app's, so no one type holds both: what changes
/// them under that rule takes the two.</summary>
public static class OpenAssemblies
{
    /// <summary>Takes every assembly off <paramref name="list"/>, and the analyses of them off
    /// <paramref name="analyzer"/> with it.</summary>
    public static void CloseAll(AssemblyList list, AnalyzerTree analyzer)
    {
        AssemblyListing.CloseAll(list);
        analyzer.ClearRoots();
    }
}

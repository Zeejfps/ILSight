using ILSight.Core;

namespace ILSight;

/// <summary>The browse tree following the open assemblies, as does the analyzer's rows following the analyses
/// (<see cref="BrowseTree"/> keeps what the tree's rows show and how they expand).</summary>
public static class Trees
{
    /// <summary>Forgets what the tree had expanded and selected, and its menu's target, once the list
    /// was cleared; rebuilds its roots when the open assemblies changed; and lists the analyzer's rows
    /// again when its roots changed. The roots of the assemblies read from <paramref name="reopening"/>
    /// spin while they are reopened.</summary>
    internal static void SyncTrees(BrowseTree tree, AnalyzerRows rows, ShownChanges shown, AssemblyList assemblies, AnalyzerTree analyzer,
        ApiVisibility visibility, IReadOnlyCollection<AssemblyPath> reopening)
    {
        ForgetClearedList(tree, shown, assemblies);
        tree.ShowApi(visibility);
        tree.TrySyncRoots(assemblies);
        tree.ShowReopening(reopening);
        rows.FollowTree(analyzer);
    }

    private static void ForgetClearedList(BrowseTree tree, ShownChanges shown, AssemblyList assemblies)
    {
        if (!shown.TryTakeListGeneration(assemblies.Generation))
        {
            return;
        }

        tree.Forget();
    }
}

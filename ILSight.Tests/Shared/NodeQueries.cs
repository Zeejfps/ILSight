using ILSight.Core;

namespace ILSight.Tests;

internal static class NodeQueries
{
    /// <summary>The node at <paramref name="path"/> under <paramref name="node"/>, one label a level.</summary>
    public static BrowseNode Child(this BrowseNode node, params ReadOnlySpan<string> path)
    {
        foreach (var text in path)
        {
            node = ChildNamed(node, text);
        }

        return node;
    }

    /// <summary>The child labelled <paramref name="text"/>; the one the source declared when the
    /// compiler made up another of the same label, as it does for an event's field.</summary>
    private static BrowseNode ChildNamed(BrowseNode node, string text)
    {
        var named = node.Children
            .Where(c => c.Text == text)
            .ToList();
        if (named.Count > 1)
        {
            named = named.FindAll(IsSourceDeclared);
        }

        var child = named.SingleOrDefault();
        if (child is null)
        {
            throw new InvalidOperationException($"'{node.Text}' has no child '{text}': {string.Join(", ", node.Children.Select(c => c.Text))}");
        }

        return child;
    }

    private static bool IsSourceDeclared(BrowseNode node)
    {
        return !node.IsCompilerGenerated;
    }

    public static NodeContent Content(this BrowseNode node, DecompilationLanguage language = DecompilationLanguage.CSharp)
    {
        return TestWaits.Wait(() => NodeContentLoader.GetContentAsync(TestSession.Shared, node, language, DecompilerOptions.Default));
    }

    public static DecompiledDocument Decompile(this BrowseNode node, DecompilationLanguage language, CancellationToken cancellationToken = default)
    {
        return TestWaits.Wait(() => NodeContentLoader.DecompileAsync(TestSession.Shared, node, language, DecompilerOptions.Default, cancellationToken));
    }

    public static DecompiledDocument Decompile(this BrowseNode node, DecompilationLanguage language, DecompilerOptions options,
        CancellationToken cancellationToken = default)
    {
        return TestWaits.Wait(() => NodeContentLoader.DecompileAsync(TestSession.Shared, node, language, options, cancellationToken));
    }
}

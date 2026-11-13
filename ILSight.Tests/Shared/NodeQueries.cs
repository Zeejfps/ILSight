using ILSight.Core;

namespace ILSight.Tests;

/// <summary>A node of <see cref="TestSession.Shared"/>'s tree read further, each an async read the test awaits (also
/// of a node still being read, so the reads chain): a child by its labels, what the node shows, the node decompiled.</summary>
internal static class NodeQueries
{
    /// <summary>How many rows a grid these tests show reads with its content: as many as the app's first show of a grid
    /// measures its columns over.</summary>
    public const int FirstGridRows = 200;

    /// <summary>The node at <paramref name="path"/> under <paramref name="node"/>, one label a level, each level's
    /// children built when nobody built them yet; fails the test when a level has no such child.</summary>
    public static async Task<BrowseNode> GetChildAsync(this BrowseNode node, params string[] path)
    {
        foreach (var text in path)
        {
            node = await GetChildNamedAsync(node, text);
        }

        return node;
    }

    /// <summary><see cref="GetChildAsync(BrowseNode, string[])"/> of the node <paramref name="read"/> gives.</summary>
    public static async Task<BrowseNode> GetChildAsync(this Task<BrowseNode> read, params string[] path)
    {
        var node = await read;
        return await node.GetChildAsync(path);
    }

    /// <summary>The child labelled <paramref name="text"/>; the one the source declared when the
    /// compiler made up another of the same label, as it does for an event's field.</summary>
    private static async Task<BrowseNode> GetChildNamedAsync(BrowseNode node, string text)
    {
        var children = await node.ListChildrenAsync();
        var named = children
            .Where(c => c.Text == text)
            .ToList();
        if (named.Count > 1)
        {
            named = named.FindAll(IsSourceDeclared);
        }

        var child = named.SingleOrDefault();
        if (child is null)
        {
            throw new InvalidOperationException($"'{node.Text}' has no child '{text}': {string.Join(", ", children.Select(c => c.Text))}");
        }

        return child;
    }

    private static bool IsSourceDeclared(BrowseNode node)
    {
        return !node.IsCompilerGenerated;
    }

    /// <summary>What <paramref name="node"/> shows in <paramref name="language"/>.</summary>
    public static Task<NodeContent> ReadContentAsync(this BrowseNode node, DecompilationLanguage language = DecompilationLanguage.CSharp)
    {
        return NodeContentLoader.ReadContentAsync(TestSession.Shared, node, language, DecompilerOptions.Default, FirstGridRows).GetValueAsync();
    }

    public static async Task<NodeContent> ReadContentAsync(this Task<BrowseNode> read, DecompilationLanguage language = DecompilationLanguage.CSharp)
    {
        var node = await read;
        return await node.ReadContentAsync(language);
    }

    public static Task<DecompiledDocument> DecompileAsync(this BrowseNode node, DecompilationLanguage language, CancellationToken cancellationToken = default)
    {
        return NodeContentLoader.DecompileAsync(TestSession.Shared, node, language, DecompilerOptions.Default, cancellationToken).GetValueAsync();
    }

    public static Task<DecompiledDocument> DecompileAsync(this BrowseNode node, DecompilationLanguage language, DecompilerOptions options,
        CancellationToken cancellationToken = default)
    {
        return NodeContentLoader.DecompileAsync(TestSession.Shared, node, language, options, cancellationToken).GetValueAsync();
    }

    public static async Task<DecompiledDocument> DecompileAsync(this Task<BrowseNode> read, DecompilationLanguage language,
        CancellationToken cancellationToken = default)
    {
        var node = await read;
        return await node.DecompileAsync(language, cancellationToken);
    }

    public static async Task<DecompiledDocument> DecompileAsync(this Task<BrowseNode> read, DecompilationLanguage language, DecompilerOptions options,
        CancellationToken cancellationToken = default)
    {
        var node = await read;
        return await node.DecompileAsync(language, options, cancellationToken);
    }
}

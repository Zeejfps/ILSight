namespace ILSight.Core;

/// <summary>An element of an entity's documentation as read: a run of text, or an element with the attributes the
/// tooltip reads, its cref's target (named by the library), and its children.</summary>
internal sealed record DocNode
{
    public required string Name { get; init; }
    public required bool IsTextNode { get; init; }
    public required string TextContent { get; init; }
    public required IReadOnlyDictionary<string, string> Attributes { get; init; }
    public required DocCref? Cref { get; init; }
    public required IReadOnlyList<DocNode> Children { get; init; }

    public string? FindAttribute(string name)
    {
        return Attributes.GetValueOrDefault(name);
    }
}

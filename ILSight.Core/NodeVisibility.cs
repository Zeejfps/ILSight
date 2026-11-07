namespace ILSight.Core;

/// <summary>How visible what a node stands for is: its declared accessibility; whether it is visible
/// outside its assembly, taking enclosing types into account, as non-public nodes are usually shown
/// dimmed; and whether the compiler made it up rather than the source declaring it, such as a lambda's
/// closure class or a property's backing field, or it sits inside something the compiler made up, as
/// such nodes are usually hidden.</summary>
public readonly record struct NodeVisibility(NodeAccessibility Accessibility, bool IsPublicApi, bool IsCompilerGenerated)
{
    /// <summary>What a node that declares no accessibility of its own shows as: visible, and declared.</summary>
    internal static readonly NodeVisibility Open = new(NodeAccessibility.None, true, false);
}

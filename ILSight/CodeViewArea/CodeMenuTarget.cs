using ILSight.Core;

namespace ILSight.CodeViewArea;

/// <summary>What a code view's context menu was opened on: the reference under the pointer, whether
/// that is where it is defined, and the line, null off the text; one value, so that the view forgets it whole.</summary>
internal sealed record CodeMenuTarget
{
    public required CodeReference? Reference { get; init; }

    public required bool OnDefinition { get; init; }

    public required int? Line { get; init; }

    /// <summary>The type or member Analyze applies to here, as the key decides it at the caret; null where it isn't
    /// offered.</summary>
    public required CodeReference.Entity? Analyzable { get; init; }

    public static readonly CodeMenuTarget None = new CodeMenuTarget
    {
        Reference = null,
        OnDefinition = false,
        Line = null,
        Analyzable = null,
    };

    /// <summary>The reference to go to from here; null on its definition.</summary>
    public CodeReference.Navigable? Navigable => Reference is CodeReference.Navigable navigable && !OnDefinition ? navigable : null;

    /// <summary>The entity to analyze, find in the API reference or show in a diagram.</summary>
    public CodeReference.Entity? Entity => Reference as CodeReference.Entity;

    /// <summary>The reference to show among the metadata tables.</summary>
    public CodeReference.Navigable? Metadata => Reference as CodeReference.Navigable;
}

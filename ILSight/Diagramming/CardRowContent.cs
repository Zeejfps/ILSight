using ILSight.Core;

namespace ILSight.Diagramming;

/// <summary>What a row of a card shows, with what only that kind of row has.</summary>
public abstract record CardRowContent
{
    private CardRowContent()
    {
    }

    /// <summary>The icon and name of the card's type, which a click opens.</summary>
    public sealed record Name(NodeIcon Icon, NodeAccessibility Accessibility, CodeReference.Entity Type) : CardRowContent;

    /// <summary>Whether the type is abstract, static or an interface.</summary>
    public sealed record Stereotype : CardRowContent;

    /// <summary>The title of <see cref="Part"/>, which folds, with how many members it holds.</summary>
    public sealed record Fold(CardFold Part, bool IsOpen) : CardRowContent;

    /// <summary>A member's icon and label, which a click opens.</summary>
    public sealed record Member(NodeIcon Icon, NodeAccessibility Accessibility, CodeReference.Entity Reference) : CardRowContent;
}

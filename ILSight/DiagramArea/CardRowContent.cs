using ILSight.Core;

namespace ILSight.DiagramArea;

/// <summary>What a row of a card shows, with what only that kind of row has.</summary>
public union CardRowContent(CardRowContent.Name, CardRowContent.Stereotype, CardRowContent.Fold, CardRowContent.Member)
{
    /// <summary>The icon and name of the card's type, which a click opens.</summary>
    public sealed record Name
    {
        public required NodeIcon Icon { get; init; }

        public required NodeAccessibility Accessibility { get; init; }

        public required CodeReference.Entity Type { get; init; }
    }

    /// <summary>Whether the type is abstract, static or an interface.</summary>
    public sealed record Stereotype;

    /// <summary>The title of <see cref="Part"/>, which folds, with how many members it holds.</summary>
    public sealed record Fold
    {
        public required CardFold Part { get; init; }

        public required bool IsOpen { get; init; }
    }

    /// <summary>A member's icon and label, which a click opens.</summary>
    public sealed record Member
    {
        public required NodeIcon Icon { get; init; }

        public required NodeAccessibility Accessibility { get; init; }

        public required CodeReference.Entity Reference { get; init; }
    }
}

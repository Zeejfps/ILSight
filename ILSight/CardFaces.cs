using System.Diagnostics;
using ILSight.Core;
using ILSight.Core.Diagrams;
using ILSight.Localization;

namespace ILSight;

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

/// <summary>A row of a card at the diagram's own size: what it shows, how tall it is and how far in its
/// content starts, its text, where it starts below the card's top, and how wide its text is.</summary>
public readonly record struct CardRow(
    CardRowContent Content,
    float Height,
    float Indent,
    string Text,
    float Top = 0f,
    float TextWidth = 0f)
{
    public float Bottom => Top + Height;

    /// <summary>The type or member whose code a click on the row opens, or null for a row that opens none.</summary>
    public CodeReference.Entity? Target => Content switch
    {
        CardRowContent.Name name => name.Type,
        CardRowContent.Member member => member.Reference,
        CardRowContent.Stereotype or CardRowContent.Fold => null,
        _ => throw new UnreachableException(),
    };

    /// <summary>Whether a click on the row opens the code of what it shows.</summary>
    public bool IsLink => Target != null;
}

/// <summary>How a card is drawn at the diagram's own size: its rows from the top, how wide and tall it is,
/// and how far down its header reaches.</summary>
public sealed record CardFace(TypeCard Card, IReadOnlyList<CardRow> Rows, float Width, float Height, float HeaderHeight);

/// <summary>What a diagram's card faces are measured for: the cards, which parts of them are open, whether
/// members hidden from other assemblies show, the font and the language.</summary>
public readonly record struct FaceKey(int DiagramVersion, int FoldsVersion, bool ShowsHiddenMembers, float FontSize, string? FontFamily, Strings Strings);

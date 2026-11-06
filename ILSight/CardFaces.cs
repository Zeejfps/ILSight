using ILSight.Core;
using ILSight.Core.Diagrams;
using ILSight.Localization;

namespace ILSight;

/// <summary>What a row of a card shows.</summary>
public enum CardRowKind
{
    /// <summary>The type's icon and name.</summary>
    Name,

    /// <summary>Whether the type is abstract, static or an interface.</summary>
    Stereotype,

    /// <summary>The title of a part that folds, with how many members it holds.</summary>
    Fold,

    /// <summary>A member's icon and label.</summary>
    Member,
}

/// <summary>A row of a card at the diagram's own size: what it shows, how tall it is and how far in its
/// content starts, its text and icon, what it stands for: the type or member it opens, or the part it
/// folds, open or not; where it starts below the card's top, and how wide its text is.</summary>
public readonly record struct CardRow(
    CardRowKind Kind,
    float Height,
    float Indent,
    string Text,
    NodeIcon? Icon = null,
    NodeAccessibility Accessibility = NodeAccessibility.None,
    CodeReference.Entity? Target = null,
    CardFold? Fold = null,
    bool IsOpen = false,
    float Top = 0f,
    float TextWidth = 0f)
{
    public float Bottom => Top + Height;

    /// <summary>Whether a click on the row opens the code of what it shows.</summary>
    public bool IsLink => Target != null;
}

/// <summary>How a card is drawn at the diagram's own size: its rows from the top, how wide and tall it is,
/// and how far down its header reaches.</summary>
public sealed record CardFace(TypeCard Card, IReadOnlyList<CardRow> Rows, float Width, float Height, float HeaderHeight);

/// <summary>What a diagram's card faces are measured for: the cards, which parts of them are open, whether
/// members hidden from other assemblies show, the font and the language.</summary>
public readonly record struct FaceKey(int DiagramVersion, int FoldsVersion, bool ShowsHiddenMembers, float FontSize, string? FontFamily, Strings Strings);

using ILSight.Core;
using ILSight.Core.Diagrams;
using ILSight.DiagramViewport;

namespace ILSight.Diagramming;

/// <summary>How a tab shows its diagram: where it looks and how near, the card selected, which parts of the
/// cards are open, whether members hidden from other assemblies show, the layout it moves to, the cards'
/// faces as last measured, the versions of the diagram last measured and laid out, what the press held now
/// landed on, the card its menu was opened on, and the tooltip shown. The first layout is fitted to the
/// view, a card focused is brought into view once a layout of its diagram as it is now places it, and the
/// card selected stays where it is on the screen while the cards move to a new layout.</summary>
public sealed class DiagramViewState
{
    public DiagramCamera Camera { get; } = new();

    /// <summary>The parts of the cards folded or opened against how they start: a card's own members show
    /// until folded, what it inherits stays folded until opened.</summary>
    public TrackedSet<CardFold> FlippedFolds { get; } = new();

    /// <summary>Counts the changes to <see cref="FlippedFolds"/>.</summary>
    public int FoldsVersion => FlippedFolds.Revision;

    public LayoutTransition Transition { get; } = new();

    /// <summary>The faces of the diagram's cards as last measured.</summary>
    public Dictionary<CodeReference.Entity, CardFace> Faces { get; } = new();

    /// <summary>What <see cref="Faces"/> were measured for, to measure them again only when that changes.</summary>
    public FaceKey? MeasuredFor;

    /// <summary>What the diagram was last painted from.</summary>
    internal DiagramFrame Frame { get; } = new();

    /// <summary>The type whose card is selected, or null.</summary>
    public CodeReference.Entity? Selected;

    /// <summary>Whether members hidden from other assemblies show on the cards of types from assemblies
    /// other than the diagram's own, and how many times that changed.</summary>
    public Tracked<bool> HiddenMembersFilter { get; } = new(false);

    /// <summary>Whether members hidden from other assemblies show.</summary>
    public bool ShowsHiddenMembers => HiddenMembersFilter.Value;

    /// <summary>Counts the changes to what the settings keep of the view: where it looks and how near,
    /// the folds, and the filter.</summary>
    public int Changes => Camera.Changes + FoldsVersion + HiddenMembersFilter.Version;

    /// <summary>Set until the first layout is fitted to the view.</summary>
    public bool IsFitPending = true;

    /// <summary>The type whose card is brought into view once it is laid out, or null.</summary>
    public CodeReference.Entity? PendingFocus;

    /// <summary>The version of the diagram whose cards were last measured, and a layout asked for.</summary>
    public int MeasuredVersion = -1;

    /// <summary>The version of the diagram the layout drawn was asked for, -1 before the first.</summary>
    public int LaidOutVersion = -1;

    /// <summary>What the press held now landed on.</summary>
    public DiagramHit PressedOn = new DiagramHit.Nothing();

    /// <summary>Whether the press held now has moved far enough to pan.</summary>
    public bool PressMoved;

    /// <summary>The card the context menu was opened on.</summary>
    public TypeCard? MenuCard;

    internal KeptTooltip Tooltip { get; } = new();
}

using ZGF.Geometry;
using ZGF.Gui.Immediate;
using ILSight.Core;
using ILSight.Core.Diagrams;
using ILSight.DiagramCameraArea;

namespace ILSight.DiagramArea;

/// <summary>How a tab shows its diagram: where it looks and how near, the card selected, which parts of the
/// cards are open, whether members hidden from other assemblies show, the layout it moves to, the cards'
/// faces as last measured, the versions of the diagram last measured and laid out, what the press held now
/// landed on, the card its menu was opened on, and the tooltip shown. The first layout is fitted to the
/// view, a card focused is brought into view once a layout of its diagram as it is now places it, and the
/// card selected stays where it is on the screen while the cards move to a new layout.</summary>
public sealed class DiagramViewState
{
    private readonly Dictionary<CodeReference.Entity, CardFace> _faces = new();
    private int _measuredVersion = -1;

    public DiagramCamera Camera { get; } = new();

    /// <summary>The parts of the cards folded or opened against how they start: a card's own members show
    /// until folded, what it inherits stays folded until opened.</summary>
    public TrackedSet<CardFold> FlippedFolds { get; } = new();

    /// <summary>Counts the changes to <see cref="FlippedFolds"/>.</summary>
    public int FoldsVersion => FlippedFolds.Revision;

    public LayoutTransition Transition { get; } = new();

    /// <summary>The faces of the diagram's cards as last measured.</summary>
    public IReadOnlyDictionary<CodeReference.Entity, CardFace> Faces => _faces;

    /// <summary>What <see cref="Faces"/> were measured for, to measure them again only when that changes.</summary>
    public FaceKey? MeasuredFor { get; private set; }

    /// <summary>What the diagram was last painted from.</summary>
    internal DiagramFrame Frame { get; } = new();

    /// <summary>The type whose card is selected, or null.</summary>
    public CodeReference.Entity? Selected { get; private set; }

    /// <summary>Whether members hidden from other assemblies show on the cards of types from assemblies
    /// other than the diagram's own, and how many times that changed.</summary>
    public Tracked<bool> HiddenMembersFilter { get; } = new(false);

    /// <summary>Whether members hidden from other assemblies show.</summary>
    public bool ShowsHiddenMembers => HiddenMembersFilter.Value;

    /// <summary>Counts the changes to what the settings keep of the view: where it looks and how near,
    /// the folds, and the filter.</summary>
    public int Changes => Camera.Changes + FoldsVersion + HiddenMembersFilter.Version;

    /// <summary>Set until the first layout is fitted to the view.</summary>
    public bool IsFitPending { get; private set; } = true;

    /// <summary>The type whose card is brought into view once it is laid out, or null.</summary>
    public CodeReference.Entity? PendingFocus { get; private set; }

    /// <summary>The version of the diagram the layout drawn was asked for, -1 before the first.</summary>
    public int LaidOutVersion { get; private set; } = -1;

    /// <summary>What the press held now landed on.</summary>
    public DiagramHit PressedOn { get; private set; } = new DiagramHit.Nothing();

    /// <summary>Whether the press held now has moved far enough to pan.</summary>
    public bool PressMoved { get; private set; }

    /// <summary>The card the context menu was opened on.</summary>
    public TypeCard? MenuCard { get; private set; }

    internal KeptTooltip Tooltip { get; } = new();

    /// <summary>Selects the card of <paramref name="type"/>, or none.</summary>
    public void Select(CodeReference.Entity? type)
    {
        Selected = type;
    }

    /// <summary>Selects the card of <paramref name="type"/> and brings it into view once it is laid out.</summary>
    public void FocusCard(CodeReference.Entity type)
    {
        Selected = type;
        PendingFocus = type;
    }

    /// <summary>Returns the view to how <paramref name="saved"/> says it was, which the first layout then
    /// keeps rather than fitting itself to the view.</summary>
    public void RestoreView(DiagramViewRecord saved)
    {
        Camera.Restore(new PointF(saved.X, saved.Y), saved.Zoom);
        if (saved.Folds is not null)
        {
            FlippedFolds.Replace(saved.Folds);
        }

        HiddenMembersFilter.Value = saved.ShowsHiddenMembers;
        IsFitPending = false;
    }

    /// <summary>Keeps <paramref name="faces"/>, measured for <paramref name="key"/> from the diagram at
    /// <paramref name="version"/>, in place of those before.</summary>
    public void KeepFaces(FaceKey key, IReadOnlyList<CardFace> faces, int version)
    {
        _faces.Clear();
        foreach (var face in faces)
        {
            _faces[face.Card.Type] = face;
        }

        MeasuredFor = key;
        _measuredVersion = version;
    }

    /// <summary>Moves toward <paramref name="layout"/> when it is a newer one, taken as a layout of the diagram as
    /// it was last measured.</summary>
    public void FollowLayout(TypeLayout? layout)
    {
        if (!Transition.TryFollowLayout(layout))
        {
            return;
        }

        LaidOutVersion = _measuredVersion;
    }

    /// <summary>Fits <paramref name="layout"/> to <paramref name="rect"/> when it is the first that places cards.</summary>
    public void FitFirstLayout(TypeLayout layout, UiRect rect)
    {
        if (!IsFitPending)
        {
            return;
        }

        if (layout.Places.Count == 0)
        {
            return;
        }

        Camera.Fit(layout.Width, layout.Height, rect);
        IsFitPending = false;
    }

    /// <summary>Brings the card focused into <paramref name="rect"/> when <paramref name="layout"/> places it and it
    /// is not wholly shown, then lets the focus go.</summary>
    public void FocusPendingCard(TypeLayout layout, UiRect rect)
    {
        var type = PendingFocus;
        if (type == null)
        {
            return;
        }

        PendingFocus = null;
        if (!layout.Places.TryGetValue(type, out var place))
        {
            return;
        }

        var face = _faces.GetValueOrDefault(type);
        if (face is null)
        {
            return;
        }

        if (Camera.Shows(place, face.Width, face.Height, rect))
        {
            return;
        }

        Camera.CenterOn(new PointF(place.X + face.Width / 2, place.Y + face.Height / 2), rect);
    }

    /// <summary>Keeps what a press landed on, which has not moved yet.</summary>
    public void PressOn(DiagramHit hit)
    {
        PressedOn = hit;
        PressMoved = false;
    }

    /// <summary>Takes the press as moved once it went <paramref name="dx"/> by <paramref name="dy"/> from where it
    /// started, <paramref name="threshold"/> or further; once moved, it stays so.</summary>
    public void TrackPress(float dx, float dy, float threshold)
    {
        if (PressMoved)
        {
            return;
        }

        PressMoved = dx * dx + dy * dy >= threshold * threshold;
    }

    /// <summary>Selects <paramref name="card"/> and takes it as the one the context menu was opened on.</summary>
    public void OpenMenuOn(TypeCard card)
    {
        Selected = card.Type;
        MenuCard = card;
    }

    /// <summary>Forgets the card of <paramref name="type"/>, taken off the diagram: the menu's card, and the
    /// selection when it was that card.</summary>
    public void ForgetRemoved(CodeReference.Entity type)
    {
        MenuCard = null;
        if (Selected != type)
        {
            return;
        }

        Selected = null;
    }
}

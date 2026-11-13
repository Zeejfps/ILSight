using ZGF.Geometry;
using ZGF.Gui.Immediate;
using ILSight.Core;
using ILSight.Core.Diagrams;
using ILSight.DiagramCameraArea;

namespace ILSight.DiagramArea;

/// <summary>Diagrams of types, each in a tab of its own: showing a type on one, taking cards off it, and what the
/// diagram and its view hold. Showing a type, reopening a diagram and laying one out start the work they need,
/// returned for the edge to run (<see cref="DiagramGrowing"/>, <see cref="ReopenDiagramHandler"/>,
/// <see cref="LayOutDiagramHandler"/>), whose results are dropped when their diagram is closed meanwhile.</summary>
public static class Diagrams
{
    /// <summary>Whether <paramref name="fold"/> shows what it holds in <paramref name="view"/>.</summary>
    public static bool IsFoldOpen(DiagramViewState view, CardFold fold)
    {
        var flipped = view.FlippedFolds.Contains(fold);
        return fold.Match(flipped,
            own: static (flipped, _) => !flipped,
            inherited: static (flipped, _) => flipped);
    }

    /// <summary>Opens <paramref name="fold"/> when it is folded, and folds it when it is open.</summary>
    public static void ToggleFold(DiagramViewState view, CardFold fold)
    {
        view.FlippedFolds.Flip(fold);
    }

    /// <summary>Shows members hidden from other assemblies on every card when <paramref name="shows"/>.</summary>
    public static void ShowHiddenMembers(DiagramViewState view, bool shows)
    {
        view.HiddenMembersFilter.Value = shows;
    }

    /// <summary>Selects the card of <paramref name="type"/> and brings it into view once it is laid out.</summary>
    public static void FocusCard(DiagramViewState view, CodeReference.Entity type)
    {
        view.Selected = type;
        view.PendingFocus = type;
    }

    /// <summary>What the settings keep of the view.</summary>
    public static DiagramViewRecord SaveView(DiagramViewState view)
    {
        var corner = view.Camera.Corner;
        return new DiagramViewRecord
        {
            X = corner.X,
            Y = corner.Y,
            Zoom = view.Camera.Zoom,
            Folds = view.FlippedFolds.ToList(),
            ShowsHiddenMembers = view.ShowsHiddenMembers,
        };
    }

    /// <summary>Returns the view to how <paramref name="saved"/> says it was, which the first layout then
    /// keeps rather than fitting itself to the view.</summary>
    public static void RestoreView(DiagramViewState view, DiagramViewRecord saved)
    {
        view.Camera.Restore(new PointF(saved.X, saved.Y), saved.Zoom);
        if (saved.Folds is not null)
        {
            RestoreFolds(view, saved.Folds);
        }

        view.HiddenMembersFilter.Value = saved.ShowsHiddenMembers;
        view.IsFitPending = false;
    }

    /// <summary>Folds and opens the parts as <paramref name="flipped"/>, as the view saved them, says.</summary>
    public static void RestoreFolds(DiagramViewState view, IEnumerable<CardFold> flipped)
    {
        view.FlippedFolds.Replace(flipped);
    }

    /// <summary>Keeps <paramref name="faces"/>, measured for <paramref name="key"/>, in place of those before.</summary>
    public static void KeepFaces(DiagramViewState view, FaceKey key, IReadOnlyList<CardFace> faces)
    {
        view.Faces.Clear();
        foreach (var face in faces)
        {
            view.Faces[face.Card.Type] = face;
        }

        view.MeasuredFor = key;
    }

    /// <summary>The face of the card of <paramref name="type"/>, or null when none was measured.</summary>
    public static CardFace? FindFace(DiagramViewState view, CodeReference.Entity type)
    {
        return view.Faces.GetValueOrDefault(type);
    }

    /// <summary>The index of the row <paramref name="y"/> below the top of <paramref name="face"/> falls in, or null.</summary>
    public static int? FindRowAt(CardFace face, float y)
    {
        for (var i = 0; i < face.Rows.Count; i++)
        {
            if (y >= face.Rows[i].Top && y < face.Rows[i].Bottom)
            {
                return i;
            }
        }
        return null;
    }

    /// <summary>Shows the type <paramref name="type"/> points at, with the types of its assembly it is
    /// directly related to, on the diagram the user last worked in, brought to the front; on a new one
    /// when none is open, titled <paramref name="newTitle"/>. Returns the read of their cards it started, for the
    /// edge to start (<see cref="DiagramGrowing.AddWithNeighboursAsync"/>) on a token closing the diagram cancels.</summary>
    public static NeighboursRead ShowInDiagram(AppState appState, CodeReference.Entity type, string newTitle)
    {
        var tab = appState.Navigation.DiagramTab;
        if (tab is null || tab.Content is not TabContent.Diagram shown)
        {
            return ShowInNewDiagram(appState, type, newTitle);
        }
        appState.Navigation.ShowDiagramTab(tab);
        return StartReadingWithNeighbours(tab, shown.Types, type);
    }

    /// <summary>Shows the type <paramref name="type"/> points at, with the types of its assembly it is
    /// directly related to, on a new diagram: in the active tab while it shows nothing, else in a new tab. Returns
    /// the read of their cards it started, as <see cref="ShowInDiagram"/> does.</summary>
    public static NeighboursRead ShowInNewDiagram(AppState appState, CodeReference.Entity type, string title)
    {
        var diagram = new TypeDiagram();
        var tab = appState.Navigation.OpenDiagram(diagram, title);
        return StartReadingWithNeighbours(tab, diagram, type);
    }

    /// <summary>Counts the read of the card of <paramref name="type"/> and its neighbours, for
    /// <paramref name="diagram"/>, which <paramref name="tab"/> shows, as started.</summary>
    private static NeighboursRead StartReadingWithNeighbours(DocumentTab tab, TypeDiagram diagram, CodeReference.Entity type)
    {
        diagram.StartReading();
        return new NeighboursRead { Tab = tab, Diagram = diagram, Type = type };
    }

    /// <summary>Whether <paramref name="card"/> derives from or extends a type <paramref name="diagram"/>
    /// does not show.</summary>
    public static bool HasMissingBases(TypeDiagram diagram, TypeCard card)
    {
        return ListMissing(diagram, card, IsBaseLink).Count > 0;
    }

    /// <summary>Whether <paramref name="card"/> implements an interface <paramref name="diagram"/> does not show.</summary>
    public static bool HasMissingInterfaces(TypeDiagram diagram, TypeCard card)
    {
        return ListMissing(diagram, card, IsInterfaceLink).Count > 0;
    }

    /// <summary>Whether <paramref name="card"/>'s properties hold a type <paramref name="diagram"/> does not show.</summary>
    public static bool HasMissingHeld(TypeDiagram diagram, TypeCard card)
    {
        return ListMissing(diagram, card, IsHeldLink).Count > 0;
    }

    /// <summary>Whether <paramref name="link"/> leads to a type derived from or extended.</summary>
    public static bool IsBaseLink(TypeLink link)
    {
        return link is TypeLink.Inherits;
    }

    /// <summary>Whether <paramref name="link"/> leads to an interface implemented.</summary>
    public static bool IsInterfaceLink(TypeLink link)
    {
        return link is TypeLink.Implements;
    }

    /// <summary>Whether <paramref name="link"/> leads to a type held in a property.</summary>
    public static bool IsHeldLink(TypeLink link)
    {
        return link.IsHeld;
    }

    /// <summary>The types <paramref name="card"/> links to as <paramref name="isKind"/> picks that
    /// <paramref name="diagram"/> does not show, each once.</summary>
    public static List<CodeReference.Entity> ListMissing(TypeDiagram diagram, TypeCard card, Func<TypeLink, bool> isKind)
    {
        return card.Links
            .Where(isKind)
            .Select(static l => l.To)
            .Where(t => !diagram.HasCard(t))
            .Distinct()
            .ToList();
    }

    /// <summary>Shows the diagram <paramref name="saved"/> kept in the tab <paramref name="reopened"/> waits in, one
    /// of <paramref name="tabs"/> and of <paramref name="reopenedTabs"/>, the assemblies it names its own, and has the
    /// tab wait on for the cards of its types still found. Returns the read of those cards it started, for the edge to
    /// start (<see cref="ReopenDiagramHandler"/>) on a token closing the diagram cancels; null when the tab waits no
    /// more, as when it was pointed elsewhere.</summary>
    internal static SavedDiagramRead? ReopenDiagram(Navigation navigation, ReopenedTabs reopenedTabs, ReopenedTab reopened, DiagramDocumentRecord saved)
    {
        if (!reopened.IsWaiting)
        {
            return null;
        }

        var tab = reopened.Tab;
        var diagram = ShowSavedDiagram(navigation, tab, saved);
        var waiting = SavedDocuments.WaitFor(reopenedTabs, tab, saved);
        diagram.StartReading();
        return new SavedDiagramRead { Reopened = waiting, Diagram = diagram, Saved = saved };
    }

    private static TypeDiagram ShowSavedDiagram(Navigation navigation, DocumentTab tab, DiagramDocumentRecord saved)
    {
        var diagram = new TypeDiagram();
        diagram.TakeHomes(saved.HomeAssemblies);
        diagram.Keep(saved.Types);
        navigation.RestoreDiagram(tab, diagram, saved.Title ?? string.Empty);
        return diagram;
    }
}

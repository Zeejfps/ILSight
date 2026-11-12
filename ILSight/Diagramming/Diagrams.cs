using ZGF.Geometry;
using ZGF.Gui.Immediate;
using System.Diagnostics;
using ILSight.Core;
using ILSight.Core.Diagrams;
using ILSight.DiagramViewport;

namespace ILSight.Diagramming;

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
        return fold switch
        {
            CardFold.Own => !flipped,
            CardFold.Inherited => flipped,
            _ => throw new UnreachableException(),
        };
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
        return new DiagramViewRecord(corner.X, corner.Y, view.Camera.Zoom, view.FlippedFolds.ToList(), view.ShowsHiddenMembers);
    }

    /// <summary>Returns the view to how <paramref name="saved"/> says it was, which the first layout then
    /// keeps rather than fitting itself to the view.</summary>
    public static void RestoreView(DiagramViewState view, DiagramViewRecord saved)
    {
        DiagramCameras.Restore(view.Camera, new PointF(saved.X, saved.Y), saved.Zoom);
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
        appState.Navigation.ShowDiagramTab(tab, shown);
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
        StartReading(diagram);
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
        return link.Kind == TypeLinkKind.Inherits;
    }

    /// <summary>Whether <paramref name="link"/> leads to an interface implemented.</summary>
    public static bool IsInterfaceLink(TypeLink link)
    {
        return link.Kind == TypeLinkKind.Implements;
    }

    /// <summary>Whether <paramref name="link"/> leads to a type held in a property.</summary>
    public static bool IsHeldLink(TypeLink link)
    {
        return link.Kind is TypeLinkKind.HoldsOne or TypeLinkKind.HoldsMany;
    }

    /// <summary>The types <paramref name="card"/> links to as <paramref name="isKind"/> picks that
    /// <paramref name="diagram"/> does not show, each once.</summary>
    public static List<CodeReference.Entity> ListMissing(TypeDiagram diagram, TypeCard card, Func<TypeLink, bool> isKind)
    {
        return card.Links
            .Where(isKind)
            .Select(static l => l.To)
            .Where(t => !Diagrams.HasCard(diagram, t))
            .Distinct()
            .ToList();
    }

    /// <summary>Takes the card of <paramref name="type"/> off <paramref name="diagram"/>.</summary>
    public static void RemoveFromDiagram(AppState appState, TypeDiagram diagram, CodeReference.Entity type)
    {
        RemoveCard(diagram, type);
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
        StartReading(diagram);
        return new SavedDiagramRead { Reopened = waiting, Diagram = diagram, Saved = saved };
    }

    private static TypeDiagram ShowSavedDiagram(Navigation navigation, DocumentTab tab, DiagramDocumentRecord saved)
    {
        var diagram = new TypeDiagram();
        TakeHomes(diagram, saved.HomeAssemblies);
        navigation.RestoreDiagram(tab, diagram, saved.Title ?? string.Empty);
        return diagram;
    }

    private static void TakeHomes(TypeDiagram diagram, List<AssemblyPath>? assemblies)
    {
        if (assemblies is null)
        {
            return;
        }

        foreach (var assembly in assemblies)
        {
            Diagrams.TakeHome(diagram, assembly);
        }
    }

    /// <summary>Starts laying out <paramref name="diagram"/>, cancelling the layout in flight, whose result is then
    /// dropped. Returns the work the layout runs as, for the edge to start (<see cref="LayOutDiagramHandler"/>) on a
    /// token a newer layout and closing the diagram cancel; null when the diagram is closed.</summary>
    public static WorkTicket? StartLayout(TypeDiagram diagram)
    {
        if (diagram.IsClosed)
        {
            return null;
        }

        return diagram.Work.Start();
    }

    public static bool HasCard(TypeDiagram diagram, CodeReference.Entity type)
    {
        return diagram.Types.Contains(type);
    }

    /// <summary>The card of <paramref name="type"/>, or null when the diagram does not show it.</summary>
    public static TypeCard? FindCard(TypeDiagram diagram, CodeReference.Entity type)
    {
        foreach (var card in diagram.Cards)
        {
            if (card.Type == type)
            {
                return card;
            }
        }
        return null;
    }

    /// <summary>Adds the cards of <paramref name="cards"/> the diagram does not show yet, after the others.</summary>
    public static void AddCards(TypeDiagram diagram, IReadOnlyList<TypeCard> cards)
    {
        var shown = diagram.Cards.Count;
        foreach (var card in cards)
        {
            AddCardIfNew(diagram, card);
        }

        if (diagram.Cards.Count == shown)
        {
            return;
        }

        ChangeCards(diagram);
    }

    private static void AddCardIfNew(TypeDiagram diagram, TypeCard card)
    {
        if (!diagram.Types.Add(card.Type))
        {
            return;
        }

        diagram.Cards.Add(card);
    }

    public static void RemoveCard(TypeDiagram diagram, CodeReference.Entity type)
    {
        if (!diagram.Types.Remove(type))
        {
            return;
        }

        diagram.Cards.RemoveAll(c => c.Type == type);
        ChangeCards(diagram);
    }

    private static void ChangeCards(TypeDiagram diagram)
    {
        diagram.Version++;
        diagram.Links.Clear();
        foreach (var card in diagram.Cards)
        {
            AddLinksAmongCards(diagram, card);
        }
    }

    private static void AddLinksAmongCards(TypeDiagram diagram, TypeCard card)
    {
        foreach (var link in card.Links)
        {
            if (diagram.Types.Contains(link.To))
            {
                diagram.Links.Add(link);
            }
        }
    }

    /// <summary>Takes the assembly of <paramref name="card"/>, which the user put on the diagram, as one of
    /// its own, whose members show even when hidden from other assemblies.</summary>
    public static void TakeHome(TypeDiagram diagram, TypeCard card)
    {
        TakeHome(diagram, card.AssemblyPath);
    }

    /// <summary>Takes the assembly at <paramref name="assemblyPath"/> as one of the diagram's own.</summary>
    public static void TakeHome(TypeDiagram diagram, AssemblyPath assemblyPath)
    {
        diagram.HomeAssemblies.Add(assemblyPath);
    }

    /// <summary>Whether the assembly at <paramref name="assemblyPath"/> is one of the diagram's own; false
    /// for none.</summary>
    public static bool IsHome(TypeDiagram diagram, AssemblyPath? assemblyPath)
    {
        if (assemblyPath == null)
        {
            return false;
        }

        return diagram.HomeAssemblies.Contains(assemblyPath);
    }

    /// <summary>Counts a read of cards for the diagram as started, until <see cref="FinishReading"/>.</summary>
    public static void StartReading(TypeDiagram diagram)
    {
        diagram.Reading++;
    }

    public static void FinishReading(TypeDiagram diagram)
    {
        diagram.Reading--;
    }

    /// <summary>Holds <paramref name="layout"/>, which the newest layout finished with.</summary>
    public static void ShowLayout(TypeDiagram diagram, TypeLayout layout)
    {
        diagram.Layout = layout;
    }

    /// <summary>Starts moving to <paramref name="layout"/> when it is another than <see cref="LayoutTransition.Layout"/>;
    /// the first layout is drawn at once. True when it was another.</summary>
    public static bool TryFollowLayout(LayoutTransition transition, TypeLayout? layout)
    {
        if (ReferenceEquals(layout, transition.To))
        {
            return false;
        }

        transition.From = transition.To;
        transition.To = layout;
        transition.FromRoutes = ListRoutesByLink(transition.From);
        transition.Elapsed = GetTransitionStart(transition.From);
        return true;
    }

    private static Dictionary<TypeLink, LinkRoute> ListRoutesByLink(TypeLayout? layout)
    {
        var routes = new Dictionary<TypeLink, LinkRoute>();
        if (layout is null)
        {
            return routes;
        }

        foreach (var route in layout.Routes)
        {
            routes.TryAdd(route.Link, route);
        }

        return routes;
    }

    /// <summary>How far into the move a move from <paramref name="from"/> starts: at its end when there is
    /// nothing to move from.</summary>
    private static float GetTransitionStart(TypeLayout? from)
    {
        if (from is null)
        {
            return LayoutTransition.Seconds;
        }

        return 0f;
    }

    public static void AdvanceTransition(LayoutTransition transition, float seconds)
    {
        transition.Elapsed = MathF.Min(LayoutTransition.Seconds, transition.Elapsed + seconds);
    }

    /// <summary>Where the top-left of the card of <paramref name="type"/> is drawn now; null when the
    /// layout does not place it.</summary>
    public static PointF? FindCardPlace(LayoutTransition transition, CodeReference.Entity type)
    {
        if (transition.To is null)
        {
            return null;
        }

        if (!transition.To.Places.TryGetValue(type, out var to))
        {
            return null;
        }

        var from = FindOldCardPlace(transition, type);
        if (from is null)
        {
            return to;
        }

        return DiagramLayouts.Lerp(from.Value, to, transition.Progress);
    }

    private static PointF? FindOldCardPlace(LayoutTransition transition, CodeReference.Entity type)
    {
        if (!transition.IsMoving)
        {
            return null;
        }

        if (transition.From is null)
        {
            return null;
        }

        if (!transition.From.Places.TryGetValue(type, out var from))
        {
            return null;
        }

        return from;
    }

    /// <summary>How <paramref name="route"/>, one of the layout's, is drawn now: bent from the route its
    /// link had before while moving, else as it is.</summary>
    public static LinkRoute ShapeRoute(LayoutTransition transition, LinkRoute route)
    {
        if (!transition.IsMoving)
        {
            return route;
        }

        if (!transition.FromRoutes.TryGetValue(route.Link, out var from))
        {
            return route;
        }

        if (!CanBlendRoutes(from, route))
        {
            return route;
        }

        return BlendRoutes(from, route, transition.Progress);
    }

    private static bool CanBlendRoutes(LinkRoute from, LinkRoute to)
    {
        if (from.Pieces.Count == 0)
        {
            return false;
        }

        return to.Pieces.Count > 0;
    }

    private static LinkRoute BlendRoutes(LinkRoute from, LinkRoute to, float t)
    {
        var fromPieces = SubdivideRoute(from.Pieces, to.Pieces.Count);
        var toPieces = SubdivideRoute(to.Pieces, from.Pieces.Count);
        var pieces = new List<RoutePiece>(toPieces.Count);
        for (var i = 0; i < toPieces.Count; i++)
        {
            pieces.Add(DiagramLayouts.Blend(fromPieces[i], toPieces[i], t));
        }

        var tip = DiagramLayouts.Lerp(from.Tip, to.Tip, t);
        var labelCenter = BlendPoints(from.LabelCenter, to.LabelCenter, t);
        var sourceTip = BlendPoints(from.SourceTip, to.SourceTip, t);
        return to with { Pieces = pieces, Tip = tip, LabelCenter = labelCenter, SourceTip = sourceTip };
    }

    /// <summary><paramref name="pieces"/>, halved one after another, until there are at least
    /// <paramref name="count"/> of them.</summary>
    private static List<RoutePiece> SubdivideRoute(IReadOnlyList<RoutePiece> pieces, int count)
    {
        var split = pieces.ToList();
        var next = 0;
        while (split.Count < count)
        {
            var (first, second) = DiagramLayouts.Split(split[next]);
            split[next] = first;
            split.Insert(next + 1, second);
            next = (next + 2) % split.Count;
        }
        return split;
    }

    /// <summary>The point <paramref name="t"/> of the way between two a route may have: the one it moves
    /// to when it had none, and none when it moves to none.</summary>
    private static PointF? BlendPoints(PointF? from, PointF? to, float t)
    {
        if (from is null)
        {
            return to;
        }

        if (to is null)
        {
            return null;
        }

        return DiagramLayouts.Lerp(from.Value, to.Value, t);
    }
}

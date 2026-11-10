using ZGF.Geometry;
using ZGF.Gui.Immediate;
using System.Diagnostics;
using ILSight.Core;
using ILSight.Core.Diagrams;
using static ILSight.Navigation;

namespace ILSight;

/// <summary>Diagrams of types, each in a tab of its own: showing a type on one, growing it along the
/// types' relations, taking cards off it, and laying it out in the background. Cards are read in the
/// background too, and dropped when their diagram is closed meanwhile.</summary>
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
        view.HiddenMembersFilter.Set(shows);
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
        if (saved.Folds != null)
            RestoreFolds(view, saved.Folds);
        view.HiddenMembersFilter.Set(saved.ShowsHiddenMembers);
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
            view.Faces[face.Card.Type] = face;
        view.MeasuredFor = key;
    }

    /// <summary>The face of the card of <paramref name="type"/>, or null when none was measured.</summary>
    public static CardFace? FindFace(DiagramViewState view, CodeReference.Entity type)
    {
        return view.Faces.GetValueOrDefault(type);
    }

    /// <summary>The index of the row <paramref name="y"/> below the top of <paramref name="face"/> falls in, or -1.</summary>
    public static int FindRowAt(CardFace face, float y)
    {
        for (var i = 0; i < face.Rows.Count; i++)
        {
            if (y >= face.Rows[i].Top && y < face.Rows[i].Bottom)
                return i;
        }
        return -1;
    }

    /// <summary>Shows the type <paramref name="type"/> points at, with the types of its assembly it is
    /// directly related to, on the diagram the user last worked in, brought to the front; on a new one
    /// when none is open, titled <paramref name="newTitle"/>.</summary>
    public static void ShowInDiagram(AppState appState, CodeReference.Entity type, string newTitle)
    {
        var tab = appState.Tabs.Diagram;
        if (tab == null || tab.Content is not TabContent.Diagram shown)
        {
            ShowInNewDiagram(appState, type, newTitle);
            return;
        }
        BringToFront(appState, tab);
        RecordDiagram(appState.History, shown.Types, shown.Title);
        AddWithNeighbours(appState, tab, shown.Types, type);
    }

    private static void BringToFront(AppState appState, DocumentTab tab)
    {
        LeaveActiveTab(appState);
        Tabs.MakeActive(appState.Tabs, tab);
        appState.ViewCommands.Enqueue(new ViewCommand.ShowTab(tab, Beside: null));
    }

    /// <summary>Opens the code of <paramref name="target"/>, picked on <paramref name="diagram"/>, beside it,
    /// in a new tab when <paramref name="inNewTab"/>; going back returns to the diagram.</summary>
    public static void OpenFromDiagram(AppState appState, TypeDiagram diagram, CodeReference.Entity target, bool inNewTab)
    {
        var tab = FindTabShowing(appState, diagram);
        if (tab == null || tab.Content is not TabContent.Diagram shown)
            return;
        RecordDiagram(appState.History, shown.Types, shown.Title);
        Navigate(appState, target, inNewTab);
    }

    /// <summary>Shows the type <paramref name="type"/> points at, with the types of its assembly it is
    /// directly related to, on a new diagram: in the active tab while it shows nothing, else in a new tab.</summary>
    public static void ShowInNewDiagram(AppState appState, CodeReference.Entity type, string title)
    {
        LeaveActiveTab(appState);
        var tab = FindOrOpenEmptyTab(appState);
        var diagram = new TypeDiagram();
        Tabs.ShowDiagram(tab, title, diagram);
        Tabs.Keep(appState.Tabs, tab);
        Tabs.MakeActive(appState.Tabs, tab);
        RecordDiagram(appState.History, diagram, title);
        AddWithNeighbours(appState, tab, diagram, type);
    }

    /// <summary>Reads the card of <paramref name="type"/> and its neighbours in the background, adds them to
    /// <paramref name="diagram"/>, which <paramref name="tab"/> shows, taking the type's assembly as one of its
    /// own, then commands the view to focus the card.</summary>
    private static void AddWithNeighbours(AppState appState, DocumentTab tab, TypeDiagram diagram, CodeReference.Entity type)
    {
        Background.Start(appState.Work, nameof(AddWithNeighboursAsync), AddWithNeighboursAsync(appState, tab, diagram, type), problem => Errors.Report(appState, problem));
    }

    private static async Task AddWithNeighboursAsync(AppState appState, DocumentTab tab, TypeDiagram diagram, CodeReference.Entity type)
    {
        var token = diagram.Token;
        Diagrams.StartReading(diagram);
        var assemblies = appState.Assemblies;
        var read = await Background.Run(() => TypeCards.ReadWithNeighbours(assemblies, type, token));
        Diagrams.FinishReading(diagram);
        if (diagram.IsClosed)
            return;
        switch (read)
        {
            case Outcome<List<TypeCard>>.Ok ok:
                TakeHome(appState, tab, diagram, type, ok.Value);
                break;
            case Outcome<List<TypeCard>>.Failed failed:
                ReportUnreadable(appState, type, failed.Error);
                break;
            default:
                throw new UnreachableException();
        }
    }

    private static void TakeHome(AppState appState, DocumentTab tab, TypeDiagram diagram, CodeReference.Entity type, List<TypeCard> cards)
    {
        if (cards.Count == 0)
        {
            ReportUnreadable(appState, type, null);
            return;
        }
        Diagrams.TakeHome(diagram, cards[0]);
        Diagrams.AddCards(diagram, cards);
        appState.ViewCommands.Enqueue(new ViewCommand.FocusCard(tab, cards[0].Type));
    }

    private static void ReportUnreadable(AppState appState, CodeReference.Entity type, Exception? error)
    {
        Errors.Report(appState, new AppProblem.TypeNotDiagrammed(type, error));
    }

    /// <summary>Adds to <paramref name="diagram"/> the types <paramref name="card"/> derives from, or, for an
    /// interface, the interfaces it extends.</summary>
    public static void ExpandBases(AppState appState, TypeDiagram diagram, TypeCard card)
    {
        AddCards(appState, diagram, card, ListMissing(diagram, card, IsBaseLink));
    }

    /// <summary>Adds to <paramref name="diagram"/> the interfaces <paramref name="card"/> implements.</summary>
    public static void ExpandInterfaces(AppState appState, TypeDiagram diagram, TypeCard card)
    {
        AddCards(appState, diagram, card, ListMissing(diagram, card, IsInterfaceLink));
    }

    /// <summary>Adds to <paramref name="diagram"/> the types <paramref name="card"/>'s properties hold.</summary>
    public static void ExpandHeld(AppState appState, TypeDiagram diagram, TypeCard card)
    {
        AddCards(appState, diagram, card, ListMissing(diagram, card, IsHeldLink));
    }

    /// <summary>Whether <paramref name="card"/> derives from or extends a type <paramref name="diagram"/>
    /// does not show.</summary>
    public static bool HasMissingBases(TypeDiagram diagram, TypeCard card) => ListMissing(diagram, card, IsBaseLink).Count > 0;

    /// <summary>Whether <paramref name="card"/> implements an interface <paramref name="diagram"/> does not show.</summary>
    public static bool HasMissingInterfaces(TypeDiagram diagram, TypeCard card) => ListMissing(diagram, card, IsInterfaceLink).Count > 0;

    /// <summary>Whether <paramref name="card"/>'s properties hold a type <paramref name="diagram"/> does not show.</summary>
    public static bool HasMissingHeld(TypeDiagram diagram, TypeCard card) => ListMissing(diagram, card, IsHeldLink).Count > 0;

    /// <summary>Whether <paramref name="link"/> leads to a type derived from or extended.</summary>
    public static bool IsBaseLink(TypeLink link) => link.Kind == TypeLinkKind.Inherits;

    /// <summary>Whether <paramref name="link"/> leads to an interface implemented.</summary>
    public static bool IsInterfaceLink(TypeLink link) => link.Kind == TypeLinkKind.Implements;

    /// <summary>Whether <paramref name="link"/> leads to a type held in a property.</summary>
    public static bool IsHeldLink(TypeLink link) => link.Kind is TypeLinkKind.HoldsOne or TypeLinkKind.HoldsMany;

    /// <summary>The types <paramref name="card"/> links to as <paramref name="isKind"/> picks that
    /// <paramref name="diagram"/> does not show, each once.</summary>
    private static List<CodeReference.Entity> ListMissing(TypeDiagram diagram, TypeCard card, Func<TypeLink, bool> isKind)
    {
        return card.Links
            .Where(isKind)
            .Select(static l => l.To)
            .Where(t => !Diagrams.HasCard(diagram, t))
            .Distinct()
            .ToList();
    }

    /// <summary>Reads the cards of <paramref name="types"/>, which <paramref name="card"/> links to, in the
    /// background and adds them to <paramref name="diagram"/>.</summary>
    private static void AddCards(AppState appState, TypeDiagram diagram, TypeCard card, List<CodeReference.Entity> types)
    {
        Background.Start(appState.Work, nameof(AddCardsAsync), AddCardsAsync(appState, diagram, card, types), problem => Errors.Report(appState, problem));
    }

    private static async Task AddCardsAsync(AppState appState, TypeDiagram diagram, TypeCard card, List<CodeReference.Entity> types)
    {
        if (types.Count == 0)
            return;
        Diagrams.StartReading(diagram);
        var assemblies = appState.Assemblies;
        var read = await Background.Run(() => ReadCards(assemblies, types));
        FinishAdding(appState, diagram, card, read);
    }

    private static List<TypeCard> ReadCards(AssemblyList list, List<CodeReference.Entity> types)
    {
        return types
            .Select(t => TypeCards.Read(list, t))
            .OfType<TypeCard>()
            .ToList();
    }

    /// <summary>Searches the open assemblies in the background for the types deriving from
    /// <paramref name="card"/>, or implementing it, and adds them to <paramref name="diagram"/>.</summary>
    public static void ExpandDerived(AppState appState, TypeDiagram diagram, TypeCard card)
    {
        Background.Start(appState.Work, nameof(ExpandDerivedAsync), ExpandDerivedAsync(appState, diagram, card), problem => Errors.Report(appState, problem));
    }

    private static async Task ExpandDerivedAsync(AppState appState, TypeDiagram diagram, TypeCard card)
    {
        var token = diagram.Token;
        Diagrams.StartReading(diagram);
        var assemblies = appState.Assemblies;
        var type = card.Type;
        var read = await Background.Run(() => TypeCards.ReadDerived(assemblies, type, token));
        FinishAdding(appState, diagram, card, read);
    }

    /// <summary>Adds the cards a read of the types related to <paramref name="card"/> found to
    /// <paramref name="diagram"/>, unless it was closed meanwhile; reports a read that failed.</summary>
    private static void FinishAdding(AppState appState, TypeDiagram diagram, TypeCard card, Outcome<List<TypeCard>> read)
    {
        Diagrams.FinishReading(diagram);
        if (diagram.IsClosed)
            return;
        switch (read)
        {
            case Outcome<List<TypeCard>>.Ok ok:
                Diagrams.AddCards(diagram, ok.Value);
                break;
            case Outcome<List<TypeCard>>.Failed failed:
                ReportUnreadable(appState, card.Type, failed.Error);
                break;
            default:
                throw new UnreachableException();
        }
    }

    /// <summary>Takes the card of <paramref name="type"/> off <paramref name="diagram"/>.</summary>
    public static void RemoveFromDiagram(AppState appState, TypeDiagram diagram, CodeReference.Entity type)
    {
        RemoveCard(diagram, type);
    }

    /// <summary>Shows the diagram <paramref name="saved"/> kept in the tab <paramref name="reopened"/> waits in,
    /// the assemblies it names its own, and reads in the background the cards of its types still found,
    /// which it adds unless the tab was pointed elsewhere or the diagram closed meanwhile.</summary>
    internal static void ReopenDiagram(AppState appState, ReopenedTab reopened, DiagramDocumentRecord saved)
    {
        Background.Start(appState.Work, nameof(ReopenDiagramAsync), ReopenDiagramAsync(appState, reopened, saved), problem => Errors.Report(appState, problem));
    }

    private static async Task ReopenDiagramAsync(AppState appState, ReopenedTab reopened, DiagramDocumentRecord saved)
    {
        if (!reopened.IsWaiting)
            return;
        var tab = reopened.Tab;
        var diagram = ShowSavedDiagram(appState, tab, saved);
        var reading = SavedDocuments.WaitFor(appState.Reopened, tab, saved);
        Diagrams.StartReading(diagram);
        var assemblies = appState.Assemblies;
        var types = saved.Types;
        var read = await Background.Run(() => ReadCards(assemblies, types));
        Diagrams.FinishReading(diagram);
        if (!SavedDocuments.FinishWaiting(appState.Reopened, reading))
            return;
        if (diagram.IsClosed)
            return;
        AddSavedCards(appState, diagram, read, saved);
    }

    private static TypeDiagram ShowSavedDiagram(AppState appState, DocumentTab tab, DiagramDocumentRecord saved)
    {
        var diagram = new TypeDiagram();
        TakeHomes(diagram, saved.HomeAssemblies);
        var title = saved.Title ?? string.Empty;
        Tabs.ShowDiagram(tab, title, diagram);
        WorkInIfActive(appState, tab);
        return diagram;
    }

    private static void TakeHomes(TypeDiagram diagram, List<AssemblyPath>? assemblies)
    {
        if (assemblies == null)
            return;
        foreach (var assembly in assemblies)
            Diagrams.TakeHome(diagram, assembly);
    }

    /// <summary>Has the diagram <paramref name="tab"/> shows taken as the one the user works in, when the tab is the active one.</summary>
    private static void WorkInIfActive(AppState appState, DocumentTab tab)
    {
        if (tab != appState.Tabs.Active)
            return;
        Tabs.MakeActive(appState.Tabs, tab);
    }

    private static List<TypeCard> ReadCards(AssemblyList list, List<TypeAnchor> types)
    {
        return types
            .Select(t => TypeCards.Read(list, t))
            .OfType<TypeCard>()
            .ToList();
    }

    private static void AddSavedCards(AppState appState, TypeDiagram diagram, Outcome<List<TypeCard>> read, DiagramDocumentRecord saved)
    {
        switch (read)
        {
            case Outcome<List<TypeCard>>.Ok ok:
                Diagrams.AddCards(diagram, ok.Value);
                break;
            case Outcome<List<TypeCard>>.Failed failed:
                ReportUnreadableDiagram(appState, saved, failed.Error);
                break;
            default:
                throw new UnreachableException();
        }
    }

    private static void ReportUnreadableDiagram(AppState appState, DiagramDocumentRecord saved, Exception error)
    {
        Errors.Report(appState, new AppProblem.DiagramNotReopened(saved.Title ?? string.Empty, error));
    }

    /// <summary>Lays out <paramref name="diagram"/> in the background, its cards and labels as large as
    /// <paramref name="measures"/> says, cancelling the layout in flight, and holds the layout unless
    /// another was started meanwhile or the diagram was closed.</summary>
    public static void LayOutDiagram(AppState appState, TypeDiagram diagram, DiagramMeasures measures)
    {
        Background.Start(appState.Work, nameof(LayOutDiagramAsync), LayOutDiagramAsync(appState, diagram, measures), problem => Errors.Report(appState, problem));
    }

    private static async Task LayOutDiagramAsync(AppState appState, TypeDiagram diagram, DiagramMeasures measures)
    {
        if (diagram.IsClosed)
            return;
        var ticket = Diagrams.StartLayout(diagram);
        var laidOut = await Background.Run(() => DiagramLayouts.Arrange(measures, ticket.Token));
        if (!Diagrams.FinishLayout(diagram, ticket))
            return;
        switch (laidOut)
        {
            case Outcome<TypeLayout>.Ok ok:
                Diagrams.ShowLayout(diagram, ok.Value);
                break;
            case Outcome<TypeLayout>.Failed failed:
                ReportLayoutFailure(appState, failed.Error);
                break;
            default:
                throw new UnreachableException();
        }
    }

    private static void ReportLayoutFailure(AppState appState, Exception error)
    {
        Errors.Report(appState, new AppProblem.DiagramNotLaidOut(error));
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
                return card;
        }
        return null;
    }

    /// <summary>Adds the cards of <paramref name="cards"/> the diagram does not show yet, after the others.</summary>
    public static void AddCards(TypeDiagram diagram, IReadOnlyList<TypeCard> cards)
    {
        var shown = diagram.Cards.Count;
        foreach (var card in cards)
            AddCardIfNew(diagram, card);
        if (diagram.Cards.Count == shown)
            return;
        ChangeCards(diagram);
    }

    private static void AddCardIfNew(TypeDiagram diagram, TypeCard card)
    {
        if (!diagram.Types.Add(card.Type))
            return;
        diagram.Cards.Add(card);
    }

    public static void RemoveCard(TypeDiagram diagram, CodeReference.Entity type)
    {
        if (!diagram.Types.Remove(type))
            return;
        diagram.Cards.RemoveAll(c => c.Type == type);
        ChangeCards(diagram);
    }

    private static void ChangeCards(TypeDiagram diagram)
    {
        diagram.Version++;
        diagram.Links.Clear();
        foreach (var card in diagram.Cards)
            AddLinksAmongCards(diagram, card);
    }

    private static void AddLinksAmongCards(TypeDiagram diagram, TypeCard card)
    {
        foreach (var link in card.Links)
        {
            if (diagram.Types.Contains(link.To))
                diagram.Links.Add(link);
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
            return false;
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

    /// <summary>Starts a layout, cancelling the one in flight.</summary>
    public static LoadTicket StartLayout(TypeDiagram diagram)
    {
        diagram.LayoutWork?.Cancel();
        var work = CancellationTokenSource.CreateLinkedTokenSource(diagram.Open.Token);
        diagram.LayoutWork = work;
        return new LoadTicket { Work = work };
    }

    /// <summary>Ends the layout <paramref name="ticket"/> was given for. False when another started since,
    /// or the diagram was closed, and what it laid out should be dropped.</summary>
    public static bool FinishLayout(TypeDiagram diagram, LoadTicket ticket)
    {
        ticket.Work.Dispose();
        if (diagram.LayoutWork != ticket.Work)
            return false;
        diagram.LayoutWork = null;
        return !diagram.IsClosed;
    }

    /// <summary>Holds <paramref name="layout"/>, which the newest layout finished with.</summary>
    public static void ShowLayout(TypeDiagram diagram, TypeLayout layout)
    {
        diagram.Layout = layout;
    }

    /// <summary>Cancels what is in flight for the diagram, for good.</summary>
    public static void CloseDiagram(TypeDiagram diagram)
    {
        diagram.Open.Cancel();
        diagram.LayoutWork = null;
    }

    /// <summary>Starts moving to <paramref name="layout"/> when it is another than <see cref="LayoutTransition.Layout"/>;
    /// the first layout is drawn at once. True when it was another.</summary>
    public static bool FollowLayout(LayoutTransition transition, TypeLayout? layout)
    {
        if (ReferenceEquals(layout, transition.To))
            return false;
        transition.From = transition.To;
        transition.To = layout;
        transition.FromRoutes = ListRoutesByLink(transition.From);
        transition.Elapsed = GetTransitionStart(transition.From);
        return true;
    }

    private static Dictionary<TypeLink, LinkRoute> ListRoutesByLink(TypeLayout? layout)
    {
        var routes = new Dictionary<TypeLink, LinkRoute>();
        if (layout == null)
            return routes;
        foreach (var route in layout.Routes)
            routes.TryAdd(route.Link, route);
        return routes;
    }

    /// <summary>How far into the move a move from <paramref name="from"/> starts: at its end when there is
    /// nothing to move from.</summary>
    private static float GetTransitionStart(TypeLayout? from)
    {
        if (from == null)
            return LayoutTransition.Seconds;
        return 0f;
    }

    public static void AdvanceTransition(LayoutTransition transition, float seconds)
    {
        transition.Elapsed = MathF.Min(LayoutTransition.Seconds, transition.Elapsed + seconds);
    }

    /// <summary>Where the top-left of the card of <paramref name="type"/> is drawn now; null when the
    /// layout does not place it.</summary>
    public static PointF? GetCardPlace(LayoutTransition transition, CodeReference.Entity type)
    {
        if (transition.To == null)
            return null;
        if (!transition.To.Places.TryGetValue(type, out var to))
            return null;
        var from = FindOldCardPlace(transition, type);
        if (from == null)
            return to;
        return DiagramLayouts.Lerp(from.Value, to, transition.Progress);
    }

    private static PointF? FindOldCardPlace(LayoutTransition transition, CodeReference.Entity type)
    {
        if (!transition.IsMoving)
            return null;
        if (transition.From == null)
            return null;
        if (!transition.From.Places.TryGetValue(type, out var from))
            return null;
        return from;
    }

    /// <summary>How <paramref name="route"/>, one of the layout's, is drawn now: bent from the route its
    /// link had before while moving, else as it is.</summary>
    public static LinkRoute ShapeRoute(LayoutTransition transition, LinkRoute route)
    {
        if (!transition.IsMoving)
            return route;
        if (!transition.FromRoutes.TryGetValue(route.Link, out var from))
            return route;
        if (!CanBlendRoutes(from, route))
            return route;
        return BlendRoutes(from, route, transition.Progress);
    }

    private static bool CanBlendRoutes(LinkRoute from, LinkRoute to)
    {
        if (from.Pieces.Count == 0)
            return false;
        return to.Pieces.Count > 0;
    }

    private static LinkRoute BlendRoutes(LinkRoute from, LinkRoute to, float t)
    {
        var fromPieces = SubdivideRoute(from.Pieces, to.Pieces.Count);
        var toPieces = SubdivideRoute(to.Pieces, from.Pieces.Count);
        var pieces = new List<RoutePiece>(toPieces.Count);
        for (var i = 0; i < toPieces.Count; i++)
            pieces.Add(DiagramLayouts.Blend(fromPieces[i], toPieces[i], t));
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
        if (from == null)
            return to;
        if (to == null)
            return null;
        return DiagramLayouts.Lerp(from.Value, to.Value, t);
    }
}

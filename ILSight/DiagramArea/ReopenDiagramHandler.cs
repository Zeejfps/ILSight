using ILSight.Core;
using ILSight.Core.Diagrams;

namespace ILSight.DiagramArea;

/// <summary>Brings in the cards of a diagram made again: one reopened from the open list's saved documents
/// (<see cref="Diagrams.ReopenDiagram"/>), or one going back to it made again once no tab showed it
/// (<see cref="Navigation.GoToEntry"/>). Reads them through <see cref="IPorts"/> without holding up the screen, and
/// reports a diagram whose cards could not be read, and names each module that could not be read a card is in. The
/// diagram keeps the types whose cards couldn't be read (<see cref="TypeDiagram.Keep(IReadOnlyList{TypeAnchor})"/>), so that saving it again saves
/// them and its next reopening reads them again; it drops only those the open assemblies don't have.</summary>
public sealed class ReopenDiagramHandler(ReopenDiagramHandler.IPorts ports)
{
    public interface IPorts
    {
        /// <summary>Tells the user a diagram could not be reopened, or a module a card is in could not be read.</summary>
        void ReportError(AppProblem problem);

        /// <summary>The card of each of <paramref name="types"/>, in order: not found for one the open assemblies no
        /// longer have, unreadable for one in a module whose type system the library could not read; read without
        /// holding up the screen. A read that failed comes back as <see cref="Outcome{T}.Failed"/>; the cancellation
        /// of <paramref name="ct"/> is let through.</summary>
        Task<Outcome<List<Lookup<TypeCard>>>> ReadCardsAsync(IReadOnlyList<TypeAnchor> types, CancellationToken ct);
    }

    /// <summary>Brings in the cards <paramref name="read"/> started reading and adds them to its diagram, unless its
    /// tab stopped waiting among <paramref name="reopened"/>, as when it was pointed elsewhere, or the diagram was
    /// closed meanwhile. <paramref name="ct"/> is the workflow's token, which the edge that starts it links to the
    /// diagram's closing.</summary>
    public async Task HandleAsync(ReopenedTabs reopened, SavedDiagramRead read, CancellationToken ct)
    {
        var cards = await ports.ReadCardsAsync(read.Saved.Types, ct);
        read.Diagram.FinishReading(read.Read);
        if (!SavedDocuments.TryFinishWaiting(reopened, read.Reopened))
        {
            return;
        }

        TakeCards(read.Diagram, read.Saved.Title ?? string.Empty, read.Saved.Types, cards);
    }

    /// <summary>Brings in the cards <paramref name="read"/> started reading and adds them to its diagram, unless the
    /// diagram was closed meanwhile. <paramref name="ct"/> is the workflow's token, which the edge that starts it links
    /// to the diagram's closing.</summary>
    public async Task ShowPlaceAsync(DiagramPlaceRead read, CancellationToken ct)
    {
        var cards = await ports.ReadCardsAsync(read.Types, ct);
        read.Diagram.FinishReading(read.Read);
        TakeCards(read.Diagram, read.Title, read.Types, cards);
    }

    private void TakeCards(TypeDiagram diagram, string title, IReadOnlyList<TypeAnchor> types, Outcome<List<Lookup<TypeCard>>> cards)
    {
        if (diagram.IsClosed)
        {
            return;
        }

        cards.Switch(
            ok => AddFound(diagram, types, ok.Value),
            failed => ports.ReportError(new AppProblem.DiagramNotReopened { Diagram = title, Error = failed.Error }));
    }

    private void AddFound(TypeDiagram diagram, IReadOnlyList<TypeAnchor> types, List<Lookup<TypeCard>> cards)
    {
        diagram.AddCards(Lookup.ListFound(cards));
        diagram.Forget(ListNotFound(types, cards));
        foreach (var unreadable in Lookup.ListUnreadable(cards))
        {
            ports.ReportError(AppProblem.ModuleUnreadable.FromModule(unreadable));
        }
    }

    // The cards are the lookups of the types, in the same order.
    private static List<TypeAnchor> ListNotFound(IReadOnlyList<TypeAnchor> types, List<Lookup<TypeCard>> cards)
    {
        var notFound = new List<TypeAnchor>();
        for (var i = 0; i < cards.Count; i++)
        {
            if (cards[i] is Lookup<TypeCard>.NotFound)
            {
                notFound.Add(types[i]);
            }
        }
        return notFound;
    }
}

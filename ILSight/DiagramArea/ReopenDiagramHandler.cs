using ILSight.Core;
using ILSight.Core.Diagrams;

namespace ILSight.DiagramArea;

/// <summary>Brings in the cards of a diagram made again: one reopened from the open list's saved documents
/// (<see cref="Diagrams.ReopenDiagram"/>), or one going back to it made again once no tab showed it
/// (<see cref="Navigation.GoToEntry"/>). Reads them through <see cref="IPorts"/> without holding up the screen, and
/// reports a diagram whose cards could not be read.</summary>
public sealed class ReopenDiagramHandler(ReopenDiagramHandler.IPorts ports)
{
    public interface IPorts
    {
        /// <summary>Tells the user a diagram could not be reopened.</summary>
        void ReportError(AppProblem problem);

        /// <summary>The cards of those of <paramref name="types"/> still found in the open assemblies, read without
        /// holding up the screen. A read that failed comes back as <see cref="Outcome{T}.Failed"/>; the cancellation
        /// of <paramref name="ct"/> is let through.</summary>
        Task<Outcome<List<TypeCard>>> ReadCardsAsync(IReadOnlyList<TypeAnchor> types, CancellationToken ct);
    }

    /// <summary>Brings in the cards <paramref name="read"/> started reading and adds them to its diagram, unless its
    /// tab stopped waiting among <paramref name="reopened"/>, as when it was pointed elsewhere, or the diagram was
    /// closed meanwhile. <paramref name="ct"/> is the workflow's token, which the edge that starts it links to the
    /// diagram's closing.</summary>
    public async Task HandleAsync(ReopenedTabs reopened, SavedDiagramRead read, CancellationToken ct)
    {
        var cards = await ports.ReadCardsAsync(read.Saved.Types, ct);
        Diagrams.FinishReading(read.Diagram);
        if (!SavedDocuments.TryFinishWaiting(reopened, read.Reopened))
        {
            return;
        }

        TakeCards(read.Diagram, read.Saved.Title ?? string.Empty, cards);
    }

    /// <summary>Brings in the cards <paramref name="read"/> started reading and adds them to its diagram, unless the
    /// diagram was closed meanwhile. <paramref name="ct"/> is the workflow's token, which the edge that starts it links
    /// to the diagram's closing.</summary>
    public async Task ShowPlaceAsync(DiagramPlaceRead read, CancellationToken ct)
    {
        var cards = await ports.ReadCardsAsync(read.Types, ct);
        Diagrams.FinishReading(read.Diagram);
        TakeCards(read.Diagram, read.Title, cards);
    }

    /// <summary>Adds the cards read to <paramref name="diagram"/>, titled <paramref name="title"/>, unless it was closed
    /// meanwhile, or reports why they could not be read.</summary>
    private void TakeCards(TypeDiagram diagram, string title, Outcome<List<TypeCard>> cards)
    {
        if (diagram.IsClosed)
        {
            return;
        }

        cards.Switch(
            ok => Diagrams.AddCards(diagram, ok.Value),
            failed => ports.ReportError(new AppProblem.DiagramNotReopened { Diagram = title, Error = failed.Error }));
    }
}

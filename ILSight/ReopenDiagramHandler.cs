using System.Diagnostics;
using ILSight.Core;
using ILSight.Core.Diagrams;

namespace ILSight;

/// <summary>Brings in the cards of a diagram reopened from the open list's saved documents
/// (<see cref="Diagrams.ReopenDiagram"/>), reading them through <see cref="IPorts"/> without holding up the screen,
/// and reports a diagram whose cards could not be read.</summary>
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
        var diagram = read.Diagram;
        Diagrams.FinishReading(diagram);
        if (!SavedDocuments.TryFinishWaiting(reopened, read.Reopened))
        {
            return;
        }

        if (diagram.IsClosed)
        {
            return;
        }

        switch (cards)
        {
            case Outcome<List<TypeCard>>.Ok ok:
                Diagrams.AddCards(diagram, ok.Value);
                break;
            case Outcome<List<TypeCard>>.Failed failed:
                ports.ReportError(new AppProblem.DiagramNotReopened(read.Saved.Title ?? string.Empty, failed.Error));
                break;
            default:
                throw new UnreachableException();
        }
    }
}

using System.Diagnostics;
using ILSight.Core;
using ILSight.Core.Diagrams;

namespace ILSight.Diagramming;

/// <summary>Growing diagrams by the cards of types: a type shown with the types of its assembly it is directly related
/// to, and the types a card links to. The cards are read through <see cref="IPorts"/>, without holding up the screen,
/// and dropped when their diagram was closed meanwhile; a type that cannot be read is reported.</summary>
public sealed class DiagramGrowing(DiagramGrowing.IPorts ports)
{
    public interface IPorts
    {
        /// <summary>Tells the user a type could not be shown on a diagram.</summary>
        void ReportError(AppProblem problem);

        /// <summary>The card of <paramref name="type"/>, first, and those of the types of its assembly it is directly
        /// related to, read without holding up the screen; empty when it is no type that can be read. A read that
        /// failed comes back as <see cref="Outcome{T}.Failed"/>; the cancellation of <paramref name="ct"/> is let
        /// through.</summary>
        Task<Outcome<List<TypeCard>>> ReadWithNeighboursAsync(CodeReference.Entity type, CancellationToken ct);

        /// <summary>The cards of those of <paramref name="types"/> that can be read, as
        /// <see cref="ReadWithNeighboursAsync"/> reads them.</summary>
        Task<Outcome<List<TypeCard>>> ReadCardsAsync(IReadOnlyList<CodeReference.Entity> types, CancellationToken ct);

        /// <summary>The cards of the types of the open assemblies deriving from <paramref name="type"/>, or
        /// implementing it, as <see cref="ReadWithNeighboursAsync"/> reads them.</summary>
        Task<Outcome<List<TypeCard>>> ReadDerivedAsync(CodeReference.Entity type, CancellationToken ct);
    }

    /// <summary>Brings in the cards <paramref name="read"/> started reading and adds them to its diagram, taking the
    /// type's assembly as one of the diagram's own, then commands the view, through <paramref name="viewCommands"/>,
    /// to focus the type's card; drops them when the diagram was closed meanwhile. Reports a type that cannot be
    /// read. <paramref name="ct"/> is the workflow's token, which the edge that starts it links to the diagram's
    /// closing.</summary>
    public async Task AddWithNeighboursAsync(Queue<ViewCommand> viewCommands, NeighboursRead read, CancellationToken ct)
    {
        var cards = await ports.ReadWithNeighboursAsync(read.Type, ct);
        var diagram = read.Diagram;
        Diagrams.FinishReading(diagram);
        if (diagram.IsClosed)
        {
            return;
        }

        switch (cards)
        {
            case Outcome<List<TypeCard>>.Ok ok:
                TakeHome(viewCommands, read, ok.Value);
                break;
            case Outcome<List<TypeCard>>.Failed failed:
                ReportUnreadable(read.Type, failed.Error);
                break;
            default:
                throw new UnreachableException();
        }
    }

    private void TakeHome(Queue<ViewCommand> viewCommands, NeighboursRead read, List<TypeCard> cards)
    {
        if (cards.Count == 0)
        {
            ReportUnreadable(read.Type, null);
            return;
        }

        Diagrams.TakeHome(read.Diagram, cards[0]);
        Diagrams.AddCards(read.Diagram, cards);
        viewCommands.Enqueue(new ViewCommand.FocusCard(read.Tab, cards[0].Type));
    }

    /// <summary>Adds to <paramref name="diagram"/> the types <paramref name="card"/> derives from, or, for an
    /// interface, the interfaces it extends; drops them when the diagram was closed meanwhile.
    /// <paramref name="ct"/> is the workflow's token, which the edge that starts it links to the diagram's
    /// closing.</summary>
    public Task ExpandBasesAsync(TypeDiagram diagram, TypeCard card, CancellationToken ct)
    {
        return AddCardsAsync(diagram, card, Diagrams.ListMissing(diagram, card, Diagrams.IsBaseLink), ct);
    }

    /// <summary>Adds to <paramref name="diagram"/> the interfaces <paramref name="card"/> implements, as
    /// <see cref="ExpandBasesAsync"/> adds its bases.</summary>
    public Task ExpandInterfacesAsync(TypeDiagram diagram, TypeCard card, CancellationToken ct)
    {
        return AddCardsAsync(diagram, card, Diagrams.ListMissing(diagram, card, Diagrams.IsInterfaceLink), ct);
    }

    /// <summary>Adds to <paramref name="diagram"/> the types <paramref name="card"/>'s properties hold, as
    /// <see cref="ExpandBasesAsync"/> adds its bases.</summary>
    public Task ExpandHeldAsync(TypeDiagram diagram, TypeCard card, CancellationToken ct)
    {
        return AddCardsAsync(diagram, card, Diagrams.ListMissing(diagram, card, Diagrams.IsHeldLink), ct);
    }

    /// <summary>Reads the cards of <paramref name="types"/>, which <paramref name="card"/> links to, and adds them
    /// to <paramref name="diagram"/>; nothing when there are none.</summary>
    private async Task AddCardsAsync(TypeDiagram diagram, TypeCard card, List<CodeReference.Entity> types, CancellationToken ct)
    {
        if (types.Count == 0)
        {
            return;
        }

        Diagrams.StartReading(diagram);
        var read = await ports.ReadCardsAsync(types, ct);
        FinishAdding(diagram, card, read);
    }

    /// <summary>Looks in the open assemblies for the types deriving from <paramref name="card"/>, or implementing it,
    /// and adds them to <paramref name="diagram"/>, as <see cref="ExpandBasesAsync"/> adds its bases.</summary>
    public async Task ExpandDerivedAsync(TypeDiagram diagram, TypeCard card, CancellationToken ct)
    {
        Diagrams.StartReading(diagram);
        var read = await ports.ReadDerivedAsync(card.Type, ct);
        FinishAdding(diagram, card, read);
    }

    /// <summary>Adds the cards a read of the types related to <paramref name="card"/> found to
    /// <paramref name="diagram"/>, unless it was closed meanwhile; reports a read that failed.</summary>
    private void FinishAdding(TypeDiagram diagram, TypeCard card, Outcome<List<TypeCard>> read)
    {
        Diagrams.FinishReading(diagram);
        if (diagram.IsClosed)
        {
            return;
        }

        switch (read)
        {
            case Outcome<List<TypeCard>>.Ok ok:
                Diagrams.AddCards(diagram, ok.Value);
                break;
            case Outcome<List<TypeCard>>.Failed failed:
                ReportUnreadable(card.Type, failed.Error);
                break;
            default:
                throw new UnreachableException();
        }
    }

    private void ReportUnreadable(CodeReference.Entity type, Exception? error)
    {
        ports.ReportError(new AppProblem.TypeNotDiagrammed(type, error));
    }
}

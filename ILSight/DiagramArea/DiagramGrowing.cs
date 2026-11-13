using ILSight.Core;
using ILSight.Core.Diagrams;

namespace ILSight.DiagramArea;

/// <summary>Growing diagrams by the cards of types: a type shown with the types of its assembly it is directly related
/// to, and the types a card links to. The cards are read through <see cref="IPorts"/>, without holding up the screen,
/// and dropped when their diagram was closed meanwhile; a type that cannot be read is reported, and so is a module
/// that could not be read a type is in, or that the search for a type's derived types reached.</summary>
public sealed class DiagramGrowing(DiagramGrowing.IPorts ports)
{
    public interface IPorts
    {
        /// <summary>Tells the user a type could not be shown on a diagram, or is in a module that could not be
        /// read.</summary>
        void ReportError(AppProblem problem);

        /// <summary>The card of <paramref name="type"/>, first, and those of the types of its assembly it is directly
        /// related to, read without holding up the screen; empty when it is no type that can be read; the module, with
        /// why, when it is in one whose type system the library could not read. A read that failed comes back as
        /// <see cref="Outcome{T}.Failed"/>; the cancellation of <paramref name="ct"/> is let through.</summary>
        Task<Outcome<TypeSystemWork<List<TypeCard>>>> ReadWithNeighboursAsync(CodeReference.Entity type, CancellationToken ct);

        /// <summary>The card of each of <paramref name="types"/>, in order: not found for one that is no type that can
        /// be read, unreadable for one in a module whose type system the library could not read; read as
        /// <see cref="ReadWithNeighboursAsync"/> reads them.</summary>
        Task<Outcome<List<Lookup<TypeCard>>>> ReadCardsAsync(IReadOnlyList<CodeReference.Entity> types, CancellationToken ct);

        /// <summary>The cards of the types of the open assemblies deriving from <paramref name="type"/>, or
        /// implementing it, as <see cref="ReadWithNeighboursAsync"/> reads them, beside each module the search reached
        /// that the library could not read.</summary>
        Task<Outcome<ScopeSearch<TypeCard>>> ReadDerivedAsync(CodeReference.Entity type, CancellationToken ct);
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
        diagram.FinishReading(read.Read);
        if (diagram.IsClosed)
        {
            return;
        }

        cards.Switch(
            ok => ok.Value.Switch(
                done => TakeHome(viewCommands, read, done.Value),
                unreadable => ReportModuleUnreadable(unreadable.Module),
                static _ => { }),
            failed => ReportUnreadable(read.Type, failed.Error));
    }

    private void TakeHome(Queue<ViewCommand> viewCommands, NeighboursRead read, List<TypeCard> cards)
    {
        if (cards.Count == 0)
        {
            ReportUnreadable(read.Type, null);
            return;
        }

        read.Diagram.TakeHome(cards[0].AssemblyPath);
        read.Diagram.AddCards(cards);
        viewCommands.Enqueue(new ViewCommand.FocusCard { Tab = read.Tab, Type = cards[0].Type });
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

    private async Task AddCardsAsync(TypeDiagram diagram, TypeCard card, List<CodeReference.Entity> types, CancellationToken ct)
    {
        if (types.Count == 0)
        {
            return;
        }

        var reading = diagram.StartReading();
        var read = await ports.ReadCardsAsync(types, ct);
        FinishAdding(diagram, reading, card, read);
    }

    /// <summary>Looks in the open assemblies for the types deriving from <paramref name="card"/>, or implementing it,
    /// and adds them to <paramref name="diagram"/>, as <see cref="ExpandBasesAsync"/> adds its bases, naming each module
    /// the search reached that could not be read.</summary>
    public async Task ExpandDerivedAsync(TypeDiagram diagram, TypeCard card, CancellationToken ct)
    {
        var reading = diagram.StartReading();
        var read = await ports.ReadDerivedAsync(card.Type, ct);
        FinishAdding(diagram, reading, card, read);
    }

    private void FinishAdding(TypeDiagram diagram, CardRead reading, TypeCard card, Outcome<ScopeSearch<TypeCard>> read)
    {
        diagram.FinishReading(reading);
        if (diagram.IsClosed)
        {
            return;
        }

        read.Switch(
            ok => AddFound(diagram, ok.Value),
            failed => ReportUnreadable(card.Type, failed.Error));
    }

    private void AddFound(TypeDiagram diagram, ScopeSearch<TypeCard> search)
    {
        diagram.AddCards(search.Found);
        foreach (var unreadable in search.Unreadable)
        {
            ReportModuleUnreadable(unreadable);
        }
    }

    private void FinishAdding(TypeDiagram diagram, CardRead reading, TypeCard card, Outcome<List<Lookup<TypeCard>>> read)
    {
        diagram.FinishReading(reading);
        if (diagram.IsClosed)
        {
            return;
        }

        read.Switch(
            ok => AddFound(diagram, ok.Value),
            failed => ReportUnreadable(card.Type, failed.Error));
    }

    private void AddFound(TypeDiagram diagram, List<Lookup<TypeCard>> cards)
    {
        diagram.AddCards(Lookup.ListFound(cards));
        foreach (var unreadable in Lookup.ListUnreadable(cards))
        {
            ReportModuleUnreadable(unreadable);
        }
    }

    private void ReportUnreadable(CodeReference.Entity type, Exception? error)
    {
        ports.ReportError(new AppProblem.TypeNotDiagrammed { Type = type, Error = error });
    }

    private void ReportModuleUnreadable(UnreadableModule unreadable)
    {
        ports.ReportError(AppProblem.ModuleUnreadable.FromModule(unreadable));
    }
}

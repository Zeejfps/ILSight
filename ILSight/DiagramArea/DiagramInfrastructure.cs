using ILSight.Core;
using ILSight.Core.Diagrams;

namespace ILSight.DiagramArea;

/// <summary>Reaches the app's error reports, and the open assemblies and the layout off the UI thread, for
/// <see cref="DiagramGrowing"/>, <see cref="ReopenDiagramHandler"/> and <see cref="LayOutDiagramHandler"/>. Each read
/// runs on the thread pool and comes back on the UI thread, a read the library failed as
/// <see cref="Outcome{T}.Failed"/>, as the core answers it; a layout fails in no way it expects. The cancellation of
/// the token the call was given, and anything else, are let through.</summary>
public sealed class DiagramInfrastructure(ErrorLog errors, AssemblyList assemblies)
    : DiagramGrowing.IPorts, ReopenDiagramHandler.IPorts, LayOutDiagramHandler.IPorts
{
    public void ReportError(AppProblem problem)
    {
        Errors.Report(errors, problem);
    }

    public async Task<Outcome<TypeSystemWork<List<TypeCard>>>> ReadWithNeighboursAsync(CodeReference.Entity type, CancellationToken ct)
    {
        var read = await Background.Wait(Task.Run(() => TypeCards.ReadWithNeighboursAsync(assemblies, type, ct), ct));
        ct.ThrowIfCancellationRequested();
        return read;
    }

    /// <summary>Reads on the thread pool; the first card the library failed to read is the answer's
    /// <see cref="Outcome{T}.Failed"/>.</summary>
    public async Task<Outcome<List<Lookup<TypeCard>>>> ReadCardsAsync(IReadOnlyList<CodeReference.Entity> types, CancellationToken ct)
    {
        var read = await Background.Wait(Task.Run(() => ReadEachCardAsync(types, ct), ct));
        ct.ThrowIfCancellationRequested();
        return read;
    }

    /// <summary>Reads on the thread pool once the reads of the type systems the search can reach ended.</summary>
    public async Task<Outcome<ScopeSearch<TypeCard>>> ReadDerivedAsync(CodeReference.Entity type, CancellationToken ct)
    {
        var read = await Background.Wait(Task.Run(() => TypeCards.ReadDerivedAsync(assemblies, type, ct), ct));
        ct.ThrowIfCancellationRequested();
        return read;
    }

    /// <inheritdoc cref="ReadCardsAsync(IReadOnlyList{CodeReference.Entity}, CancellationToken)"/>
    public async Task<Outcome<List<Lookup<TypeCard>>>> ReadCardsAsync(IReadOnlyList<TypeAnchor> types, CancellationToken ct)
    {
        var read = await Background.Wait(Task.Run(() => ReadEachCardAsync(types, ct), ct));
        ct.ThrowIfCancellationRequested();
        return read;
    }

    public async Task<Outcome<TypeLayout>> ArrangeAsync(DiagramMeasures measures, CancellationToken ct)
    {
        var laidOut = await Background.Wait(Task.Run(() => DiagramLayouts.Arrange(measures, ct), ct));
        ct.ThrowIfCancellationRequested();
        return new Outcome<TypeLayout>.Ok(laidOut);
    }

    private async Task<Outcome<List<Lookup<TypeCard>>>> ReadEachCardAsync(IReadOnlyList<CodeReference.Entity> types, CancellationToken ct)
    {
        var cards = new List<Lookup<TypeCard>>();
        foreach (var type in types)
        {
            var card = await TypeCards.ReadAsync(assemblies, type, ct);
            if (card is Outcome<Lookup<TypeCard>>.Failed failed)
            {
                return new Outcome<List<Lookup<TypeCard>>>.Failed(failed.Error);
            }

            cards.Add(((Outcome<Lookup<TypeCard>>.Ok)card).Value);
        }
        return new Outcome<List<Lookup<TypeCard>>>.Ok(cards);
    }

    private async Task<Outcome<List<Lookup<TypeCard>>>> ReadEachCardAsync(IReadOnlyList<TypeAnchor> types, CancellationToken ct)
    {
        var cards = new List<Lookup<TypeCard>>();
        foreach (var type in types)
        {
            var card = await TypeCards.ReadAsync(assemblies, type, ct);
            if (card is Outcome<Lookup<TypeCard>>.Failed failed)
            {
                return new Outcome<List<Lookup<TypeCard>>>.Failed(failed.Error);
            }

            cards.Add(((Outcome<Lookup<TypeCard>>.Ok)card).Value);
        }
        return new Outcome<List<Lookup<TypeCard>>>.Ok(cards);
    }
}

using ILSight.Core;
using ILSight.Core.Diagrams;

namespace ILSight.DiagramArea;

/// <summary>Reaches the app's error reports, and the open assemblies and the layout off the UI thread, for
/// <see cref="DiagramGrowing"/>, <see cref="ReopenDiagramHandler"/> and <see cref="LayOutDiagramHandler"/>. Each read
/// runs on the thread pool and comes back on the UI thread, a file it could not read as <see cref="Outcome{T}.Failed"/>
/// (<see cref="UntrustedReads"/>); a layout fails in no way it expects. The cancellation of the token the call was
/// given, and anything else, are let through.</summary>
public sealed class DiagramInfrastructure(ErrorLog errors, AssemblyList assemblies)
    : DiagramGrowing.IPorts, ReopenDiagramHandler.IPorts, LayOutDiagramHandler.IPorts
{
    public void ReportError(AppProblem problem)
    {
        Errors.Report(errors, problem);
    }

    public Task<Outcome<TypeSystemWork<List<TypeCard>>>> ReadWithNeighboursAsync(CodeReference.Entity type, CancellationToken ct)
    {
        return UntrustedReads.CatchAsync(Background.Wait(Task.Run(() => TypeCards.ReadWithNeighboursAsync(assemblies, type, ct), ct)), ct);
    }

    public Task<Outcome<List<Lookup<TypeCard>>>> ReadCardsAsync(IReadOnlyList<CodeReference.Entity> types, CancellationToken ct)
    {
        return UntrustedReads.CatchAsync(Background.Wait(Task.Run(() => ReadEachCardAsync(types, ct), ct)), ct);
    }

    /// <summary>Reads on the thread pool once the reads of the type systems the search can reach ended.</summary>
    public Task<Outcome<ScopeSearch<TypeCard>>> ReadDerivedAsync(CodeReference.Entity type, CancellationToken ct)
    {
        return UntrustedReads.CatchAsync(Background.Wait(Task.Run(() => TypeCards.ReadDerivedAsync(assemblies, type, ct), ct)), ct);
    }

    public Task<Outcome<List<Lookup<TypeCard>>>> ReadCardsAsync(IReadOnlyList<TypeAnchor> types, CancellationToken ct)
    {
        return UntrustedReads.CatchAsync(Background.Wait(Task.Run(() => ReadEachCardAsync(types, ct), ct)), ct);
    }

    public async Task<Outcome<TypeLayout>> ArrangeAsync(DiagramMeasures measures, CancellationToken ct)
    {
        var laidOut = await Background.Wait(Task.Run(() => DiagramLayouts.Arrange(measures, ct), ct));
        ct.ThrowIfCancellationRequested();
        return new Outcome<TypeLayout>.Ok(laidOut);
    }

    private async Task<List<Lookup<TypeCard>>> ReadEachCardAsync(IReadOnlyList<CodeReference.Entity> types, CancellationToken ct)
    {
        var cards = new List<Lookup<TypeCard>>();
        foreach (var type in types)
        {
            cards.Add(await TypeCards.ReadAsync(assemblies, type, ct));
        }
        return cards;
    }

    private async Task<List<Lookup<TypeCard>>> ReadEachCardAsync(IReadOnlyList<TypeAnchor> types, CancellationToken ct)
    {
        var cards = new List<Lookup<TypeCard>>();
        foreach (var type in types)
        {
            cards.Add(await TypeCards.ReadAsync(assemblies, type, ct));
        }
        return cards;
    }
}

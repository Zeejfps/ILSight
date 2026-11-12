using ILSight.Core;
using ILSight.Core.Diagrams;

namespace ILSight.Diagramming;

/// <summary>Reaches the app's error reports, and the open assemblies and the layout off the UI thread, for
/// <see cref="DiagramGrowing"/>, <see cref="ReopenDiagramHandler"/> and <see cref="LayOutDiagramHandler"/>. Each read
/// runs on the thread pool and comes back on the UI thread, with whatever it threw as
/// <see cref="Outcome{T}.Failed"/>, except the cancellation of the token the call was given, which it lets
/// through.</summary>
public sealed class DiagramInfrastructure(ErrorLog errors, AssemblyList assemblies)
    : DiagramGrowing.IPorts, ReopenDiagramHandler.IPorts, LayOutDiagramHandler.IPorts
{
    public void ReportError(AppProblem problem)
    {
        Errors.Report(errors, problem);
    }

    public async Task<Outcome<List<TypeCard>>> ReadWithNeighboursAsync(CodeReference.Entity type, CancellationToken ct)
    {
        var read = await Background.Wait(Task.Run(() => TypeCards.ReadWithNeighbours(assemblies, type, ct), ct));
        ct.ThrowIfCancellationRequested();
        return read;
    }

    public async Task<Outcome<List<TypeCard>>> ReadCardsAsync(IReadOnlyList<CodeReference.Entity> types, CancellationToken ct)
    {
        var read = await Background.Wait(Task.Run(() => ReadCards(types), ct));
        ct.ThrowIfCancellationRequested();
        return read;
    }

    public async Task<Outcome<List<TypeCard>>> ReadDerivedAsync(CodeReference.Entity type, CancellationToken ct)
    {
        var read = await Background.Wait(Task.Run(() => TypeCards.ReadDerived(assemblies, type, ct), ct));
        ct.ThrowIfCancellationRequested();
        return read;
    }

    public async Task<Outcome<List<TypeCard>>> ReadCardsAsync(IReadOnlyList<TypeAnchor> types, CancellationToken ct)
    {
        var read = await Background.Wait(Task.Run(() => ReadCards(types), ct));
        ct.ThrowIfCancellationRequested();
        return read;
    }

    public async Task<Outcome<TypeLayout>> ArrangeAsync(DiagramMeasures measures, CancellationToken ct)
    {
        var laidOut = await Background.Wait(Task.Run(() => DiagramLayouts.Arrange(measures, ct), ct));
        ct.ThrowIfCancellationRequested();
        return laidOut;
    }

    private List<TypeCard> ReadCards(IReadOnlyList<CodeReference.Entity> types)
    {
        return types
            .Select(t => TypeCards.Read(assemblies, t))
            .OfType<TypeCard>()
            .ToList();
    }

    private List<TypeCard> ReadCards(IReadOnlyList<TypeAnchor> types)
    {
        return types
            .Select(t => TypeCards.Read(assemblies, t))
            .OfType<TypeCard>()
            .ToList();
    }
}

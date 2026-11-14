using ILSight.Core;
using ILSight.AssemblyArea;

namespace ILSight.App.Tests;

/// <summary>Answers <see cref="OpenAssembliesAsReloadHandler"/>'s opening with what the test set, doing
/// <see cref="WhileWorking"/> before it answers, and letting through the cancellation of the token it was asked with,
/// as the adapter does; keeps the tokens it was asked with.</summary>
internal sealed class FakeOpenAssembliesAsReloadHandlerPorts : OpenAssembliesAsReloadHandler.IPorts
{
    public LoadedAssembly? Opened { get; init; }

    public Action WhileWorking { get; init; } = static () => { };

    public List<CancellationToken> Tokens { get; } = [];

    public Task<LoadedAssembly?> OpenFilesAsync(IReadOnlyList<AssemblyPath> paths, CancellationToken ct)
    {
        Tokens.Add(ct);
        WhileWorking();
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(Opened);
    }
}

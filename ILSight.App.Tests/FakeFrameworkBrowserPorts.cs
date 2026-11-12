using ILSight.Core;
using ILSight.Localization;
using ILSight.FrameworkPicking;

namespace ILSight.App.Tests;

/// <summary>A runtime that has <see cref="Assemblies"/>, or fails to be scanned with <see cref="Failure"/>;
/// counts the scans and keeps the titles of the problems reported.</summary>
internal sealed class FakeFrameworkBrowserPorts : FrameworkBrowser.IPorts
{
    public IReadOnlyList<RuntimeAssembly> Assemblies { get; init; } = [];

    public Exception? Failure { get; set; }

    public int Scans { get; private set; }

    public List<string> Reported { get; } = [];

    public void ReportError(AppProblem problem)
    {
        Reported.Add(ProblemTexts.Describe(Strings.En, problem).Title);
    }

    public Task<Outcome<IReadOnlyList<RuntimeAssembly>>> ListAssembliesAsync(CancellationToken ct)
    {
        Scans++;
        var failure = Failure;
        if (failure is not null)
        {
            return Task.FromResult<Outcome<IReadOnlyList<RuntimeAssembly>>>(new Outcome<IReadOnlyList<RuntimeAssembly>>.Failed(failure));
        }

        return Task.FromResult<Outcome<IReadOnlyList<RuntimeAssembly>>>(new Outcome<IReadOnlyList<RuntimeAssembly>>.Ok(Assemblies));
    }
}

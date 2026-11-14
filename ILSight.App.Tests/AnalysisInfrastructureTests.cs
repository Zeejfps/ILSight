using System.Reflection.Metadata.Ecma335;
using ILSight.Core;
using ILSight.Core.Analysis;

namespace ILSight.App.Tests;

/// <summary>The analysis's adapter over real assemblies: what the core answers for a member the library can't read.</summary>
public sealed class AnalysisInfrastructureTests
{
    [Fact]
    public async Task RunAsync_CallerTheLibraryCannotRead_EndsWithItsFailureWithWhy()
    {
        using var temp = new TempDir();
        var greet = BrokenAssemblies.GetMethod(AppStateDriver.FixturePath, "Greeter", "Greet");
        var twice = BrokenAssemblies.GetMethod(AppStateDriver.FixturePath, "Greeter", "Twice");
        var path = AssemblyPath.Of(BrokenAssemblies.WriteUnreadableMethodName(temp, AppStateDriver.FixturePath, twice));
        var assemblies = new AssemblyList { Runtime = TestRuntime.Installed };
        AssemblyListing.Open(assemblies, path);
        var analysis = new AnalysisInfrastructure(new ErrorLog(), assemblies);
        var anchor = new CodeAnchor { Kind = CodeAnchorKind.Definition, FilePath = path, Token = MetadataTokens.GetToken(greet) };
        var found = Assert.IsType<Outcome<Lookup<AnalysisSymbol>>.Ok>(Assert.Single(await analysis.LookUpAnalyzedAsync([anchor], CancellationToken.None))).Value;
        var symbol = Assert.IsType<Lookup<AnalysisSymbol>.Found>(found).Value;
        var usedBy = symbol.Kinds.Single(static k => Analyzers.GetHeader(k) == "Used By");

        var batches = new List<Outcome<IReadOnlyList<AnalysisFind>>>();
        await foreach (var batch in analysis.RunAsync(symbol, usedBy, CancellationToken.None))
        {
            batches.Add(batch);
        }

        Assert.IsType<BadImageFormatException>(Assert.IsType<Outcome<IReadOnlyList<AnalysisFind>>.Failed>(batches[^1]).Error);
    }
}

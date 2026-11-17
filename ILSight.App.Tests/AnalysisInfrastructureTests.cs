using System.Reflection.Metadata.Ecma335;
using ILSight.Core;
using ILSight.Core.AnalysisArea;

namespace ILSight.App.Tests;

/// <summary>The analysis's adapter over real assemblies: what the core answers for a member the library can't read.</summary>
public sealed class AnalysisInfrastructureTests
{
    [Fact]
    public async Task RunAsync_CallerTheLibraryCannotRead_NamesItsModulePartlyReadWithWhy()
    {
        using var temp = new TempDir();
        var greet = BrokenAssemblies.GetMethod(AppStateDriver.FixturePath, "Greeter", "Greet");
        var twice = BrokenAssemblies.GetMethod(AppStateDriver.FixturePath, "Greeter", "Twice");
        var unreadablePath = BrokenAssemblies.WriteUnreadableMethodName(temp, AppStateDriver.FixturePath, twice);
        var path = AssemblyPath.Of(unreadablePath);
        var assemblies = new AssemblyList { Runtime = TestRuntime.Installed };
        AssemblyListing.Open(assemblies, path);
        var analysis = new AnalysisInfrastructure(new ErrorLog(), assemblies);
        var anchor = new CodeAnchor { Kind = CodeAnchorKind.Definition, FilePath = path, Token = MetadataTokens.GetToken(greet) };
        var found = UnionAsserts.GetCase<Outcome<Lookup<AnalysisSymbol>>.Ok>(Assert.Single(await analysis.LookUpAnalyzedAsync([anchor], CancellationToken.None))).Value;
        var symbol = UnionAsserts.GetCase<Lookup<AnalysisSymbol>.Found>(found).Value;
        var usedBy = symbol.Kinds.Single(static k => Analyzers.GetHeader(k) == "Used By");

        var batches = new List<Outcome<IReadOnlyList<AnalysisFind>>>();
        await foreach (var batch in analysis.RunAsync(symbol, usedBy, CancellationToken.None))
        {
            batches.Add(batch);
        }

        var finds = batches.SelectMany(static b => UnionAsserts.GetCase<Outcome<IReadOnlyList<AnalysisFind>>.Ok>(b).Value).ToList();
        var partly = UnionAsserts.GetCase<AnalysisFind.PartlyRead>(Assert.Single(finds, static f => f is AnalysisFind.PartlyRead));
        Assert.Equal("UnreadableMethodName.dll", partly.Name);
        Assert.IsType<BadImageFormatException>(partly.Error);
    }
}

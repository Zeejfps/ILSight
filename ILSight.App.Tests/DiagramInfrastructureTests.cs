using ILSight.Core;
using ILSight.Core.DiagramArea;
using ILSight.DiagramArea;

namespace ILSight.App.Tests;

/// <summary>The diagram's adapter over real assemblies: what the core answers for a type the library can't read.</summary>
public sealed class DiagramInfrastructureTests
{
    [Fact]
    public async Task ReadCardsAsync_TypeWhoseMemberTheLibraryCannotRead_AnswersItsFailureWithWhy()
    {
        using var temp = new TempDir();
        var greet = BrokenAssemblies.GetMethod(AppStateDriver.FixturePath, "Greeter", "Greet");
        var unreadablePath = BrokenAssemblies.WriteUnreadableMethodName(temp, AppStateDriver.FixturePath, greet);
        var path = AssemblyPath.Of(unreadablePath);
        var greeter = new TypeAnchor { AssemblyPath = path, FullName = "ILSight.App.Tests.Fixtures.Greeter" };
        var diagram = new DiagramInfrastructure(new ErrorLog(), new RefusalLine(), new AssemblyList { Runtime = TestRuntime.Installed });

        var cards = await diagram.ReadCardsAsync([greeter], CancellationToken.None);

        Assert.IsType<BadImageFormatException>(Assert.IsType<Outcome<List<Lookup<TypeCard>>>.Failed>(cards).Error);
    }
}

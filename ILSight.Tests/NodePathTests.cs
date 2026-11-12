using ILSight.Core;

namespace ILSight.Tests;

public sealed class NodePathTests
{
    [Fact]
    public async Task A_listed_lookup_finds_a_place_only_in_an_assembly_the_list_holds_and_lists_none()
    {
        var (_, assembly) = Trees.OpenTestAssembly();
        var add = await assembly.CalculatorAsync().GetChildAsync("Add(T) : void");
        var empty = new AssemblyList { Runtime = TestRuntime.Installed };

        Assert.Null(await empty.FindListedNodeAsync(add.Path));
        Assert.Empty(empty.Listed);

        var (listing, _) = Trees.OpenTestAssembly();
        Assert.Equal(add.Text, (await listing.FindListedNodeAsync(add.Path))?.Text);
    }

    [Fact]
    public async Task A_path_finds_the_same_place_in_a_reopened_assembly()
    {
        var (_, assembly) = Trees.OpenTestAssembly();
        var add = await assembly.CalculatorAsync().GetChildAsync("Add(T) : void");
        var (fresh, _) = Trees.OpenTestAssembly();

        var found = await fresh.FindNodeAsync(add.Path);

        Assert.NotNull(found);
        Assert.NotSame(add, found);
        Assert.Equal(add.Path, found.Path);
        Assert.Equal(add.Text, found.Text);
        Assert.Contains(found.Assembly, fresh.Assemblies);
    }

    [Fact]
    public async Task Paths_are_equal_by_place()
    {
        var (_, a) = Trees.OpenTestAssembly();
        var (_, b) = Trees.OpenTestAssembly();

        Assert.Equal((await a.CalculatorAsync()).Path, (await b.CalculatorAsync()).Path);
        Assert.Equal((await a.CalculatorAsync()).Path.GetHashCode(), (await b.CalculatorAsync()).Path.GetHashCode());
        Assert.NotEqual((await a.CalculatorAsync()).Path, (await a.FixturesAsync()).Path);
        Assert.Equal((await a.ReadRootAsync()).Path, (await b.ReadRootAsync()).Path);
    }

    [Fact]
    public async Task A_path_into_a_file_that_is_gone_finds_nothing()
    {
        using var temp = new TempDir();
        var copy = temp.Copy(Trees.TestAssemblyPath, "fixture.dll");
        var path = (await new AssemblyList { Runtime = TestRuntime.Installed }.Open(copy).CalculatorAsync()).Path;
        File.Delete(copy);

        Assert.Null(await new AssemblyList { Runtime = TestRuntime.Installed }.FindNodeAsync(path));
    }

    [Fact]
    public void A_closed_assembly_is_never_listed_again()
    {
        var (list, assembly) = Trees.OpenTestAssembly();
        list.Close(assembly);

        Assert.False(AssemblyListing.TryList(list, assembly));
        var reopened = list.Open(Trees.TestAssemblyPath);
        Assert.False(AssemblyListing.TryList(list, assembly));

        Assert.NotSame(assembly, reopened);
        Assert.Equal([reopened], list.Assemblies);
    }

    [Fact]
    public async Task Children_can_be_asked_for_without_building_them()
    {
        var (_, assembly) = Trees.OpenTestAssembly();
        var root = await assembly.ReadRootAsync();

        Assert.IsType<ChildrenState.Unloaded>(root.ChildrenState);

        var built = await root.ListChildrenAsync();
        Assert.Same(built, Assert.IsType<ChildrenState.Ready>(root.ChildrenState).Children);
    }
}

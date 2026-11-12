using ILSight.Core;

namespace ILSight.Tests;

public sealed class NodePathTests
{
    [Fact]
    public void A_listed_lookup_finds_a_place_only_in_an_assembly_the_list_holds_and_lists_none()
    {
        var (_, assembly) = Trees.OpenTestAssembly();
        var add = assembly.Calculator().Child("Add(T) : void");
        var empty = new AssemblyList { Runtime = TestRuntime.Installed };

        Assert.Null(empty.FindListedNode(BrowseNodes.GetPath(add)));
        Assert.Empty(empty.Listed);

        var (listing, _) = Trees.OpenTestAssembly();
        Assert.Equal(add.Text, listing.FindListedNode(BrowseNodes.GetPath(add))?.Text);
    }

    [Fact]
    public void A_path_finds_the_same_place_in_a_reopened_assembly()
    {
        var (_, assembly) = Trees.OpenTestAssembly();
        var add = assembly.Calculator().Child("Add(T) : void");
        var (fresh, _) = Trees.OpenTestAssembly();

        var found = fresh.FindNode(BrowseNodes.GetPath(add));

        Assert.NotNull(found);
        Assert.NotSame(add, found);
        Assert.Equal(BrowseNodes.GetPath(add), BrowseNodes.GetPath(found));
        Assert.Equal(add.Text, found.Text);
        Assert.Contains(found.Assembly, fresh.Assemblies);
    }

    [Fact]
    public void Paths_are_equal_by_place()
    {
        var (_, a) = Trees.OpenTestAssembly();
        var (_, b) = Trees.OpenTestAssembly();

        Assert.Equal(BrowseNodes.GetPath(a.Calculator()), BrowseNodes.GetPath(b.Calculator()));
        Assert.Equal(BrowseNodes.GetPath(a.Calculator()).GetHashCode(), BrowseNodes.GetPath(b.Calculator()).GetHashCode());
        Assert.NotEqual(BrowseNodes.GetPath(a.Calculator()), BrowseNodes.GetPath(a.Fixtures()));
        Assert.Equal(BrowseNodes.GetPath(a.Root), BrowseNodes.GetPath(b.Root));
    }

    [Fact]
    public void A_path_into_a_file_that_is_gone_finds_nothing()
    {
        using var temp = new TempDir();
        var copy = temp.Copy(Trees.TestAssemblyPath, "fixture.dll");
        var path = BrowseNodes.GetPath(new AssemblyList { Runtime = TestRuntime.Installed }.Open(copy).Calculator());
        File.Delete(copy);

        Assert.Null(new AssemblyList { Runtime = TestRuntime.Installed }.FindNode(path));
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
    public void Children_can_be_asked_for_without_building_them()
    {
        var (_, assembly) = Trees.OpenTestAssembly();
        var root = assembly.Root;

        Assert.IsType<ChildrenState.Unloaded>(root.ChildrenState);

        var built = root.Children;
        Assert.Same(built, Assert.IsType<ChildrenState.Ready>(root.ChildrenState).Children);
    }
}

using ILSight.AssemblyListArea;

namespace ILSight.App.Tests;

public sealed class SavedAssemblyListsTests
{
    [Fact]
    public void A_new_list_is_numbered_when_its_name_is_taken()
    {
        var lists = new SavedAssemblyLists();

        var first = lists.Add("New list");
        var second = lists.Add("new list");

        Assert.Equal("New list", first);
        Assert.Equal("new list 2", second);
        Assert.Equal([SavedAssemblyLists.DefaultName, "New list", "new list 2"], lists.Names);
    }

    [Fact]
    public void A_list_is_never_made_without_a_name()
    {
        var lists = new SavedAssemblyLists();

        Assert.Throws<ArgumentException>(() => lists.Add(" "));
        Assert.Throws<ArgumentException>(() => lists.Duplicate(SavedAssemblyLists.DefaultName, ""));
        Assert.Equal([SavedAssemblyLists.DefaultName], lists.Names);
    }

    [Fact]
    public void A_duplicate_holds_the_same_assemblies_under_a_name_of_its_own()
    {
        var lists = new SavedAssemblyLists();
        lists.RememberActivePaths([TestPaths.FakePath("a.dll"), TestPaths.FakePath("b.dll")]);

        var copy = lists.Duplicate(SavedAssemblyLists.DefaultName, "Default copy");
        lists.RememberActivePaths([TestPaths.FakePath("c.dll")]);

        Assert.Equal("Default copy", copy);
        Assert.Equal([TestPaths.FakePath("a.dll"), TestPaths.FakePath("b.dll")], lists.GetPaths(copy));
    }

    [Fact]
    public void A_list_made_from_assemblies_holds_them_under_a_name_of_its_own()
    {
        var lists = new SavedAssemblyLists();
        lists.Add("Framework");

        var name = lists.Add("Framework", [TestPaths.FakePath("a.dll"), TestPaths.FakePath("b.dll")]);

        Assert.Equal("Framework 2", name);
        Assert.Equal([TestPaths.FakePath("a.dll"), TestPaths.FakePath("b.dll")], lists.GetPaths(name));
        Assert.Equal(SavedAssemblyLists.DefaultName, lists.Active);
    }

    [Fact]
    public void A_list_is_not_renamed_to_nothing_or_to_another_lists_name()
    {
        var lists = new SavedAssemblyLists();
        lists.Add("Work");

        Assert.False(lists.TryRename("Work", "  "));
        Assert.False(lists.TryRename("Work", "default"));
        Assert.True(lists.TryRename("Work", "WORK"));
        Assert.True(lists.TryRename(SavedAssemblyLists.DefaultName, " Play "));
        Assert.Equal(["Play", "WORK"], lists.Names);
        Assert.Equal("Play", lists.Active);
    }

    [Fact]
    public void Deleting_the_open_list_opens_the_one_that_takes_its_place_and_the_last_leaves_an_empty_default()
    {
        var lists = new SavedAssemblyLists();
        lists.Add("Work");
        lists.Add("Play");
        lists.TryActivate("Play");

        lists.Remove("Play");
        Assert.Equal("Work", lists.Active);

        lists.Remove("Work");
        lists.RememberActivePaths([TestPaths.FakePath("a.dll")]);
        lists.Remove(SavedAssemblyLists.DefaultName);

        Assert.Equal([SavedAssemblyLists.DefaultName], lists.Names);
        Assert.Empty(lists.ActivePaths);
    }

    [Fact]
    public void The_revision_rises_with_every_change_to_the_lists_and_not_with_what_changes_nothing()
    {
        var lists = new SavedAssemblyLists();
        var revisions = new List<int> { lists.Revision };

        lists.Add("Work");
        revisions.Add(lists.Revision);
        Assert.False(lists.TryRename("Work", SavedAssemblyLists.DefaultName));
        Assert.False(lists.TryActivate(SavedAssemblyLists.DefaultName));
        lists.RememberActivePaths([]);
        lists.RememberActiveDocuments(null);
        revisions.Add(lists.Revision);
        lists.RememberActivePaths([TestPaths.FakePath("a.dll")]);
        revisions.Add(lists.Revision);
        Assert.True(lists.TryRename("Work", "Play"));
        revisions.Add(lists.Revision);
        Assert.True(lists.TryActivate("Play"));
        revisions.Add(lists.Revision);
        lists.Remove("Play");
        revisions.Add(lists.Revision);

        Assert.Equal([0, 1, 1, 2, 3, 4, 5], revisions);
        Assert.Equal(SavedAssemblyLists.DefaultName, lists.Active);
    }

    [Fact]
    public void Loading_drops_nameless_and_repeated_lists_and_opens_the_named_one_or_else_the_first()
    {
        var lists = new SavedAssemblyLists();
        List<AssemblyListRecord> records =
        [
            new AssemblyListRecord { Name = " ", Assemblies = [TestPaths.FakePath("x.dll")] },
            new AssemblyListRecord { Name = "Work", Assemblies = [TestPaths.FakePath("a.dll")] },
            new AssemblyListRecord { Name = "work", Assemblies = [TestPaths.FakePath("b.dll")] },
            new AssemblyListRecord { Name = "Play", Assemblies = [TestPaths.FakePath("c.dll")] },
        ];

        lists.Restore(records, "play");
        Assert.Equal(["Work", "Play"], lists.Names);
        Assert.Equal("Play", lists.Active);

        lists.Restore(records, "Gone");
        Assert.Equal("Work", lists.Active);
        Assert.Equal([TestPaths.FakePath("a.dll")], lists.ActivePaths);
    }
}

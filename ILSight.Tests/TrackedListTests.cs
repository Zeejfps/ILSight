using ILSight.Core;

namespace ILSight.Tests;

public sealed class TrackedListTests
{
    private static TrackedList<string> Holding(params string[] items)
    {
        var list = new TrackedList<string>();
        foreach (var item in items)
        {
            list.Add(item);
        }

        return list;
    }

    [Fact]
    public void It_starts_empty_at_no_revision()
    {
        var list = new TrackedList<string>();

        Assert.Empty(list);
        Assert.Equal(0, list.Revision);
    }

    [Fact]
    public void Each_change_counts()
    {
        var list = Holding("a", "b", "c");
        Assert.Equal(3, list.Revision);

        list[0] = "d";
        Assert.Equal(4, list.Revision);

        list.RemoveAt(0);
        Assert.Equal(5, list.Revision);

        Assert.True(list.Remove("b"));
        Assert.Equal(6, list.Revision);

        list.Clear();
        Assert.Equal(7, list.Revision);
    }

    [Fact]
    public void What_changes_nothing_does_not_count()
    {
        var list = Holding("a");
        var revision = list.Revision;

        list[0] = "a";
        Assert.False(list.Remove("gone"));
        _ = list.Contains("a");
        _ = list.IndexOf("a");
        _ = list[0];

        Assert.Equal(revision, list.Revision);
    }

    [Fact]
    public void Clearing_nothing_does_not_count()
    {
        var list = new TrackedList<string>();

        list.Clear();

        Assert.Equal(0, list.Revision);
    }

    [Fact]
    public void A_change_inside_an_item_counts_once_it_is_told()
    {
        var list = Holding("a");
        var revision = list.Revision;

        list.NoteChange();

        Assert.Equal(revision + 1, list.Revision);
    }

    [Fact]
    public void It_holds_what_a_list_would()
    {
        var list = Holding("a", "b", "c");

        list[1] = "d";
        list.RemoveAt(0);

        Assert.Equal(["d", "c"], list);
        Assert.Equal(1, list.IndexOf("c"));
        Assert.Contains("d", list);
        Assert.Equal(-1, list.IndexOf("a"));
    }

    [Fact]
    public void Going_through_it_allocates_nothing()
    {
        var list = new TrackedList<int>();
        list.Add(1);

        var before = GC.GetAllocatedBytesForCurrentThread();
        var total = 0;
        foreach (var item in list)
        {
            total += item;
        }

        Assert.Equal(before, GC.GetAllocatedBytesForCurrentThread());
        Assert.Equal(1, total);
    }
}

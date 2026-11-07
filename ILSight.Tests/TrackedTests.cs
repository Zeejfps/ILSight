using ILSight.Core;

namespace ILSight.Tests;

public sealed class TrackedTests
{
    [Fact]
    public void A_value_starts_as_given_with_no_changes()
    {
        var value = new Tracked<int>(3);

        Assert.Equal(3, value.Value);
        Assert.Equal(0, value.Changes);
    }

    [Fact]
    public void A_different_value_counts()
    {
        var value = new Tracked<bool>(false);

        Assert.True(value.Set(true));

        Assert.True(value.Value);
        Assert.Equal(1, value.Changes);
    }

    [Fact]
    public void The_same_value_does_not_count()
    {
        var value = new Tracked<bool>(false);

        Assert.False(value.Set(false));

        Assert.Equal(0, value.Changes);
    }

    [Fact]
    public void Going_to_null_and_back_counts_each_way()
    {
        var value = new Tracked<string?>("a");

        value.Set(null);
        value.Set(null);
        value.Set("a");

        Assert.Equal(2, value.Changes);
    }

    [Fact]
    public void A_set_flips_what_is_given_and_counts_each_flip()
    {
        var set = new TrackedSet<string>();

        set.Flip("a");
        Assert.Contains("a", set);
        Assert.Equal(1, set.Revision);

        set.Flip("a");
        Assert.DoesNotContain("a", set);
        Assert.Equal(2, set.Revision);
    }

    [Fact]
    public void Replacing_a_set_counts_once_and_holds_what_was_given()
    {
        var set = new TrackedSet<string>();
        set.Flip("a");
        var revision = set.Revision;

        set.Replace(["b", "c"]);

        Assert.Equal(["b", "c"], set.Order());
        Assert.Equal(2, set.Count);
        Assert.Equal(revision + 1, set.Revision);
    }

    [Fact]
    public void Replacing_a_set_with_what_it_holds_does_not_count()
    {
        var set = new TrackedSet<string>();
        set.Replace(["a", "b"]);
        var revision = set.Revision;

        set.Replace(["b", "a", "a"]);

        Assert.Equal(revision, set.Revision);
    }

    [Fact]
    public void Replacing_an_empty_set_with_nothing_does_not_count()
    {
        var set = new TrackedSet<string>();

        set.Replace([]);

        Assert.Equal(0, set.Revision);
    }

    [Fact]
    public void A_set_replaced_from_itself_keeps_what_it_holds()
    {
        var set = new TrackedSet<string>();
        set.Replace(["a"]);

        set.Replace(set);

        Assert.Equal(["a"], set);
    }

    [Fact]
    public void Asking_a_set_allocates_nothing()
    {
        var set = new TrackedSet<int>();
        set.Flip(1);

        var before = GC.GetAllocatedBytesForCurrentThread();
        var found = 0;
        foreach (var item in set)
            found += set.Contains(item) ? 1 : 0;

        Assert.Equal(before, GC.GetAllocatedBytesForCurrentThread());
        Assert.Equal(1, found);
    }
}

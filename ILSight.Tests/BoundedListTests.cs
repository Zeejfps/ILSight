using ILSight.Core;

namespace ILSight.Tests;

public sealed class BoundedListTests
{
    [Fact]
    public void It_starts_empty_and_keeps_what_is_added_in_order()
    {
        var list = new BoundedList<int>(3);
        Assert.Empty(list);

        list.Add(1);
        list.Add(2);

        Assert.Equal([1, 2], list);
        Assert.Equal(2, list[1]);
    }

    [Fact]
    public void The_oldest_goes_once_it_is_past_its_capacity()
    {
        var list = new BoundedList<int>(2);

        list.Add(1);
        list.Add(2);
        list.Add(3);

        Assert.Equal([2, 3], list);
    }

    [Fact]
    public void It_counts_each_item_added_though_the_oldest_went()
    {
        var list = new BoundedList<int>(1);

        list.Add(1);
        list.Add(2);

        Assert.Single(list);
        Assert.Equal(2, list.Added);
    }

    [Fact]
    public void Clearing_empties_it_and_leaves_the_count_of_what_was_added()
    {
        var list = new BoundedList<int>(2);
        list.Add(1);

        list.Clear();

        Assert.Empty(list);
        Assert.Equal(1, list.Added);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void A_capacity_with_no_room_is_refused(int capacity)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new BoundedList<int>(capacity));
    }

    [Fact]
    public void Going_through_it_allocates_nothing()
    {
        var list = new BoundedList<int>(4);
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

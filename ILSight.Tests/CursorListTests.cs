using ILSight.Core;

namespace ILSight.Tests;

public sealed class CursorListTests
{
    private static CursorList<string> Holding(params string[] items)
    {
        var list = new CursorList<string>(10);
        foreach (var item in items)
        {
            list.Add(item);
        }

        return list;
    }

    [Fact]
    public void Empty_it_has_no_current_item()
    {
        var list = new CursorList<string>(2);

        Assert.Equal(-1, list.CurrentIndex);
        Assert.Null(list.Current);
        Assert.Equal(0, list.Revision);
    }

    [Fact]
    public void What_is_added_becomes_current()
    {
        var list = Holding("a", "b");

        Assert.Equal(1, list.CurrentIndex);
        Assert.Equal("b", list.Current);
    }

    [Fact]
    public void Adding_drops_what_was_after_the_current_item()
    {
        var list = Holding("a", "b", "c");
        list.MoveTo(0);

        list.Add("d");

        Assert.Equal(["a", "d"], list);
        Assert.Equal("d", list.Current);
    }

    [Fact]
    public void Past_its_capacity_the_oldest_goes_and_the_new_item_stays_current()
    {
        var list = new CursorList<string>(2);
        list.Add("a");
        list.Add("b");

        list.Add("c");

        Assert.Equal(["b", "c"], list);
        Assert.Equal(1, list.CurrentIndex);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(2)]
    [InlineData(99)]
    public void It_refuses_to_move_outside_its_items(int index)
    {
        var list = Holding("a", "b");
        var revision = list.Revision;

        Assert.False(list.MoveTo(index));

        Assert.Equal(1, list.CurrentIndex);
        Assert.Equal(revision, list.Revision);
    }

    [Fact]
    public void Moving_to_where_it_is_is_no_change()
    {
        var list = Holding("a", "b");
        var revision = list.Revision;

        Assert.False(list.MoveTo(1));

        Assert.Equal(revision, list.Revision);
    }

    [Fact]
    public void Moving_counts_as_a_change()
    {
        var list = Holding("a", "b");
        var revision = list.Revision;

        Assert.True(list.MoveTo(0));

        Assert.Equal("a", list.Current);
        Assert.True(list.Revision > revision);
    }

    [Fact]
    public void Removing_the_current_item_makes_the_one_before_it_current()
    {
        var list = Holding("a", "b", "c");
        list.MoveTo(1);

        list.RemoveAt(1);

        Assert.Equal("a", list.Current);
    }

    [Fact]
    public void Removing_the_first_while_it_is_current_makes_the_first_left_current()
    {
        var list = Holding("a", "b");
        list.MoveTo(0);

        list.RemoveAt(0);

        Assert.Equal(0, list.CurrentIndex);
        Assert.Equal("b", list.Current);
    }

    [Fact]
    public void Removing_after_or_before_the_current_item_keeps_it_current()
    {
        var list = Holding("a", "b", "c");
        list.MoveTo(1);

        list.RemoveAt(2);
        Assert.Equal("b", list.Current);

        list.RemoveAt(0);
        Assert.Equal("b", list.Current);
        Assert.Equal(0, list.CurrentIndex);
    }

    [Fact]
    public void Removing_the_last_item_leaves_no_current_one()
    {
        var list = Holding("a");

        list.RemoveAt(0);

        Assert.Equal(-1, list.CurrentIndex);
        Assert.Null(list.Current);
    }

    [Fact]
    public void Clearing_leaves_no_current_item_and_clearing_nothing_is_no_change()
    {
        var list = Holding("a");

        list.Clear();
        var revision = list.Revision;
        list.Clear();

        Assert.Equal(-1, list.CurrentIndex);
        Assert.Empty(list);
        Assert.Equal(revision, list.Revision);
    }

    [Fact]
    public void Replacing_the_current_item_keeps_the_place_and_is_not_counted()
    {
        var list = Holding("a", "b");
        var revision = list.Revision;

        Assert.True(list.ReplaceCurrent("c"));

        Assert.Equal(["a", "c"], list);
        Assert.Equal(1, list.CurrentIndex);
        Assert.Equal(revision, list.Revision);
    }

    [Fact]
    public void Replacing_with_nothing_current_does_nothing()
    {
        var list = new CursorList<string>(2);

        Assert.False(list.ReplaceCurrent("a"));

        Assert.Empty(list);
    }

    [Fact]
    public void A_capacity_with_no_room_is_refused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new CursorList<string>(0));
    }

    /// <summary>Random changes never leave the place outside the items, whatever the order.</summary>
    [Fact]
    public void No_order_of_changes_leaves_the_place_outside_the_items()
    {
        var random = new Random(12345);
        var list = new CursorList<string>(5);
        for (var step = 0; step < 20_000; step++)
        {
            switch (random.Next(5))
            {
                case 0 or 1:
                    list.Add("item");
                    break;
                case 2 when list.Count > 0:
                    list.RemoveAt(random.Next(list.Count));
                    break;
                case 3:
                    list.MoveTo(random.Next(-1, list.Count + 1));
                    break;
                case 4 when random.Next(20) == 0:
                    list.Clear();
                    break;
            }

            Assert.InRange(list.Count, 0, 5);
            Assert.Equal(list.Count == 0, list.CurrentIndex == -1);
            Assert.InRange(list.CurrentIndex, -1, list.Count - 1);
        }
    }
}

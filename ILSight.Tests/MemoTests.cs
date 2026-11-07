using ILSight.Core;

namespace ILSight.Tests;

public sealed class MemoTests
{
    private sealed record Report(string Name);

    [Fact]
    public void One_made_by_default_holds_nothing_whatever_the_key()
    {
        var memo = default(Memo<int, string>);

        Assert.False(memo.TryGet(0, out var value));
        Assert.Null(value);
        Assert.Null(memo.Last);
        Assert.False(default(Memo<string, int>).TryGet("", out _));
        Assert.False(default(Memo<(ByReference<string>, int), int>).TryGet((new ByReference<string>(null), 0), out _));
    }

    [Fact]
    public void It_gives_what_it_holds_only_for_the_key_it_was_worked_out_for()
    {
        var memo = new Memo<int, string>(1, "one");

        Assert.True(memo.TryGet(1, out var value));
        Assert.Equal("one", value);
        Assert.False(memo.TryGet(2, out _));
        Assert.Equal("one", memo.Last);
    }

    [Fact]
    public void Assigning_a_new_one_replaces_key_and_value_together()
    {
        var memo = new Memo<int, string>(1, "one");

        memo = new Memo<int, string>(2, "two");

        Assert.False(memo.TryGet(1, out _));
        Assert.True(memo.TryGet(2, out var value));
        Assert.Equal("two", value);
    }

    [Fact]
    public void A_copy_stands_for_the_same_pair()
    {
        var memo = new Memo<int, string>(1, "one");
        var copy = memo;

        memo = new Memo<int, string>(2, "two");

        Assert.True(copy.TryGet(1, out var value));
        Assert.Equal("one", value);
    }

    [Fact]
    public void A_key_of_several_parts_needs_all_of_them_to_match()
    {
        var memo = new Memo<(string Query, int Revision), int>(("a", 1), 7);

        Assert.True(memo.TryGet(("a", 1), out _));
        Assert.False(memo.TryGet(("a", 2), out _));
        Assert.False(memo.TryGet(("b", 1), out _));
    }

    [Fact]
    public void A_key_wrapped_by_reference_tells_two_equal_records_apart()
    {
        var report = new Report("same");
        var equal = new Report("same");
        Assert.Equal(report, equal);
        var memo = new Memo<ByReference<Report>, int>(new ByReference<Report>(report), 1);

        Assert.True(memo.TryGet(new ByReference<Report>(report), out _));
        Assert.False(memo.TryGet(new ByReference<Report>(equal), out _));
    }

    [Fact]
    public void A_key_left_bare_compares_as_its_type_does()
    {
        var memo = new Memo<Report, int>(new Report("same"), 1);

        Assert.True(memo.TryGet(new Report("same"), out _));
    }

    [Fact]
    public void A_null_key_held_by_reference_matches_only_null()
    {
        var memo = new Memo<ByReference<string>, int>(new ByReference<string>(null), 1);

        Assert.True(memo.TryGet(new ByReference<string>(null), out _));
        Assert.True(memo.TryGet(default, out _));
        Assert.False(memo.TryGet(new ByReference<string>("text"), out _));
    }

    [Fact]
    public void Things_compared_by_reference_hash_as_their_identity_and_not_their_contents()
    {
        var report = new Report("same");

        Assert.Equal(new ByReference<Report>(report).GetHashCode(), new ByReference<Report>(report).GetHashCode());
        Assert.Equal(new ByReference<Report>(report), (object)new ByReference<Report>(report));
        Assert.NotEqual(new ByReference<Report>(report), (object)report);
        Assert.Equal(new ByReference<Report>(null).GetHashCode(), default(ByReference<Report>).GetHashCode());
    }

    [Fact]
    public void Asking_allocates_nothing()
    {
        var report = new Report("same");
        var memo = new Memo<(ByReference<Report>, string, int), int>((new ByReference<Report>(report), "text", 1), 7);
        memo.TryGet((new ByReference<Report>(report), "text", 1), out _);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1000; i++)
            memo.TryGet((new ByReference<Report>(report), "text", 1), out _);

        Assert.Equal(before, GC.GetAllocatedBytesForCurrentThread());
    }
}

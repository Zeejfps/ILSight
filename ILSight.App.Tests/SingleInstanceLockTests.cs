namespace ILSight.App.Tests;

public sealed class SingleInstanceLockTests
{
    [Fact]
    public void The_first_run_with_some_settings_gets_the_lock_and_later_ones_do_not()
    {
        using var temp = new TempDir();
        var address = CommandLineAddress.ForSettingsFile(temp.PathOf("settings.json"));

        var first = SingleInstanceLock.TryAcquire(address, out var firstLock);
        var later = SingleInstanceLock.TryAcquire(address, out var laterLock);
        using var held = firstLock;

        Assert.True(first);
        Assert.False(later);
        Assert.Null(laterLock);
    }

    [Fact]
    public void Runs_with_other_settings_each_get_a_lock()
    {
        using var temp = new TempDir();

        var one = SingleInstanceLock.TryAcquire(CommandLineAddress.ForSettingsFile(temp.PathOf("one.json")), out var oneLock);
        var other = SingleInstanceLock.TryAcquire(CommandLineAddress.ForSettingsFile(temp.PathOf("other.json")), out var otherLock);
        using var heldOne = oneLock;
        using var heldOther = otherLock;

        Assert.True(one);
        Assert.True(other);
    }

    [Fact]
    public void Once_the_first_run_ends_the_next_one_gets_the_lock()
    {
        using var temp = new TempDir();
        var address = CommandLineAddress.ForSettingsFile(temp.PathOf("settings.json"));
        Assert.True(SingleInstanceLock.TryAcquire(address, out var first));
        first.Dispose();

        var next = SingleInstanceLock.TryAcquire(address, out var nextLock);
        using var held = nextLock;

        Assert.True(next);
    }

    [Fact]
    public void A_run_that_did_not_get_the_lock_does_not_keep_it_from_the_next_one()
    {
        using var temp = new TempDir();
        var address = CommandLineAddress.ForSettingsFile(temp.PathOf("settings.json"));
        Assert.True(SingleInstanceLock.TryAcquire(address, out var first));
        Assert.False(SingleInstanceLock.TryAcquire(address, out _));
        first.Dispose();

        var next = SingleInstanceLock.TryAcquire(address, out var nextLock);
        using var held = nextLock;

        Assert.True(next);
    }
}

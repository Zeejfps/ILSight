namespace ILSight.App.Tests;

public sealed class SingleInstanceLockTests
{
    [Fact]
    public void The_first_run_with_some_settings_is_granted_the_lock_and_later_ones_are_not()
    {
        using var temp = new TempDir();
        var settings = temp.PathOf("settings.json");
        using var first = SingleInstanceLock.Request(settings);
        using var later = SingleInstanceLock.Request(settings);

        Assert.True(first.IsGranted);
        Assert.False(later.IsGranted);
        Assert.Equal(first.Name, later.Name);
    }

    [Fact]
    public void Runs_with_other_settings_are_each_the_first()
    {
        using var temp = new TempDir();
        using var one = SingleInstanceLock.Request(temp.PathOf("one.json"));
        using var other = SingleInstanceLock.Request(temp.PathOf("other.json"));

        Assert.True(one.IsGranted);
        Assert.True(other.IsGranted);
    }

    [Fact]
    public void Once_the_first_run_ends_the_next_one_is_the_first()
    {
        using var temp = new TempDir();
        var settings = temp.PathOf("settings.json");
        var first = SingleInstanceLock.Request(settings);
        first.Dispose();

        using var next = SingleInstanceLock.Request(settings);

        Assert.True(next.IsGranted);
    }
}

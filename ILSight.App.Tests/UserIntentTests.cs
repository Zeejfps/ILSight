using ILSight.UserState;

namespace ILSight.App.Tests;

public sealed class UserIntentTests
{
    [Fact]
    public void EndResolving_LookupFoundThePlace_IsFoundAndStopsResolving()
    {
        var intent = new UserIntent();
        var ticket = intent.BeginResolving();
        Assert.True(intent.IsResolving);

        var resolved = intent.EndResolving<string>(ticket, new Outcome<string?>.Ok("place"));

        Assert.Equal("place", Assert.IsType<Resolution<string>.Found>(resolved).Value);
        Assert.False(intent.IsResolving);
    }

    [Fact]
    public void EndResolving_LookupFoundNothing_IsMissing()
    {
        var intent = new UserIntent();
        var ticket = intent.BeginResolving();

        var resolved = intent.EndResolving<string>(ticket, new Outcome<string?>.Ok(null));

        Assert.IsType<Resolution<string>.Missing>(resolved);
        Assert.False(intent.IsResolving);
    }

    [Fact]
    public void EndResolving_LookupFailed_IsFailedWithTheError()
    {
        var intent = new UserIntent();
        var error = new InvalidOperationException("unreadable");
        var ticket = intent.BeginResolving();

        var resolved = intent.EndResolving<string>(ticket, new Outcome<string?>.Failed(error));

        Assert.Same(error, Assert.IsType<Resolution<string>.Failed>(resolved).Error);
        Assert.False(intent.IsResolving);
    }

    [Fact]
    public void EndResolving_ValueTypeLookup_FindsTheValueOrNothing()
    {
        var intent = new UserIntent();

        var found = intent.EndResolving<int>(intent.BeginResolving(), new Outcome<int?>.Ok(7));
        var missing = intent.EndResolving<int>(intent.BeginResolving(), new Outcome<int?>.Ok(null));

        Assert.Equal(7, Assert.IsType<Resolution<int>.Found>(found).Value);
        Assert.IsType<Resolution<int>.Missing>(missing);
    }

    [Fact]
    public void EndResolving_AnotherRequestBeganMeanwhile_IsStaleAndLeavesThatRequestAlone()
    {
        var intent = new UserIntent();
        var ticket = intent.BeginResolving();
        var next = intent.Begin();
        intent.StartResolving(next);

        var resolved = intent.EndResolving<string>(ticket, new Outcome<string?>.Ok("place"));

        Assert.IsType<Resolution<string>.Stale>(resolved);
        Assert.True(intent.IsResolving);
        Assert.True(intent.IsCurrent(next));
    }
}

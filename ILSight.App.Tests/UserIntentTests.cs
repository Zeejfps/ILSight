using ILSight.Core;
using ILSight.UserArea;

namespace ILSight.App.Tests;

public sealed class UserIntentTests
{
    [Fact]
    public void EndResolving_LookupFoundThePlace_IsFoundAndStopsResolving()
    {
        var intent = new UserIntent();
        var ticket = intent.BeginResolving();
        Assert.True(intent.IsResolving);

        var resolved = intent.EndResolving<string>(ticket, new Outcome<Lookup<string>>.Ok(Lookup.FromNullable<string>("place")));

        Assert.Equal("place", UnionAsserts.GetCase<Resolution<string>.Found>(resolved).Value);
        Assert.False(intent.IsResolving);
    }

    [Fact]
    public void EndResolving_LookupFoundNothing_IsMissing()
    {
        var intent = new UserIntent();
        var ticket = intent.BeginResolving();

        var resolved = intent.EndResolving<string>(ticket, new Outcome<Lookup<string>>.Ok(Lookup.FromNullable<string>(null)));

        UnionAsserts.GetCase<Resolution<string>.Missing>(resolved);
        Assert.False(intent.IsResolving);
    }

    [Fact]
    public void EndResolving_LookupFailed_IsFailedWithTheError()
    {
        var intent = new UserIntent();
        var error = new InvalidOperationException("unreadable");
        var ticket = intent.BeginResolving();

        var resolved = intent.EndResolving<string>(ticket, new Outcome<Lookup<string>>.Failed(error));

        Assert.Same(error, UnionAsserts.GetCase<Resolution<string>.Failed>(resolved).Error);
        Assert.False(intent.IsResolving);
    }

    [Fact]
    public void EndResolving_ValueTypeLookup_FindsTheValueOrNothing()
    {
        var intent = new UserIntent();

        var foundTicket = intent.BeginResolving();
        var found = intent.EndResolving<int>(foundTicket, new Outcome<Lookup<int>>.Ok(Lookup.FromNullable<int>(7)));
        var missingTicket = intent.BeginResolving();
        var missing = intent.EndResolving<int>(missingTicket, new Outcome<Lookup<int>>.Ok(Lookup.FromNullable<int>(null)));

        Assert.Equal(7, UnionAsserts.GetCase<Resolution<int>.Found>(found).Value);
        UnionAsserts.GetCase<Resolution<int>.Missing>(missing);
    }

    [Fact]
    public void EndResolving_AnotherRequestBeganMeanwhile_IsStaleAndLeavesThatRequestAlone()
    {
        var intent = new UserIntent();
        var ticket = intent.BeginResolving();
        var next = intent.Begin();
        intent.StartResolving(next);

        var resolved = intent.EndResolving<string>(ticket, new Outcome<Lookup<string>>.Ok(Lookup.FromNullable<string>("place")));

        UnionAsserts.GetCase<Resolution<string>.Stale>(resolved);
        Assert.True(intent.IsResolving);
        Assert.True(intent.IsCurrent(next));
    }
}

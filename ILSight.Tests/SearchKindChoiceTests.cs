using ILSight.Core;

namespace ILSight.Tests;

public sealed class SearchKindChoiceTests
{
    [Fact]
    public void IsChecked_AllPicked_NoKindIsChecked()
    {
        Assert.False(SearchKindChoice.IsChecked(SearchKinds.All, SearchKinds.Types));
        Assert.False(SearchKindChoice.IsChecked(SearchKinds.All, SearchKinds.Namespaces));
    }

    [Fact]
    public void IsChecked_SomeKindsPicked_OnlyThoseAreChecked()
    {
        var kinds = SearchKinds.Types | SearchKinds.Fields;

        Assert.True(SearchKindChoice.IsChecked(kinds, SearchKinds.Types));
        Assert.True(SearchKindChoice.IsChecked(kinds, SearchKinds.Fields));
        Assert.False(SearchKindChoice.IsChecked(kinds, SearchKinds.Methods));
    }

    [Fact]
    public void Toggle_KindCheckedWhileAllPicked_PicksItAlone()
    {
        var kinds = SearchKindChoice.Toggle(SearchKinds.All, SearchKinds.Methods, on: true);

        Assert.Equal(SearchKinds.Methods, kinds);
    }

    [Fact]
    public void Toggle_KindChecked_AddsItToThosePicked()
    {
        var kinds = SearchKindChoice.Toggle(SearchKinds.Types, SearchKinds.Literals, on: true);

        Assert.Equal(SearchKinds.Types | SearchKinds.Literals, kinds);
    }

    [Fact]
    public void Toggle_KindUnchecked_RemovesItFromThosePicked()
    {
        var kinds = SearchKindChoice.Toggle(SearchKinds.TypesAndMembers, SearchKinds.Events, on: false);

        Assert.Equal(SearchKinds.Types | SearchKinds.Methods | SearchKinds.Fields | SearchKinds.Properties, kinds);
    }

    [Fact]
    public void Toggle_LastKindUnchecked_GoesBackToAll()
    {
        var kinds = SearchKindChoice.Toggle(SearchKinds.Resources, SearchKinds.Resources, on: false);

        Assert.Equal(SearchKinds.All, kinds);
    }

    [Fact]
    public void Toggle_EveryKindCheckedInTurn_EndsWithAllPicked()
    {
        var kinds = SearchKindChoice.Toggle(SearchKinds.All, SearchKinds.Types, on: true);
        foreach (var kind in Enum.GetValues<SearchKinds>().Where(k => k != SearchKinds.Types && int.PopCount((int)k) == 1))
        {
            kinds = SearchKindChoice.Toggle(kinds, kind, on: true);
        }

        Assert.Equal(SearchKinds.All, kinds);
        Assert.False(SearchKindChoice.IsChecked(kinds, SearchKinds.Types));
    }
}

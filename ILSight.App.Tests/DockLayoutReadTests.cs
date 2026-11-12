using ILSight.SettingArea;

namespace ILSight.App.Tests;

public sealed class DockLayoutReadTests
{
    private static DockLayoutJson BuildSplitLayout(int first, int second, int nextId)
    {
        return new DockLayoutJson
        {
            Root = new DockSplitJson
            {
                Id = 1,
                First = new DockGroupJson { Id = first, Panels = [DockPanels.Assemblies] },
                Second = new DockGroupJson { Id = second, Panels = [DockPanels.Search] },
            },
            Windows = [],
            NextId = nextId,
        };
    }

    [Fact]
    public void A_layout_with_two_parts_under_one_id_reads_as_ids_repeated()
    {
        var read = DockJson.ToLayout(BuildSplitLayout(2, 2, nextId: 3));

        Assert.IsType<DockLayoutRead.IdsRepeated>(read);
    }

    [Fact]
    public void A_window_with_the_id_of_a_node_reads_as_ids_repeated()
    {
        var json = BuildSplitLayout(2, 3, nextId: 4);
        json.Windows = [new DockWindowJson { Id = 3, Root = new DockGroupJson { Id = 4, Panels = [DockPanels.Bookmarks] } }];

        var read = DockJson.ToLayout(json);

        Assert.IsType<DockLayoutRead.IdsRepeated>(read);
    }

    [Fact]
    public void A_layout_holding_a_part_as_null_reads_as_parts_missing()
    {
        var json = BuildSplitLayout(2, 3, nextId: 4);
        json.Windows = [null];

        var read = DockJson.ToLayout(json);

        Assert.IsType<DockLayoutRead.PartsMissing>(read);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(0)]
    public void A_next_id_at_or_below_an_id_in_use_is_raised_above_the_highest(int nextId)
    {
        var json = BuildSplitLayout(2, 3, nextId);
        json.Windows = [new DockWindowJson { Id = 5, Root = new DockGroupJson { Id = 4, Panels = [DockPanels.Bookmarks] } }];

        var read = DockJson.ToLayout(json);

        Assert.Equal(6, Assert.IsType<DockLayoutRead.Parsed>(read).Layout.NextId);
    }

    [Fact]
    public void A_next_id_above_every_id_in_use_is_kept()
    {
        var read = DockJson.ToLayout(BuildSplitLayout(2, 3, nextId: 9));

        Assert.Equal(9, Assert.IsType<DockLayoutRead.Parsed>(read).Layout.NextId);
    }

    [Fact]
    public void A_layout_whose_parts_have_no_ids_yet_numbers_from_one()
    {
        var read = DockJson.ToLayout(new DockLayoutJson { NextId = 0 });

        Assert.Equal(1, Assert.IsType<DockLayoutRead.Parsed>(read).Layout.NextId);
    }
}

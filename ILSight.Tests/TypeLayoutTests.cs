using ZGF.Geometry;
using ILSight.Core;
using ILSight.Core.Diagrams;

namespace ILSight.Tests;

public sealed class TypeLayoutTests
{
    private const string ZooNamespace = Trees.FixturesNamespace;
    private const float Near = 1.5f;

    private static async Task<CodeReference.Entity> ZooAsync(string type)
    {
        return (await Trees.TestAssembly.ReadRootAsync().GetChildAsync(ZooNamespace, type)).Definition;
    }

    private static readonly SharedRead<ZooTypes> s_zoo = new(ReadZooAsync);

    /// <summary>The zoo's types the layouts arrange, and the links between them.</summary>
    private sealed class ZooTypes
    {
        public required CodeReference.Entity Animal { get; init; }

        public required CodeReference.Entity Lion { get; init; }

        public required CodeReference.Entity Parrot { get; init; }

        public required CodeReference.Entity Keeper { get; init; }

        public TypeLink LionIsAnimal => new TypeLink.Inherits { From = Lion, To = Animal };

        public TypeLink ParrotIsAnimal => new TypeLink.Inherits { From = Parrot, To = Animal };

        public TypeLink KeeperHoldsAnimals => new TypeLink.HoldsMany { From = Keeper, To = Animal, Property = "Animals" };
    }

    private static async Task<ZooTypes> ReadZooAsync()
    {
        return new ZooTypes
        {
            Animal = await ZooAsync("Animal"),
            Lion = await ZooAsync("Lion"),
            Parrot = await ZooAsync("Parrot"),
            Keeper = await ZooAsync("Keeper"),
        };
    }

    private static DiagramMeasures Zoo4(ZooTypes zoo)
    {
        MeasuredCard[] cards =
        [
            new MeasuredCard { Type = zoo.Animal, Width = 160f, Height = 120f },
            new MeasuredCard { Type = zoo.Lion, Width = 140f, Height = 80f },
            new MeasuredCard { Type = zoo.Parrot, Width = 120f, Height = 60f },
            new MeasuredCard { Type = zoo.Keeper, Width = 150f, Height = 90f },
        ];
        MeasuredLink[] links =
        [
            new MeasuredLink { Link = zoo.LionIsAnimal, LabelWidth = 0f, LabelHeight = 0f },
            new MeasuredLink { Link = zoo.ParrotIsAnimal, LabelWidth = 0f, LabelHeight = 0f },
            new MeasuredLink { Link = zoo.KeeperHoldsAnimals, LabelWidth = 60f, LabelHeight = 16f },
        ];
        return new DiagramMeasures { Cards = cards, Links = links };
    }

    private static (PointF TopLeft, MeasuredCard Card) Placed(TypeLayout layout, DiagramMeasures measures, CodeReference.Entity type)
    {
        var card = measures.Cards.Single(c => c.Type == type);
        return (layout.Places[type], card);
    }

    private static bool IsOnBorder(PointF point, PointF topLeft, MeasuredCard card)
    {
        var inside = point.X > topLeft.X - Near && point.X < topLeft.X + card.Width + Near && point.Y > topLeft.Y - Near && point.Y < topLeft.Y + card.Height + Near;
        var nearSide = MathF.Abs(point.X - topLeft.X) < Near || MathF.Abs(point.X - topLeft.X - card.Width) < Near;
        var nearEdge = MathF.Abs(point.Y - topLeft.Y) < Near || MathF.Abs(point.Y - topLeft.Y - card.Height) < Near;
        return inside && (nearSide || nearEdge);
    }

    [Fact]
    public async Task Every_card_is_placed_and_the_types_derived_from_sit_above_those_deriving_from_them()
    {
        var zoo = await s_zoo.ReadAsync();
        var measures = Zoo4(zoo);

        var layout = DiagramLayouts.Arrange(measures);

        Assert.Equal(4, layout.Places.Count);
        var animal = Placed(layout, measures, zoo.Animal);
        var lion = Placed(layout, measures, zoo.Lion);
        Assert.True(animal.TopLeft.Y + animal.Card.Height <= lion.TopLeft.Y);
    }

    [Fact]
    public async Task No_two_cards_overlap_and_the_layout_holds_them_all_from_its_top_left()
    {
        var zoo = await s_zoo.ReadAsync();
        var measures = Zoo4(zoo);

        var layout = DiagramLayouts.Arrange(measures);

        var boxes = measures.Cards
            .Select(c => new RectF(layout.Places[c.Type].X, layout.Places[c.Type].Y, c.Width, c.Height))
            .ToList();
        for (var i = 0; i < boxes.Count; i++)
        {
            for (var j = i + 1; j < boxes.Count; j++)
            {
                Assert.False(boxes[i].Intersects(boxes[j]));
            }
        }
        Assert.Equal(0f, boxes.Min(static b => b.Left), 0.01f);
        Assert.Equal(0f, boxes.Min(static b => b.Bottom), 0.01f);
        Assert.Equal(layout.Width, boxes.Max(static b => b.Right), 0.01f);
        Assert.Equal(layout.Height, boxes.Max(static b => b.Top), 0.01f);
    }

    [Fact]
    public async Task A_route_runs_from_its_source_card_to_an_arrowhead_whose_tip_touches_its_target()
    {
        var zoo = await s_zoo.ReadAsync();
        var measures = Zoo4(zoo);

        var layout = DiagramLayouts.Arrange(measures);

        var route = layout.Routes.Single(r => r.Link == zoo.LionIsAnimal);
        var lion = Placed(layout, measures, zoo.Lion);
        var animal = Placed(layout, measures, zoo.Animal);
        Assert.True(IsOnBorder(route.Pieces[0].Start, lion.TopLeft, lion.Card));
        Assert.True(IsOnBorder(route.Tip, animal.TopLeft, animal.Card));
        var arrowBase = route.Pieces[^1].End;
        var length = MathF.Sqrt(arrowBase.DistanceSqTo(route.Tip));
        Assert.Equal(TypeLayout.ArrowLength, length, 0.5f);
    }

    [Fact]
    public async Task A_route_to_a_type_held_leaves_room_for_a_diamond_on_the_card_holding_it()
    {
        var zoo = await s_zoo.ReadAsync();
        var measures = Zoo4(zoo);

        var layout = DiagramLayouts.Arrange(measures);

        var held = layout.Routes.Single(r => r.Link == zoo.KeeperHoldsAnimals);
        var keeper = Placed(layout, measures, zoo.Keeper);
        Assert.NotNull(held.SourceTip);
        Assert.True(IsOnBorder(held.SourceTip.Value, keeper.TopLeft, keeper.Card));
        var length = MathF.Sqrt(held.SourceTip.Value.DistanceSqTo(held.Pieces[0].Start));
        Assert.Equal(TypeLayout.DiamondLength, length, 0.5f);
        Assert.Null(layout.Routes.Single(r => r.Link == zoo.LionIsAnimal).SourceTip);
    }

    [Fact]
    public async Task A_labelled_link_has_its_label_placed_and_others_have_none()
    {
        var zoo = await s_zoo.ReadAsync();
        var layout = DiagramLayouts.Arrange(Zoo4(zoo));

        Assert.NotNull(layout.Routes.Single(r => r.Link == zoo.KeeperHoldsAnimals).LabelCenter);
        Assert.Null(layout.Routes.Single(r => r.Link == zoo.ParrotIsAnimal).LabelCenter);
    }

    [Fact]
    public async Task A_link_to_a_card_not_measured_is_left_out()
    {
        var zoo = await s_zoo.ReadAsync();
        var measures = new DiagramMeasures
        {
            Cards = [new MeasuredCard
            {
                Type = zoo.Lion,
                Width = 100f,
                Height = 50f,
            }],
            Links = [new MeasuredLink
            {
                Link = zoo.LionIsAnimal,
                LabelWidth = 0f,
                LabelHeight = 0f,
            }],
        };

        var layout = DiagramLayouts.Arrange(measures);

        Assert.Equal(new PointF(0f, 0f), layout.Places[zoo.Lion]);
        Assert.Empty(layout.Routes);
    }

    [Fact]
    public async Task A_type_holding_itself_is_routed_as_a_loop()
    {
        var zoo = await s_zoo.ReadAsync();
        var self = new TypeLink.HoldsOne { From = zoo.Keeper, To = zoo.Keeper, Property = "Next" };
        var measures = new DiagramMeasures
        {
            Cards = [new MeasuredCard
            {
                Type = zoo.Keeper,
                Width = 100f,
                Height = 50f,
            }],
            Links = [new MeasuredLink
            {
                Link = self,
                LabelWidth = 30f,
                LabelHeight = 14f,
            }],
        };

        var layout = DiagramLayouts.Arrange(measures);

        Assert.NotEmpty(Assert.Single(layout.Routes).Pieces);
    }

    [Fact]
    public void Nothing_to_lay_out_is_an_empty_layout()
    {
        var layout = DiagramLayouts.Arrange(new DiagramMeasures { Cards = [], Links = [] });

        Assert.Empty(layout.Places);
        Assert.Equal(0f, layout.Width);
    }

    [Fact]
    public async Task Laying_out_stops_when_cancelled()
    {
        var zoo = await s_zoo.ReadAsync();
        using var cancel = new CancellationTokenSource();
        await cancel.CancelAsync();

        Assert.ThrowsAny<OperationCanceledException>(() => DiagramLayouts.Arrange(Zoo4(zoo), cancel.Token));
    }
}

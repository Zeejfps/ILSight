using ZGF.Gui.Immediate;
using ILSight.Core;
using ILSight.Core.Diagrams;
using ILSight.DiagramArea;

namespace ILSight.App.Tests;

public sealed class LayoutTransitionTests
{
    private const float HalfWay = 0.15f;
    private const float Past = 1f;

    private static readonly Lazy<LoadedAssembly> s_fixtures = new(() => new AssemblyList { Runtime = TestRuntime.Installed }.Open(AppStateDriver.FixturePath));

    private static async Task<CodeReference.Entity> TypeOfAsync(string type)
    {
        return (await s_fixtures.Value.ReadRootAsync().GetChildAsync(AppStateDriver.FixturesNamespace, type)).Definition;
    }

    private static readonly SharedRead<VehicleTypes> s_vehicles = new(ReadVehiclesAsync);

    private sealed class VehicleTypes
    {
        public required CodeReference.Entity Car { get; init; }

        public required CodeReference.Entity Vehicle { get; init; }

        public TypeLink CarIsVehicle => new TypeLink.Inherits { From = Car, To = Vehicle };
    }

    private static async Task<VehicleTypes> ReadVehiclesAsync()
    {
        return new VehicleTypes
        {
            Car = await TypeOfAsync("Car"),
            Vehicle = await TypeOfAsync("Vehicle"),
        };
    }

    private static TypeLayout Arrange(VehicleTypes vehicles, float vehicleHeight, bool withCar = true)
    {
        var cards = new List<MeasuredCard>
        {
            new MeasuredCard { Type = vehicles.Vehicle, Width = 120f, Height = vehicleHeight },
        };
        if (withCar)
        {
            cards.Add(new MeasuredCard { Type = vehicles.Car, Width = 100f, Height = 60f });
        }

        return DiagramLayouts.Arrange(new DiagramMeasures
        {
            Cards = cards,
            Links = [new MeasuredLink { Link = vehicles.CarIsVehicle, LabelWidth = 0f, LabelHeight = 0f }],
        });
    }

    [Fact]
    public async Task The_first_layout_is_drawn_at_once()
    {
        var vehicles = await s_vehicles.ReadAsync();
        var layout = Arrange(vehicles, 80f);
        var transition = new LayoutTransition();

        Assert.True(transition.TryFollowLayout(layout));

        Assert.False(transition.IsMoving);
        Assert.Equal(layout.Places[vehicles.Car], transition.FindCardPlace(vehicles.Car));
        Assert.False(transition.TryFollowLayout(layout));
    }

    [Fact]
    public async Task Cards_glide_from_their_old_places_to_their_new_ones()
    {
        var vehicles = await s_vehicles.ReadAsync();
        var before = Arrange(vehicles, 80f);
        var after = Arrange(vehicles, 300f);
        var transition = new LayoutTransition();
        transition.TryFollowLayout(before);

        transition.TryFollowLayout(after);
        transition.Advance(HalfWay);

        Assert.True(transition.IsMoving);
        var expected = DiagramLayouts.Lerp(before.Places[vehicles.Car], after.Places[vehicles.Car], Ease.OutCubic(0.5f));
        var drawn = Assert.NotNull(transition.FindCardPlace(vehicles.Car));
        Assert.Equal(expected.X, drawn.X, 0.01f);
        Assert.Equal(expected.Y, drawn.Y, 0.01f);
        transition.Advance(Past);
        Assert.False(transition.IsMoving);
        Assert.Equal(after.Places[vehicles.Car], transition.FindCardPlace(vehicles.Car));
    }

    [Fact]
    public async Task A_card_the_old_layout_lacked_appears_in_its_new_place()
    {
        var vehicles = await s_vehicles.ReadAsync();
        var transition = new LayoutTransition();
        transition.TryFollowLayout(Arrange(vehicles, 80f, withCar: false));
        var after = Arrange(vehicles, 80f);

        transition.TryFollowLayout(after);
        transition.Advance(HalfWay);

        Assert.Equal(after.Places[vehicles.Car], transition.FindCardPlace(vehicles.Car));
    }

    [Fact]
    public async Task A_route_bends_from_its_old_shape_and_ends_as_the_new_one()
    {
        var vehicles = await s_vehicles.ReadAsync();
        var before = Arrange(vehicles, 80f);
        var after = Arrange(vehicles, 300f);
        var transition = new LayoutTransition();
        transition.TryFollowLayout(before);
        transition.TryFollowLayout(after);
        var route = after.Routes.Single();
        var old = before.Routes.Single();

        var starting = transition.ShapeRoute(route);

        Assert.Equal(Math.Max(route.Pieces.Count, old.Pieces.Count), starting.Pieces.Count);
        Assert.Equal(old.Pieces[0].Start, starting.Pieces[0].Start);
        Assert.Equal(old.Tip, starting.Tip);
        transition.Advance(Past);
        Assert.Same(route, transition.ShapeRoute(route));
    }
}

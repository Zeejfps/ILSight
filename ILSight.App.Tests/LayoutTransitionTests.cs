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

    private static CodeReference.Entity TypeOf(string type)
    {
        return s_fixtures.Value.Root.Child(AppStateDriver.FixturesNamespace, type).Definition;
    }

    private static readonly CodeReference.Entity Car = TypeOf("Car");
    private static readonly CodeReference.Entity Vehicle = TypeOf("Vehicle");
    private static readonly TypeLink CarIsVehicle = new TypeLink.Inherits { From = Car, To = Vehicle };

    private static TypeLayout Arrange(float vehicleHeight, bool withCar = true)
    {
        var cards = new List<MeasuredCard> { new MeasuredCard(Vehicle, 120f, vehicleHeight) };
        if (withCar)
        {
            cards.Add(new MeasuredCard(Car, 100f, 60f));
        }

        return DiagramLayouts.Arrange(new DiagramMeasures(cards, [new MeasuredLink(CarIsVehicle, 0f, 0f)]));
    }

    [Fact]
    public void The_first_layout_is_drawn_at_once()
    {
        var layout = Arrange(80f);
        var transition = new LayoutTransition();

        Assert.True(Diagrams.TryFollowLayout(transition, layout));

        Assert.False(transition.IsMoving);
        Assert.Equal(layout.Places[Car], Diagrams.FindCardPlace(transition, Car));
        Assert.False(Diagrams.TryFollowLayout(transition, layout));
    }

    [Fact]
    public void Cards_glide_from_their_old_places_to_their_new_ones()
    {
        var before = Arrange(80f);
        var after = Arrange(300f);
        var transition = new LayoutTransition();
        Diagrams.TryFollowLayout(transition, before);

        Diagrams.TryFollowLayout(transition, after);
        Diagrams.AdvanceTransition(transition, HalfWay);

        Assert.True(transition.IsMoving);
        var expected = DiagramLayouts.Lerp(before.Places[Car], after.Places[Car], Ease.OutCubic(0.5f));
        var drawn = Assert.NotNull(Diagrams.FindCardPlace(transition, Car));
        Assert.Equal(expected.X, drawn.X, 0.01f);
        Assert.Equal(expected.Y, drawn.Y, 0.01f);
        Diagrams.AdvanceTransition(transition, Past);
        Assert.False(transition.IsMoving);
        Assert.Equal(after.Places[Car], Diagrams.FindCardPlace(transition, Car));
    }

    [Fact]
    public void A_card_the_old_layout_lacked_appears_in_its_new_place()
    {
        var transition = new LayoutTransition();
        Diagrams.TryFollowLayout(transition, Arrange(80f, withCar: false));
        var after = Arrange(80f);

        Diagrams.TryFollowLayout(transition, after);
        Diagrams.AdvanceTransition(transition, HalfWay);

        Assert.Equal(after.Places[Car], Diagrams.FindCardPlace(transition, Car));
    }

    [Fact]
    public void A_route_bends_from_its_old_shape_and_ends_as_the_new_one()
    {
        var before = Arrange(80f);
        var after = Arrange(300f);
        var transition = new LayoutTransition();
        Diagrams.TryFollowLayout(transition, before);
        Diagrams.TryFollowLayout(transition, after);
        var route = after.Routes.Single();
        var old = before.Routes.Single();

        var starting = Diagrams.ShapeRoute(transition, route);

        Assert.Equal(Math.Max(route.Pieces.Count, old.Pieces.Count), starting.Pieces.Count);
        Assert.Equal(old.Pieces[0].Start, starting.Pieces[0].Start);
        Assert.Equal(old.Tip, starting.Tip);
        Diagrams.AdvanceTransition(transition, Past);
        Assert.Same(route, Diagrams.ShapeRoute(transition, route));
    }
}

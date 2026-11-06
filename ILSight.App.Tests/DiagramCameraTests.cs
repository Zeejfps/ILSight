using ZGF.Geometry;
using ZGF.Gui.Immediate;

namespace ILSight.App.Tests;

public sealed class DiagramCameraTests
{
    private static readonly UiRect View = new(100f, 50f, 400f, 300f);

    [Fact]
    public void A_point_of_the_diagram_maps_to_the_screen_and_back()
    {
        var camera = new DiagramCamera();
        DiagramCameras.LookFrom(camera, new PointF(10f, 20f));

        var screen = DiagramCameras.ToScreen(camera, new PointF(30f, 60f), View);

        Assert.Equal(new PointF(120f, 90f), screen);
        Assert.Equal(new PointF(30f, 60f), DiagramCameras.ToDiagram(camera, screen, View));
    }

    [Fact]
    public void Zooming_keeps_the_point_under_the_anchor_where_it_is()
    {
        var camera = new DiagramCamera();
        var anchor = new PointF(250f, 120f);
        var held = DiagramCameras.ToDiagram(camera, anchor, View);

        DiagramCameras.ZoomAt(camera, anchor, View, 2f);

        Assert.Equal(2f, camera.Zoom);
        var after = DiagramCameras.ToScreen(camera, held, View);
        Assert.Equal(anchor.X, after.X, 0.001f);
        Assert.Equal(anchor.Y, after.Y, 0.001f);
    }

    [Fact]
    public void Zoom_stays_between_a_tenth_and_four_times_the_diagram_s_size()
    {
        var camera = new DiagramCamera();

        DiagramCameras.ZoomAt(camera, new PointF(), View, 100f);
        Assert.Equal(4f, camera.Zoom);
        Assert.False(camera.CanZoomIn);

        DiagramCameras.ZoomAt(camera, new PointF(), View, 0.0001f);
        Assert.Equal(0.1f, camera.Zoom);
        Assert.False(camera.CanZoomOut);
    }

    [Fact]
    public void Fitting_shrinks_a_large_diagram_into_the_view_and_centers_it_but_never_enlarges_a_small_one()
    {
        var camera = new DiagramCamera();

        DiagramCameras.Fit(camera, 704f, 252f, View);
        Assert.Equal(0.5f, camera.Zoom, 0.001f);
        var center = DiagramCameras.ToScreen(camera, new PointF(352f, 126f), View);
        Assert.Equal(300f, center.X, 0.001f);
        Assert.Equal(200f, center.Y, 0.001f);

        DiagramCameras.Fit(camera, 10f, 10f, View);
        Assert.Equal(1f, camera.Zoom);
    }

    [Fact]
    public void Panning_moves_the_diagram_with_the_pointer_at_any_zoom()
    {
        var camera = new DiagramCamera();
        DiagramCameras.ZoomAt(camera, new PointF(View.X, View.Y), View, 2f);
        var before = DiagramCameras.ToScreen(camera, new PointF(5f, 5f), View);

        DiagramCameras.Pan(camera, 30f, -10f);

        var after = DiagramCameras.ToScreen(camera, new PointF(5f, 5f), View);
        Assert.Equal(before.X + 30f, after.X, 0.001f);
        Assert.Equal(before.Y - 10f, after.Y, 0.001f);
    }
}

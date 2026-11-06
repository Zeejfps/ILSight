using ZGF.Geometry;
using ZGF.Gui.Immediate;

namespace ILSight;

/// <summary>Where a diagram is looked at from and how near: the point of the diagram at the top-left of
/// the view, and the zoom. <see cref="Changes"/> counts the moves: each step of the zoom, the wheel, a fit
/// or a centering, and a move followed frame by frame, such as a drag, once when it ends.</summary>
public sealed class DiagramCamera
{
    /// <summary>The point of the diagram at the top-left of the view.</summary>
    public PointF Corner { get; set; }

    public CameraZoom Level { get; set; }

    /// <summary>Whether a move is being followed frame by frame.</summary>
    public bool IsMoving { get; set; }

    public int Changes { get; set; }

    public float Zoom => Level.Value;

    public int Percent => (int)MathF.Round(Zoom * 100f);

    public bool CanZoomIn => Zoom < CameraZoom.Max;

    public bool CanZoomOut => Zoom > CameraZoom.Min;
}

public static class DiagramCameras
{
    private const float ZoomStep = 1.25f;
    private const float FitMargin = 24f;

    /// <summary>Where <paramref name="point"/> of the diagram is drawn in <paramref name="view"/>.</summary>
    public static PointF ToScreen(DiagramCamera camera, PointF point, UiRect view)
    {
        return new PointF(view.X + (point.X - camera.Corner.X) * camera.Zoom, view.Y + (point.Y - camera.Corner.Y) * camera.Zoom);
    }

    /// <summary>The point of the diagram drawn at <paramref name="point"/> of <paramref name="view"/>.</summary>
    public static PointF ToDiagram(DiagramCamera camera, PointF point, UiRect view)
    {
        return new PointF(camera.Corner.X + (point.X - view.X) / camera.Zoom, camera.Corner.Y + (point.Y - view.Y) / camera.Zoom);
    }

    /// <summary>Looks at the diagram with <paramref name="corner"/> at the view's top-left, as one frame of
    /// a move that <see cref="FinishMoving"/> counts once it ends.</summary>
    public static void LookFrom(DiagramCamera camera, PointF corner)
    {
        if (corner == camera.Corner)
            return;
        camera.Corner = corner;
        camera.IsMoving = true;
    }

    /// <summary>Counts the move <see cref="LookFrom"/> followed as one change, once it ended.</summary>
    public static void FinishMoving(DiagramCamera camera)
    {
        if (!camera.IsMoving)
            return;
        camera.IsMoving = false;
        camera.Changes++;
    }

    /// <summary>Looks at the diagram from <paramref name="corner"/> at <paramref name="zoom"/>, as the
    /// settings kept it.</summary>
    public static void Restore(DiagramCamera camera, PointF corner, float zoom)
    {
        camera.Corner = corner;
        camera.Level = CameraZoom.Clamp(zoom);
    }

    /// <summary>Moves the diagram by <paramref name="dx"/> and <paramref name="dy"/> on the screen.</summary>
    public static void Pan(DiagramCamera camera, float dx, float dy)
    {
        camera.Corner = new PointF(camera.Corner.X - dx / camera.Zoom, camera.Corner.Y - dy / camera.Zoom);
        camera.Changes++;
    }

    /// <summary>Zooms by <paramref name="factor"/>, keeping the point of the diagram under
    /// <paramref name="anchor"/> of <paramref name="view"/> where it is.</summary>
    public static void ZoomAt(DiagramCamera camera, PointF anchor, UiRect view, float factor)
    {
        var held = ToDiagram(camera, anchor, view);
        camera.Level = CameraZoom.Clamp(camera.Zoom * factor);
        camera.Corner = new PointF(held.X - (anchor.X - view.X) / camera.Zoom, held.Y - (anchor.Y - view.Y) / camera.Zoom);
        camera.Changes++;
    }

    public static void ZoomIn(DiagramCamera camera, UiRect view)
    {
        ZoomAt(camera, FindCenter(view), view, ZoomStep);
    }

    public static void ZoomOut(DiagramCamera camera, UiRect view)
    {
        ZoomAt(camera, FindCenter(view), view, 1f / ZoomStep);
    }

    /// <summary>Zooms out until a diagram <paramref name="width"/> by <paramref name="height"/> fits
    /// <paramref name="view"/> with a margin, but never in past its own size, and centers it.</summary>
    public static void Fit(DiagramCamera camera, float width, float height, UiRect view)
    {
        var across = (view.Width - FitMargin * 2) / MathF.Max(width, 1f);
        var down = (view.Height - FitMargin * 2) / MathF.Max(height, 1f);
        var fitting = MathF.Min(MathF.Min(across, down), 1f);
        camera.Level = CameraZoom.Clamp(fitting);
        CenterOn(camera, new PointF(width / 2, height / 2), view);
    }

    /// <summary>Moves the diagram so that <paramref name="point"/> of it is drawn at the middle of <paramref name="view"/>.</summary>
    public static void CenterOn(DiagramCamera camera, PointF point, UiRect view)
    {
        camera.Corner = new PointF(point.X - view.Width / 2 / camera.Zoom, point.Y - view.Height / 2 / camera.Zoom);
        camera.Changes++;
    }

    /// <summary>Whether the part of the diagram <paramref name="width"/> by <paramref name="height"/> from
    /// <paramref name="topLeft"/> is drawn wholly inside <paramref name="view"/>.</summary>
    public static bool Shows(DiagramCamera camera, PointF topLeft, float width, float height, UiRect view)
    {
        var start = ToScreen(camera, topLeft, view);
        var end = ToScreen(camera, new PointF(topLeft.X + width, topLeft.Y + height), view);
        var across = start.X >= view.X && end.X <= view.Right;
        var down = start.Y >= view.Y && end.Y <= view.Bottom;
        return across && down;
    }

    private static PointF FindCenter(UiRect view)
    {
        return new PointF(view.X + view.Width / 2, view.Y + view.Height / 2);
    }
}

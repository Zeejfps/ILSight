using ZGF.Geometry;

namespace ILSight.DiagramCameraArea;

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

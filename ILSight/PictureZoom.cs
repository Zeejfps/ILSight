namespace ILSight;

/// <summary>How far a picture is zoomed: one of the steps offered, and its own size when nothing was set,
/// since it counts from there.</summary>
public readonly record struct PictureZoom
{
    private const int OwnSizeStep = 2;

    private static readonly float[] Steps = [0.25f, 0.5f, 1f, 2f, 4f, 8f, 16f, 32f];

    private readonly int _fromOwnSize;

    private PictureZoom(int fromOwnSize)
    {
        _fromOwnSize = fromOwnSize;
    }

    private int Step => OwnSizeStep + _fromOwnSize;

    public float Scale => Steps[Step];

    public int Percent => (int)(Scale * 100f);

    public bool CanZoomIn => Step < Steps.Length - 1;

    public bool CanZoomOut => Step > 0;

    public bool IsOwnSize => _fromOwnSize == 0;

    public PictureZoom StepIn()
    {
        return CanZoomIn ? new PictureZoom(_fromOwnSize + 1) : this;
    }

    public PictureZoom StepOut()
    {
        return CanZoomOut ? new PictureZoom(_fromOwnSize - 1) : this;
    }
}

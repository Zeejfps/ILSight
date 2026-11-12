namespace ILSight.DiagramViewport;

/// <summary>How near a diagram is looked at: between a tenth of its own size and four times it, and its own
/// size when nothing was set.</summary>
public readonly record struct CameraZoom
{
    public const float Min = 0.1f;
    public const float Max = 4f;
    private const float OwnSize = 1f;

    private readonly float _zoom;

    private CameraZoom(float zoom)
    {
        _zoom = zoom;
    }

    /// <summary>Zero is no zoom, so it stands for the default value, which skips the constructor.</summary>
    public float Value => _zoom == 0f ? OwnSize : _zoom;

    public static CameraZoom Clamp(float zoom)
    {
        if (float.IsNaN(zoom))
        {
            return default;
        }

        return new CameraZoom(Math.Clamp(zoom, Min, Max));
    }
}

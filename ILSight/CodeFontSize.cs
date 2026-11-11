namespace ILSight;

/// <summary>A size to draw code at that the user chose, in the range the options offer.</summary>
public readonly record struct CodeFontSize
{
    public const float Min = 8f;
    public const float Max = 32f;

    private readonly float _points;

    private CodeFontSize(float points)
    {
        _points = points;
    }

    /// <summary>Zero is no size, so it stands for the default value, which skips the constructor.</summary>
    public float Points => _points == 0f ? Min : _points;

    public static CodeFontSize Clamp(float points)
    {
        if (float.IsNaN(points))
        {
            return default;
        }

        return new CodeFontSize(Math.Clamp(points, Min, Max));
    }
}

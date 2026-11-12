namespace ILSight;

/// <summary>A place in a code view's text, as a settings file can hold it.</summary>
internal readonly record struct TextPositionJson
{
    public int Line { get; init; }

    public int Column { get; init; }
}

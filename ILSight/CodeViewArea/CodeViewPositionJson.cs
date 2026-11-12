
namespace ILSight.CodeViewArea;

/// <summary>Where a code view stood, as a settings file can hold it.</summary>
internal readonly record struct CodeViewPositionJson
{
    public int TopLine { get; init; }

    public float ScrollX { get; init; }

    public TextPositionJson Anchor { get; init; }

    public TextPositionJson Caret { get; init; }
}

namespace ILSight;

/// <summary>What a readable icon theme file sets.</summary>
public sealed record IconThemeContents
{
    /// <summary>The fonts the icons can be drawn in, by id.</summary>
    public required IReadOnlyDictionary<string, IconFont> Fonts { get; init; }

    /// <summary>The icons, by key.</summary>
    public required IReadOnlyDictionary<string, IconValue> Icons { get; init; }
}

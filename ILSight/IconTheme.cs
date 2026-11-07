using ZGF.Gui.Immediate;

namespace ILSight;

/// <summary>An icon theme as resolved over the themes it extends: what each key draws, the fonts its
/// glyphs are drawn in, and each key's icon as a theme file writes it, the reference theme's where no
/// theme in the chain sets the key.</summary>
public sealed class IconTheme
{
    /// <summary>What each key draws.</summary>
    public required AppIcons Icons { get; init; }

    /// <summary>The icons by key, falling back to the reference theme's.</summary>
    public required IconSet Set { get; init; }

    /// <summary>The fonts the glyphs are drawn in, which the screen must have registered.</summary>
    public required IReadOnlyList<FontFace> Fonts { get; init; }

    /// <summary>Each key's icon as a theme file writes it.</summary>
    public required IReadOnlyDictionary<string, IconValue> Values { get; init; }
}

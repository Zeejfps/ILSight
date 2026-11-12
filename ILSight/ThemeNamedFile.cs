namespace ILSight;

/// <summary>A file a theme file names (an icon theme's font or picture), by the path the theme writes, with what of the
/// theme names it and the line it does on, which what is wrong with the file is told at.</summary>
public sealed record ThemeNamedFile
{
    public required string Path { get; init; }

    public required string Subject { get; init; }

    public required int Line { get; init; }
}

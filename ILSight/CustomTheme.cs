namespace ILSight;

/// <summary>A theme file of the user's, written by <see cref="UserThemes{TFile, TContents, TTheme}.CreateCustomTheme"/>:
/// the id of its theme and where the file is.</summary>
public sealed record CustomTheme<TFile>
    where TFile : IThemeFile<TFile>
{
    public required ThemeId<TFile> Id { get; init; }

    public required string Path { get; init; }
}

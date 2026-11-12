namespace ILSight;

/// <summary>The theme <see cref="Id"/> a theme file extends, written on <see cref="Line"/>.</summary>
public sealed record ThemeParent<TFile> where TFile : IThemeFile<TFile>
{
    public required ThemeId<TFile> Id { get; init; }

    public required int Line { get; init; }
}

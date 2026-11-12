namespace ILSight;

/// <summary>The theme <see cref="Id"/> a theme file extends, written on <see cref="Line"/>.</summary>
public sealed record ThemeParent<TFile>(ThemeId<TFile> Id, int Line) where TFile : IThemeFile<TFile>;

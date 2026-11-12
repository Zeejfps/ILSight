namespace ILSight;

/// <summary>Where an icon theme file's fonts and pictures are read from, by the paths the file names
/// them by, and the font family each font is drawn as.</summary>
/// <param name="Read">The file at a path, as the theme file writes it.</param>
/// <param name="GetFamily">The family each font is drawn as.</param>
public sealed record IconAssets(Func<string, IconAsset> Read, IconFontFamily GetFamily);

namespace ILSight;

/// <summary>An installed font to draw code in: its family's <paramref name="Family"/> name and the file
/// of its regular face, kept so the font loads at startup without listing every installed one.</summary>
public sealed record CodeFont(string Family, string Path, int FaceIndex);

namespace ILSight;

/// <summary>The family the font <paramref name="fontId"/>, read from <paramref name="path"/>, is drawn as,
/// which no other font may be drawn as.</summary>
public delegate string IconFontFamily(string fontId, string path);

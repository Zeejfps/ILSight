using ZGF.Gui.Immediate;

namespace ILSight.ILSightAppArea;

/// <summary>Reads the faces code is drawn with, as <see cref="MonoFonts.Code"/>: the bundled one, and each installed font
/// chosen this session, read from its file once and kept. The window builds one and holds it.</summary>
[Edge]
internal sealed class CodeFontReader
{
    private readonly Dictionary<CodeFont, FontFace> _readFaces = new();

    /// <param name="bundled">The bundled face, which code is drawn with when no font is chosen or the chosen one's file
    /// cannot be read.</param>
    public CodeFontReader(FontFace bundled)
    {
        Bundled = bundled;
    }

    /// <summary>Registered as the window starts, before any chosen font is read.</summary>
    public FontFace Bundled { get; }

    /// <summary>The face to draw code with when the font chosen in <paramref name="display"/> changed since
    /// <paramref name="shown"/> last took it, else null. A chosen font whose file cannot be read is given up for the
    /// bundled one: <paramref name="display"/> is set back to it.</summary>
    public FontFace? TakeFace(ScreenDisplay display, ShownChanges shown)
    {
        var chosen = display.Options.Font;
        if (!shown.TryTakeCodeFont(chosen))
        {
            return null;
        }

        var face = ReadFace(chosen);
        if (face != null)
        {
            return face;
        }

        display.SetCodeFont(null);
        shown.TryTakeCodeFont(null);
        return Bundled;
    }

    // The face of font, read from its file the first time it is asked for; null when the file cannot be read.
    private FontFace? ReadFace(CodeFont? font)
    {
        if (font is null)
        {
            return Bundled;
        }

        if (_readFaces.TryGetValue(font, out var face))
        {
            return face;
        }

        var data = ReadFontFile(font.Path);
        if (data is null)
        {
            return null;
        }

        face = new FontFace(MonoFonts.Code, data, FaceIndex: font.FaceIndex);
        _readFaces[font] = face;
        return face;
    }

    private static byte[]? ReadFontFile(string path)
    {
        try
        {
            return File.ReadAllBytes(path);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }
}

namespace ILSight.Core;

/// <summary>The absolute path of a file, which so always lies in a folder: what
/// <see cref="Path.GetDirectoryName(string)"/> leaves open is answered once, here.</summary>
public sealed record FullFilePath
{
    private FullFilePath(string text, string folder)
    {
        Text = text;
        Folder = folder;
    }

    /// <summary>The path, made absolute.</summary>
    public string Text { get; }

    /// <summary>The folder the file is in.</summary>
    public string Folder { get; }

    /// <summary>The path <paramref name="text"/> is, made absolute; or null when it is not fully qualified,
    /// ends in a separator, as a root or a folder does, or holds what no path can.</summary>
    public static FullFilePath? Parse(string text)
    {
        if (!Path.IsPathFullyQualified(text))
        {
            return null;
        }

        string full;
        try
        {
            full = Path.GetFullPath(text);
        }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
        if (Path.EndsInDirectorySeparator(full))
        {
            return null;
        }

        var folder = Path.GetDirectoryName(full);
        if (folder is null)
        {
            return null;
        }

        return new FullFilePath(full, folder);
    }

    /// <summary>The path <paramref name="text"/>, which the program made itself from paths it parsed and
    /// must be one.</summary>
    public static FullFilePath Of(string text)
    {
        var path = Parse(text);
        if (path is null)
        {
            throw new ArgumentException($"'{text}' is no absolute path of a file.", nameof(text));
        }

        return path;
    }

    public override string ToString()
    {
        return Text;
    }
}

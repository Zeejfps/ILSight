using System.Diagnostics.CodeAnalysis;
using ILSight.Core;

namespace ILSight.UserArea;

/// <summary>A folder of the user's theme files on disk: the I/O the adapters of both kinds of theme do on theirs,
/// each holding one. Lists and reads its files, makes it with a schema and a guide in it, writes new files, and
/// watches it, telling of a change on the UI thread; and reads the files a theme file names. What the disk refuses
/// comes back as a result. Decides nothing.</summary>
public sealed class ThemeFolder(ThemeFolderWatch watch)
{
    private const string GuideFileName = "README.md";

    /// <summary>The files in <paramref name="folder"/> ending in <paramref name="extension"/>, in the order of their
    /// names, without <paramref name="schemaFileName"/>; none when it is missing or cannot be listed.</summary>
    public List<string> ListThemeFiles(string folder, string extension, string schemaFileName)
    {
        try
        {
            return Directory.EnumerateFiles(folder, "*" + extension)
                .Where(p => !IsNamed(p, schemaFileName))
                .Order(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (IOException)
        {
            return [];
        }
        catch (UnauthorizedAccessException)
        {
            return [];
        }
    }

    private static bool IsNamed(string path, string fileName)
    {
        return string.Equals(Path.GetFileName(path), fileName, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The whole theme file at <paramref name="path"/>, read now, so nothing is left to fail later; why it
    /// cannot be read as <see cref="Outcome{T}.Failed"/>.</summary>
    public Outcome<byte[]> ReadThemeFile(string path)
    {
        try
        {
            return new Outcome<byte[]>.Ok(File.ReadAllBytes(path));
        }
        catch (IOException e)
        {
            return new Outcome<byte[]>.Failed(e);
        }
        catch (UnauthorizedAccessException e)
        {
            return new Outcome<byte[]>.Failed(e);
        }
    }

    /// <summary>The files at <paramref name="paths"/>, which the theme file at <paramref name="themePath"/> names
    /// relative to its folder, each read, or why it cannot be (<see cref="IconAssetFolders.Read"/>).</summary>
    public Dictionary<string, IconAsset> ReadNamedFiles(string themePath, IReadOnlyList<string> paths)
    {
        var folder = FullFilePath.Of(Path.GetFullPath(themePath)).Folder;
        var read = new Dictionary<string, IconAsset>();
        foreach (var path in paths)
        {
            read[path] = IconAssetFolders.Read(folder, path);
        }
        return read;
    }

    /// <summary>Makes <paramref name="folder"/>, with <paramref name="schema"/> in it as
    /// <paramref name="schemaFileName"/> and <paramref name="guide"/> as its README, each written only when it
    /// says something else; false, with why in <paramref name="reason"/>, when it cannot.</summary>
    public bool TryMakeFolder(string folder, string schemaFileName, string schema, string guide, [NotNullWhen(false)] out string? reason)
    {
        try
        {
            Directory.CreateDirectory(folder);
            WriteIfChanged(Path.Combine(folder, schemaFileName), schema);
            WriteIfChanged(Path.Combine(folder, GuideFileName), guide);
            reason = null;
            return true;
        }
        catch (IOException e)
        {
            reason = DescribeFolderFailure(folder, e);
            return false;
        }
        catch (UnauthorizedAccessException e)
        {
            reason = DescribeFolderFailure(folder, e);
            return false;
        }
    }

    private static string DescribeFolderFailure(string folder, Exception e)
    {
        return $"{folder}{Environment.NewLine}{e.Message}";
    }

    private static void WriteIfChanged(string path, string text)
    {
        if (IsWritten(path, text))
        {
            return;
        }

        File.WriteAllText(path, text);
    }

    private static bool IsWritten(string path, string text)
    {
        if (!File.Exists(path))
        {
            return false;
        }

        return File.ReadAllText(path) == text;
    }

    public bool IsFile(string path)
    {
        return File.Exists(path);
    }

    /// <summary>Writes <paramref name="text"/> to a file at <paramref name="path"/>, which must not exist yet; false,
    /// with why in <paramref name="reason"/>, when it cannot.</summary>
    public bool TryWriteNewFile(string path, string text, [NotNullWhen(false)] out string? reason)
    {
        try
        {
            using var file = new StreamWriter(path, new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write });
            file.Write(text);
            reason = null;
            return true;
        }
        catch (IOException e)
        {
            reason = e.Message;
            return false;
        }
        catch (UnauthorizedAccessException e)
        {
            reason = e.Message;
            return false;
        }
    }

    public bool TryStartWatching(string folder, [NotNullWhen(false)] out string? reason)
    {
        return watch.TryStart(folder, out reason);
    }

    /// <summary>Comes back on the UI thread. Shutting down stops the watch, as <paramref name="ct"/> fires, and
    /// ends the wait as cancelled.</summary>
    public async Task<Outcome<ThemeFolderEvent>> WaitForChangeAsync(CancellationToken ct)
    {
        using var stopping = ct.Register(watch.Stop);
        return await FileReadFailures.CatchAsync(Background.Wait(watch.NextChangeAsync(ct)), ct);
    }
}

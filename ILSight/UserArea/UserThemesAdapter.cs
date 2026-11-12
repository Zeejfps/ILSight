using System.Diagnostics.CodeAnalysis;

namespace ILSight.UserArea;

/// <summary>The seam around a folder of the user's themes of one kind on disk, for
/// <see cref="UserThemes{TFile, TContents, TTheme}"/>: lists and reads its theme files, makes it with its schema
/// and guide in it, writes new theme files, and watches it, telling of a change on the UI thread; and the app's
/// error reports. What the disk refuses comes back as a result; a wait the token stops ends as cancelled, and
/// stops the watch. Each kind of theme says how its files are read and where its schema comes from.</summary>
public abstract class UserThemesAdapter<TFile, TContents, TTheme>(ErrorLog errors, ThemeFolderWatch watch) : UserThemes<TFile, TContents, TTheme>.IPorts
    where TFile : class, IThemeFile<TFile, TContents>
    where TContents : class
    where TTheme : class
{
    private const string GuideFileName = "README.md";

    /// <summary>The name of the schema file in the folder, which is no theme.</summary>
    protected abstract string SchemaFileName { get; }

    /// <summary>The schema theme files of this kind name in <c>$schema</c>.</summary>
    protected abstract string ReadSchema();

    /// <summary>The README on writing themes of this kind.</summary>
    protected abstract string ReadGuide();

    /// <summary>The theme <paramref name="id"/> <paramref name="json"/>, the content of the file at
    /// <paramref name="path"/>, writes, everything else it names read with it.</summary>
    protected abstract TFile ReadTheme(ThemeId<TFile> id, string path, byte[] json);

    /// <summary>The theme <paramref name="id"/> of the file at <paramref name="path"/>, which could not be read
    /// for <paramref name="reason"/>.</summary>
    protected abstract TFile CreateUnreadableTheme(ThemeId<TFile> id, string path, string reason);

    public void ReportError(AppProblem problem)
    {
        Errors.Report(errors, problem);
    }

    public List<string> ListThemeFiles(string folder)
    {
        try
        {
            return Directory.EnumerateFiles(folder, "*" + TFile.Extension)
                .Where(p => !IsNamed(p, SchemaFileName))
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

    /// <summary>Reads the whole file and what it names now, so nothing is left to fail later.</summary>
    public TFile ReadThemeFile(ThemeId<TFile> id, string path)
    {
        byte[] json;
        try
        {
            json = File.ReadAllBytes(path);
        }
        catch (IOException e)
        {
            return CreateUnreadableTheme(id, path, e.Message);
        }
        catch (UnauthorizedAccessException e)
        {
            return CreateUnreadableTheme(id, path, e.Message);
        }

        return ReadTheme(id, path, json);
    }

    public bool TryMakeFolder(string folder, [NotNullWhen(false)] out string? reason)
    {
        try
        {
            Directory.CreateDirectory(folder);
            WriteIfChanged(Path.Combine(folder, SchemaFileName), ReadSchema());
            WriteIfChanged(Path.Combine(folder, GuideFileName), ReadGuide());
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
        var change = await Background.Wait(watch.NextChangeAsync(ct));
        ct.ThrowIfCancellationRequested();
        return change;
    }
}

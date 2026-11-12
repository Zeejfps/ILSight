using System.Diagnostics.CodeAnalysis;

namespace ILSight.UserState;

/// <summary>The user's themes of one kind, color or icon, kept in their folder beside the settings: read into
/// <see cref="Themes"/>, and again whenever the user asks or, while <see cref="WatchAsync"/> runs, the folder
/// changes; the folder made with the schema and a guide in it when it is missing; and a custom theme written
/// there. What is wrong in a theme file is reported once, until it is fixed. Lives on the UI thread, as the
/// app state that holds it does, and reaches the folder and the error reports through <see cref="IPorts"/>.</summary>
public sealed class UserThemes<TFile, TContents, TTheme>(UserThemes<TFile, TContents, TTheme>.IPorts ports)
    where TFile : class, IThemeFile<TFile, TContents>
    where TContents : class
    where TTheme : class
{
    /// <summary>The folder of the user's themes and the user's error reports.</summary>
    public interface IPorts
    {
        /// <summary>Tells the user something about the themes went wrong.</summary>
        void ReportError(AppProblem problem);

        /// <summary>The theme files in <paramref name="folder"/>, in the order of their names, without the
        /// schema the folder holds; none when it is missing or cannot be listed.</summary>
        List<string> ListThemeFiles(string folder);

        /// <summary>The theme <paramref name="id"/> read from the file at <paramref name="path"/>, with what is
        /// wrong in it; a file that cannot be read has no contents and says why.</summary>
        TFile ReadThemeFile(ThemeId<TFile> id, string path);

        /// <summary>Makes <paramref name="folder"/>, with the schema theme files name in it and a guide on
        /// writing them; false, with why in <paramref name="reason"/>, when it cannot.</summary>
        bool TryMakeFolder(string folder, [NotNullWhen(false)] out string? reason);

        /// <summary>Whether there is a file at <paramref name="path"/>.</summary>
        bool IsFile(string path);

        /// <summary>Writes <paramref name="text"/> to a file at <paramref name="path"/>, which must not exist
        /// yet; false, with why in <paramref name="reason"/>, when it cannot.</summary>
        bool TryWriteNewFile(string path, string text, [NotNullWhen(false)] out string? reason);

        /// <summary>Starts watching the theme files of <paramref name="folder"/> in place of what was watched
        /// before; false, with why in <paramref name="reason"/>, when they cannot be watched.</summary>
        bool TryStartWatching(string folder, [NotNullWhen(false)] out string? reason);

        /// <summary>Completes on the UI thread once the watched files changed and stayed quiet for a moment,
        /// or once watching was lost or stopped. When <paramref name="ct"/> fires, watching stops.</summary>
        Task<Outcome<ThemeFolderEvent>> WaitForChangeAsync(CancellationToken ct);
    }

    private const string CustomSuffix = "-custom";
    private const int FirstCustomNumber = 2;

    /// <summary>The themes of this kind, which the user's are read into.</summary>
    public required ThemeCatalog<TFile, TContents, TTheme> Themes { get; init; }

    /// <summary>Where the user's themes of this kind are; null while the settings are kept in memory only.</summary>
    public required string? Folder { get; init; }

    /// <summary>Reads the user's themes into <see cref="Themes"/> and reports what is wrong in any of them.</summary>
    public void ReadThemes()
    {
        RefreshThemes([]);
    }

    /// <summary>Reads the user's themes again, reporting only what is newly wrong.</summary>
    public void ReloadThemes()
    {
        var known = Themes.Errors.ToList();
        RefreshThemes(known);
    }

    private void RefreshThemes(IReadOnlyList<ThemeError> known)
    {
        var files = ReadUserThemes();
        Themes.ReplaceUserThemes(files);
        ReportNewErrors(known);
    }

    /// <summary>The theme files in the folder whose names are ids, read; none without a folder.</summary>
    private List<TFile> ReadUserThemes()
    {
        var files = new List<TFile>();
        var folder = Folder;
        if (folder is null)
        {
            return files;
        }

        foreach (var path in ports.ListThemeFiles(folder))
        {
            var id = ThemeId<TFile>.Parse(Path.GetFileNameWithoutExtension(path));
            if (id is not null)
            {
                files.Add(ports.ReadThemeFile(id, path));
            }
        }
        return files;
    }

    /// <summary>Reports the errors of <see cref="Themes"/> not in <paramref name="known"/>, one report per file.</summary>
    private void ReportNewErrors(IReadOnlyList<ThemeError> known)
    {
        var byFile = Themes.Errors
            .Where(e => !known.Contains(e))
            .GroupBy(static e => e.File);
        foreach (var fileErrors in byFile)
        {
            ports.ReportError(new AppProblem.ThemeFileProblems(TFile.Kind, fileErrors.Key, fileErrors.ToList()));
        }
    }

    /// <summary>The user's folder of these themes, made with the schema in it when missing, for the screen to
    /// show; null when the settings live in memory only, or after reporting why it cannot be made.</summary>
    public string? PrepareFolder()
    {
        var folder = Folder;
        if (folder is null)
        {
            return null;
        }

        if (ports.TryMakeFolder(folder, out var reason))
        {
            return folder;
        }

        ports.ReportError(new AppProblem.ThemesFolderNotMade(TFile.Kind, reason));
        return null;
    }

    /// <summary>Writes a new theme of the user's that extends the one <paramref name="chosen"/> shows, as
    /// <paramref name="draftOf"/> writes it from the id and the theme of the one it extends, then reads the themes
    /// again. Returns its id and file, for the caller to switch to it and open it; null when the settings live in
    /// memory only, or after reporting why the file cannot be written.</summary>
    public CustomTheme<TFile>? CreateCustomTheme(ThemeId<TFile> chosen, Func<ThemeId<TFile>, TTheme, string> draftOf)
    {
        var folder = PrepareFolder();
        if (folder is null)
        {
            return null;
        }

        var parent = Themes.GetShownId(chosen);
        var id = GetFreeCustomId(folder, parent);
        var path = GetThemeFilePath(folder, id);
        var text = draftOf(parent, Themes.GetTheme(parent));
        if (!ports.TryWriteNewFile(path, text, out var reason))
        {
            ports.ReportError(new AppProblem.CustomThemeNotWritten(TFile.Kind, path, reason));
            return null;
        }

        ReloadThemes();
        return new CustomTheme<TFile> { Id = id, Path = path };
    }

    /// <summary><paramref name="parent"/> followed by <c>-custom</c>, numbered from 2 while a theme or a file
    /// in <paramref name="folder"/> already has that id.</summary>
    private ThemeId<TFile> GetFreeCustomId(string folder, ThemeId<TFile> parent)
    {
        var id = ThemeId<TFile>.Of(parent.Text + CustomSuffix);
        var number = FirstCustomNumber;
        while (IsTaken(folder, id))
        {
            id = ThemeId<TFile>.Of($"{parent.Text}{CustomSuffix}-{number}");
            number++;
        }
        return id;
    }

    /// <summary>True when a theme has the id <paramref name="id"/>, or a file in <paramref name="folder"/> is
    /// named by it.</summary>
    private bool IsTaken(string folder, ThemeId<TFile> id)
    {
        var index = Themes.FindThemeIndex(id);
        if (index is not null)
        {
            return true;
        }

        return ports.IsFile(GetThemeFilePath(folder, id));
    }

    private static string GetThemeFilePath(string folder, ThemeId<TFile> id)
    {
        return Path.Combine(folder, id.Text + TFile.Extension);
    }

    /// <summary>Reads the user's themes again whenever their folder changes, until <paramref name="ct"/> fires,
    /// so a theme redraws as its file is saved. Makes the folder when it is missing, and again when it is removed
    /// while watched. When the folder cannot be made or watched, reports why and returns, leaving the themes to be
    /// read again by hand.</summary>
    public async Task WatchAsync(CancellationToken ct)
    {
        if (!TryStartWatching())
        {
            return;
        }

        var restartedSinceChange = false;
        while (true)
        {
            var outcome = await ports.WaitForChangeAsync(ct);
            if (outcome is not Outcome<ThemeFolderEvent>.Ok { Value: var news })
            {
                return;
            }

            if (news.Kind == ThemeFolderEventKind.Stopped)
            {
                return;
            }

            var isLost = news.Kind == ThemeFolderEventKind.Lost;
            if (isLost && !TryRestartWatching(news.Reason, restartedSinceChange))
            {
                return;
            }

            restartedSinceChange = isLost;
            ReloadThemes();
        }
    }

    /// <summary>Makes the folder and watches it; false when there is none to watch, or after reporting why it
    /// cannot be made or watched.</summary>
    private bool TryStartWatching()
    {
        var folder = PrepareFolder();
        if (folder is null)
        {
            return false;
        }

        if (ports.TryStartWatching(folder, out var reason))
        {
            return true;
        }

        ReportUnwatched(reason);
        return false;
    }

    /// <summary>Watches the folder again after the watch was lost, as when the folder was removed. Once it was
    /// restarted with no change seen since, it gives up rather than lose it again and again, and reports
    /// <paramref name="reason"/>.</summary>
    private bool TryRestartWatching(string reason, bool restartedSinceChange)
    {
        if (!restartedSinceChange)
        {
            return TryStartWatching();
        }

        ReportUnwatched(reason);
        return false;
    }

    private void ReportUnwatched(string reason)
    {
        ports.ReportError(new AppProblem.ThemesNotWatched(TFile.Kind, reason));
    }
}

using System.Globalization;
using System.Text;
using ILSight.BuiltInArea;
using ILSight.UserArea;

namespace ILSight.App.Tests;

/// <summary>The user's themes over a fake port: what reading them, making their folder, writing a custom theme and
/// watching the folder do with what the port answers, and the token the watch waits with.</summary>
public sealed class UserThemesTests
{
    private const string Folder = "themes";

    private static string PathOf(string id)
    {
        return Path.Combine(Folder, id + ".json");
    }

    private static UserThemes<ThemeFile, ThemeContents, AppTheme> Create(FakeUserThemesPorts<ThemeFile, ThemeContents, AppTheme> ports, string? folder = Folder)
    {
        return new UserThemes<ThemeFile, ThemeContents, AppTheme>(ports) { Themes = Theming.CreateCatalog(BuiltInThemes.Files), Folder = folder };
    }

    private static ThemeId<ThemeFile> Id(string text)
    {
        return ThemeId<ThemeFile>.Of(text);
    }

    private static ThemeFolderEvent Lost(string reason)
    {
        return new ThemeFolderEvent(ThemeFolderEventKind.Lost, reason);
    }

    private static readonly ThemeFolderEvent Changed = new(ThemeFolderEventKind.Changed);

    [Fact]
    public void ReadThemes_ThemeFileUnreadable_ReportsItUnderItsFileOnce()
    {
        var ports = new FakeUserThemesPorts<ThemeFile, ThemeContents, AppTheme>();
        ports.Files[PathOf("locked")] = null;
        var themes = Create(ports);

        themes.ReadThemes();
        themes.ReloadThemes();

        var problem = Assert.IsType<AppProblem.ThemeFileProblems>(Assert.Single(ports.Reported));
        Assert.Equal(ThemeKind.Color, problem.Kind);
        Assert.Equal(PathOf("locked"), problem.File);
        var error = Assert.Single(problem.Errors);
        Assert.Equal(ThemeProblem.Unreadable, error.Problem);
        Assert.Equal("Access is denied.", error.Detail);
        Assert.Contains(Id("locked"), themes.Themes.Ids);
    }

    [Fact]
    public void ReadThemes_IconThemeNamesAFontTheFolderLacks_TellsItsLineAndItsIconsFallBack()
    {
        var ports = new FakeUserThemesPorts<IconThemeFile, IconThemeContents, IconTheme>();
        ports.Files[PathOf("mine")] = "{\r\n  \"name\": \"Mine\",\r\n  \"fonts\": { \"m\": \"mine.ttf\" },\r\n"
            + "  \"icons\": { \"node.class\": { \"font\": \"m\", \"glyph\": \"E106\" } }\r\n}";
        var themes = new UserThemes<IconThemeFile, IconThemeContents, IconTheme>(ports)
        {
            Themes = IconTheming.CreateCatalog(BuiltInIconThemes.Files),
            Folder = Folder,
        };

        themes.ReadThemes();

        var problem = Assert.IsType<AppProblem.ThemeFileProblems>(Assert.Single(ports.Reported));
        Assert.Contains(new ThemeError(PathOf("mine"), 3, ThemeProblem.AssetUnreadable, "fonts.m", "mine.ttf"), problem.Errors);
        Assert.Equal(BuiltInIconThemes.Reference.Icons.NodeClass, themes.Themes.GetTheme(ThemeId<IconThemeFile>.Of("mine")).Icons.NodeClass);
        Assert.Contains(nameof(ports.ReadNamedFiles), ports.Asked);
    }

    [Fact]
    public void ReadThemes_NoFolder_ReadsNothing()
    {
        var ports = new FakeUserThemesPorts<ThemeFile, ThemeContents, AppTheme>();
        var themes = Create(ports, folder: null);

        themes.ReadThemes();

        Assert.Empty(ports.Asked);
        Assert.Equal(BuiltInThemes.Files.Count, themes.Themes.Ids.Count);
    }

    [Fact]
    public void PrepareFolder_FolderCannotBeMade_ReportsWhyAndGivesNone()
    {
        var ports = new FakeUserThemesPorts<ThemeFile, ThemeContents, AppTheme> { CanMakeFolder = false };
        var themes = Create(ports);

        var folder = themes.PrepareFolder();

        Assert.Null(folder);
        var problem = Assert.IsType<AppProblem.ThemesFolderNotMade>(Assert.Single(ports.Reported));
        Assert.Equal("There is a file named themes.", problem.Reason);
    }

    [Fact]
    public void CreateCustomTheme_IdOfAFileTaken_WritesTheNextOneAndReadsItWithTheOthers()
    {
        var ports = new FakeUserThemesPorts<ThemeFile, ThemeContents, AppTheme>();
        ports.Files[PathOf("dracula-custom")] = "{ \"extends\": \"dracula\" }";
        var themes = Create(ports);
        themes.ReadThemes();
        var named = new List<ThemeId<ThemeFile>>();

        var custom = themes.CreateCustomTheme(Id("dracula"), parent =>
        {
            named.Add(parent);
            return "My Dracula";
        });

        Assert.NotNull(custom);
        Assert.Equal(Id("dracula-custom-2"), custom.Id);
        Assert.Equal(PathOf("dracula-custom-2"), custom.Path);
        Assert.Equal([Id("dracula")], named);
        Assert.Contains(Id("dracula-custom-2"), themes.Themes.Ids);
        Assert.Equal("My Dracula", themes.Themes.GetThemeName(custom.Id, CultureInfo.InvariantCulture));
        var written = ports.Files[custom.Path] ?? throw new InvalidOperationException("The fake folder holds no text for the custom theme.");
        Assert.Equal(Id("dracula"), Theming.ReadTheme(custom.Id, custom.Path, Encoding.UTF8.GetBytes(written)).Extends?.Id);
        Assert.Empty(ports.Reported);
    }

    [Fact]
    public void CreateCustomTheme_FileCannotBeWritten_ReportsItAndMakesNone()
    {
        var ports = new FakeUserThemesPorts<ThemeFile, ThemeContents, AppTheme> { CanWrite = false };
        var themes = Create(ports);

        var custom = themes.CreateCustomTheme(Id("dark"), static _ => "Mine");

        Assert.Null(custom);
        var problem = Assert.IsType<AppProblem.CustomThemeNotWritten>(Assert.Single(ports.Reported));
        Assert.Equal(PathOf("dark-custom"), problem.Path);
        Assert.Equal("The folder is read-only.", problem.Reason);
        Assert.DoesNotContain(nameof(FakeUserThemesPorts<ThemeFile, ThemeContents, AppTheme>.ListThemeFiles), ports.Asked);
    }

    [Fact]
    public void WatchAsync_NothingChanges_ReadsNothingUntilShutdown()
    {
        var ports = new FakeUserThemesPorts<ThemeFile, ThemeContents, AppTheme>();
        using var shutdown = new CancellationTokenSource();

        var watching = Create(ports).WatchAsync(shutdown.Token);

        Assert.False(watching.IsCompleted);
        Assert.Equal([nameof(FakeUserThemesPorts<ThemeFile, ThemeContents, AppTheme>.TryMakeFolder), nameof(FakeUserThemesPorts<ThemeFile, ThemeContents, AppTheme>.TryStartWatching), nameof(FakeUserThemesPorts<ThemeFile, ThemeContents, AppTheme>.WaitForChangeAsync)], ports.Asked);
        Assert.All(ports.Tokens, token => Assert.Equal(shutdown.Token, token));
    }

    [Fact]
    public void WatchAsync_FolderChanged_ReadsTheThemesAgain()
    {
        var ports = new FakeUserThemesPorts<ThemeFile, ThemeContents, AppTheme> { Changes = new Queue<ThemeFolderEvent>([Changed]) };
        ports.Files[PathOf("saved")] = "{ \"extends\": \"dark\" }";
        var themes = Create(ports);
        using var shutdown = new CancellationTokenSource();

        var watching = themes.WatchAsync(shutdown.Token);

        Assert.False(watching.IsCompleted);
        Assert.Contains(Id("saved"), themes.Themes.Ids);
        Assert.Empty(ports.Reported);
    }

    [Fact]
    public void WatchAsync_ChangedThemeUnreadable_ReportsIt()
    {
        var ports = new FakeUserThemesPorts<ThemeFile, ThemeContents, AppTheme> { Changes = new Queue<ThemeFolderEvent>([Changed]) };
        ports.Files[PathOf("locked")] = null;
        using var shutdown = new CancellationTokenSource();

        var watching = Create(ports).WatchAsync(shutdown.Token);

        Assert.False(watching.IsCompleted);
        var problem = Assert.IsType<AppProblem.ThemeFileProblems>(Assert.Single(ports.Reported));
        Assert.Equal(ThemeProblem.Unreadable, Assert.Single(problem.Errors).Problem);
    }

    [Fact]
    public void WatchAsync_FolderLost_MakesItAgainWatchesItAgainAndReadsTheThemes()
    {
        var ports = new FakeUserThemesPorts<ThemeFile, ThemeContents, AppTheme> { Changes = new Queue<ThemeFolderEvent>([Lost("The folder was removed.")]) };
        using var shutdown = new CancellationTokenSource();

        var watching = Create(ports).WatchAsync(shutdown.Token);

        Assert.False(watching.IsCompleted);
        Assert.Equal(
            [
                nameof(FakeUserThemesPorts<ThemeFile, ThemeContents, AppTheme>.TryMakeFolder), nameof(FakeUserThemesPorts<ThemeFile, ThemeContents, AppTheme>.TryStartWatching), nameof(FakeUserThemesPorts<ThemeFile, ThemeContents, AppTheme>.WaitForChangeAsync),
                nameof(FakeUserThemesPorts<ThemeFile, ThemeContents, AppTheme>.TryMakeFolder), nameof(FakeUserThemesPorts<ThemeFile, ThemeContents, AppTheme>.TryStartWatching), nameof(FakeUserThemesPorts<ThemeFile, ThemeContents, AppTheme>.ListThemeFiles),
                nameof(FakeUserThemesPorts<ThemeFile, ThemeContents, AppTheme>.WaitForChangeAsync),
            ],
            ports.Asked);
        Assert.Empty(ports.Reported);
    }

    [Fact]
    public async Task WatchAsync_FolderLostAgainBeforeAnyChange_ReportsWhyAndStopsWatching()
    {
        var ports = new FakeUserThemesPorts<ThemeFile, ThemeContents, AppTheme> { Changes = new Queue<ThemeFolderEvent>([Lost("The folder was removed."), Lost("The folder was removed again.")]) };
        using var shutdown = new CancellationTokenSource();

        await Create(ports).WatchAsync(shutdown.Token);

        var problem = Assert.IsType<AppProblem.ThemesNotWatched>(Assert.Single(ports.Reported));
        Assert.Equal("The folder was removed again.", problem.Reason);
        Assert.Equal(2, ports.Asked.Count(static a => a == nameof(FakeUserThemesPorts<ThemeFile, ThemeContents, AppTheme>.TryStartWatching)));
    }

    [Fact]
    public void WatchAsync_FolderLostAgainAfterAChange_WatchesItAgain()
    {
        var ports = new FakeUserThemesPorts<ThemeFile, ThemeContents, AppTheme> { Changes = new Queue<ThemeFolderEvent>([Lost("The folder was removed."), Changed, Lost("The folder was removed again.")]) };
        using var shutdown = new CancellationTokenSource();

        var watching = Create(ports).WatchAsync(shutdown.Token);

        Assert.False(watching.IsCompleted);
        Assert.Equal(3, ports.Asked.Count(static a => a == nameof(FakeUserThemesPorts<ThemeFile, ThemeContents, AppTheme>.TryStartWatching)));
        Assert.Empty(ports.Reported);
    }

    [Fact]
    public async Task WatchAsync_FolderCannotBeMade_ReportsItAndNeverWatches()
    {
        var ports = new FakeUserThemesPorts<ThemeFile, ThemeContents, AppTheme> { CanMakeFolder = false };

        await Create(ports).WatchAsync(CancellationToken.None);

        Assert.IsType<AppProblem.ThemesFolderNotMade>(Assert.Single(ports.Reported));
        Assert.Equal([nameof(FakeUserThemesPorts<ThemeFile, ThemeContents, AppTheme>.TryMakeFolder)], ports.Asked);
    }

    [Fact]
    public async Task WatchAsync_FolderCannotBeWatched_ReportsWhyAndEnds()
    {
        var ports = new FakeUserThemesPorts<ThemeFile, ThemeContents, AppTheme> { CanWatch = false };

        await Create(ports).WatchAsync(CancellationToken.None);

        var problem = Assert.IsType<AppProblem.ThemesNotWatched>(Assert.Single(ports.Reported));
        Assert.Equal(ThemeKind.Color, problem.Kind);
        Assert.Equal("Too many folders are watched.", problem.Reason);
    }

    [Fact]
    public async Task WatchAsync_NoFolder_WatchesNothing()
    {
        var ports = new FakeUserThemesPorts<ThemeFile, ThemeContents, AppTheme>();

        await Create(ports, folder: null).WatchAsync(CancellationToken.None);

        Assert.Empty(ports.Asked);
        Assert.Empty(ports.Reported);
    }

    [Fact]
    public async Task WatchAsync_WatchStopped_Ends()
    {
        var ports = new FakeUserThemesPorts<ThemeFile, ThemeContents, AppTheme> { Changes = new Queue<ThemeFolderEvent>([new ThemeFolderEvent(ThemeFolderEventKind.Stopped)]) };

        await Create(ports).WatchAsync(CancellationToken.None);

        Assert.DoesNotContain(nameof(FakeUserThemesPorts<ThemeFile, ThemeContents, AppTheme>.ListThemeFiles), ports.Asked);
        Assert.Empty(ports.Reported);
    }

    [Fact]
    public async Task WatchAsync_Shutdown_EndsCancelledAndReportsNothing()
    {
        var ports = new FakeUserThemesPorts<ThemeFile, ThemeContents, AppTheme>();
        using var shutdown = new CancellationTokenSource();
        var watching = Create(ports).WatchAsync(shutdown.Token);

        await shutdown.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => watching);
        Assert.Empty(ports.Reported);
    }
}

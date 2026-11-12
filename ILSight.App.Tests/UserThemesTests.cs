using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace ILSight.App.Tests;

/// <summary>The user's themes over a fake port: what reading them, making their folder, writing a custom theme and
/// watching the folder do with what the port answers, and the token the watch waits with.</summary>
public sealed class UserThemesTests
{
    private const string Folder = "themes";

    /// <summary>A theme folder held in memory: answers each port member as the test set it, and keeps what was
    /// reported and, in order, what was asked of the folder.</summary>
    private sealed class FakePorts : UserThemes<ThemeFile, ThemeContents, AppTheme>.IPorts
    {
        /// <summary>The files in the folder by path, with what they hold; a null file cannot be read.</summary>
        public Dictionary<string, string?> Files { get; } = [];

        public bool CanMakeFolder { get; init; } = true;

        public bool CanWrite { get; init; } = true;

        public bool CanWatch { get; init; } = true;

        /// <summary>What the folder tells, in turn, to each wait; once none is left, a wait lasts until shutdown.</summary>
        public Queue<ThemeFolderEvent> Changes { get; init; } = [];

        public List<AppProblem> Reported { get; } = [];

        public List<string> Asked { get; } = [];

        public List<CancellationToken> Tokens { get; } = [];

        public void ReportError(AppProblem problem)
        {
            Reported.Add(problem);
        }

        public List<string> ListThemeFiles(string folder)
        {
            Asked.Add(nameof(ListThemeFiles));
            return Files.Keys
                .Order(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public ThemeFile ReadThemeFile(ThemeId<ThemeFile> id, string path)
        {
            var json = Files[path];
            if (json is null)
            {
                return Theming.CreateUnreadableTheme(id, path, "Access is denied.");
            }

            return Theming.ReadTheme(id, path, Encoding.UTF8.GetBytes(json));
        }

        public bool TryMakeFolder(string folder, [NotNullWhen(false)] out string? reason)
        {
            Asked.Add(nameof(TryMakeFolder));
            reason = CanMakeFolder ? null : "There is a file named themes.";
            return CanMakeFolder;
        }

        public bool IsFile(string path)
        {
            return Files.ContainsKey(path);
        }

        public bool TryWriteNewFile(string path, string text, [NotNullWhen(false)] out string? reason)
        {
            Asked.Add(nameof(TryWriteNewFile));
            if (!CanWrite)
            {
                reason = "The folder is read-only.";
                return false;
            }

            Files[path] = text;
            reason = null;
            return true;
        }

        public bool TryStartWatching(string folder, [NotNullWhen(false)] out string? reason)
        {
            Asked.Add(nameof(TryStartWatching));
            reason = CanWatch ? null : "Too many folders are watched.";
            return CanWatch;
        }

        public Task<Outcome<ThemeFolderEvent>> WaitForChangeAsync(CancellationToken ct)
        {
            Asked.Add(nameof(WaitForChangeAsync));
            Tokens.Add(ct);
            if (Changes.TryDequeue(out var change))
            {
                return Task.FromResult<Outcome<ThemeFolderEvent>>(new Outcome<ThemeFolderEvent>.Ok(change));
            }

            return WaitForShutdownAsync(ct);
        }

        private static async Task<Outcome<ThemeFolderEvent>> WaitForShutdownAsync(CancellationToken ct)
        {
            await Task.Delay(Timeout.Infinite, ct);
            throw new UnreachableException("Only shutting down ends a wait with no change left to tell.");
        }
    }

    private static string PathOf(string id)
    {
        return Path.Combine(Folder, id + ".json");
    }

    private static UserThemes<ThemeFile, ThemeContents, AppTheme> Create(FakePorts ports, string? folder = Folder)
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
        var ports = new FakePorts();
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
    public void ReadThemes_NoFolder_ReadsNothing()
    {
        var ports = new FakePorts();
        var themes = Create(ports, folder: null);

        themes.ReadThemes();

        Assert.Empty(ports.Asked);
        Assert.Equal(BuiltInThemes.Files.Count, themes.Themes.Ids.Count);
    }

    [Fact]
    public void PrepareFolder_FolderCannotBeMade_ReportsWhyAndGivesNone()
    {
        var ports = new FakePorts { CanMakeFolder = false };
        var themes = Create(ports);

        var folder = themes.PrepareFolder();

        Assert.Null(folder);
        var problem = Assert.IsType<AppProblem.ThemesFolderNotMade>(Assert.Single(ports.Reported));
        Assert.Equal("There is a file named themes.", problem.Reason);
    }

    [Fact]
    public void CreateCustomTheme_IdOfAFileTaken_WritesTheNextOneAndReadsItWithTheOthers()
    {
        var ports = new FakePorts();
        ports.Files[PathOf("dracula-custom")] = "{ \"extends\": \"dracula\" }";
        var themes = Create(ports);
        themes.ReadThemes();
        var drafted = new List<ThemeId<ThemeFile>>();

        var custom = themes.CreateCustomTheme(Id("dracula"), (parent, _) =>
        {
            drafted.Add(parent);
            return "{ \"extends\": \"dracula\" }";
        });

        Assert.NotNull(custom);
        Assert.Equal(Id("dracula-custom-2"), custom.Id);
        Assert.Equal(PathOf("dracula-custom-2"), custom.Path);
        Assert.Equal([Id("dracula")], drafted);
        Assert.Contains(Id("dracula-custom-2"), themes.Themes.Ids);
        Assert.Empty(ports.Reported);
    }

    [Fact]
    public void CreateCustomTheme_FileCannotBeWritten_ReportsItAndMakesNone()
    {
        var ports = new FakePorts { CanWrite = false };
        var themes = Create(ports);

        var custom = themes.CreateCustomTheme(Id("dark"), static (_, _) => "{}");

        Assert.Null(custom);
        var problem = Assert.IsType<AppProblem.CustomThemeNotWritten>(Assert.Single(ports.Reported));
        Assert.Equal(PathOf("dark-custom"), problem.Path);
        Assert.Equal("The folder is read-only.", problem.Reason);
        Assert.DoesNotContain(nameof(FakePorts.ListThemeFiles), ports.Asked);
    }

    [Fact]
    public void WatchAsync_NothingChanges_ReadsNothingUntilShutdown()
    {
        var ports = new FakePorts();
        using var shutdown = new CancellationTokenSource();

        var watching = Create(ports).WatchAsync(shutdown.Token);

        Assert.False(watching.IsCompleted);
        Assert.Equal([nameof(FakePorts.TryMakeFolder), nameof(FakePorts.TryStartWatching), nameof(FakePorts.WaitForChangeAsync)], ports.Asked);
        Assert.All(ports.Tokens, token => Assert.Equal(shutdown.Token, token));
    }

    [Fact]
    public void WatchAsync_FolderChanged_ReadsTheThemesAgain()
    {
        var ports = new FakePorts { Changes = new Queue<ThemeFolderEvent>([Changed]) };
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
        var ports = new FakePorts { Changes = new Queue<ThemeFolderEvent>([Changed]) };
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
        var ports = new FakePorts { Changes = new Queue<ThemeFolderEvent>([Lost("The folder was removed.")]) };
        using var shutdown = new CancellationTokenSource();

        var watching = Create(ports).WatchAsync(shutdown.Token);

        Assert.False(watching.IsCompleted);
        Assert.Equal(
            [
                nameof(FakePorts.TryMakeFolder), nameof(FakePorts.TryStartWatching), nameof(FakePorts.WaitForChangeAsync),
                nameof(FakePorts.TryMakeFolder), nameof(FakePorts.TryStartWatching), nameof(FakePorts.ListThemeFiles),
                nameof(FakePorts.WaitForChangeAsync),
            ],
            ports.Asked);
        Assert.Empty(ports.Reported);
    }

    [Fact]
    public async Task WatchAsync_FolderLostAgainBeforeAnyChange_ReportsWhyAndStopsWatching()
    {
        var ports = new FakePorts { Changes = new Queue<ThemeFolderEvent>([Lost("The folder was removed."), Lost("The folder was removed again.")]) };
        using var shutdown = new CancellationTokenSource();

        await Create(ports).WatchAsync(shutdown.Token);

        var problem = Assert.IsType<AppProblem.ThemesNotWatched>(Assert.Single(ports.Reported));
        Assert.Equal("The folder was removed again.", problem.Reason);
        Assert.Equal(2, ports.Asked.Count(static a => a == nameof(FakePorts.TryStartWatching)));
    }

    [Fact]
    public void WatchAsync_FolderLostAgainAfterAChange_WatchesItAgain()
    {
        var ports = new FakePorts { Changes = new Queue<ThemeFolderEvent>([Lost("The folder was removed."), Changed, Lost("The folder was removed again.")]) };
        using var shutdown = new CancellationTokenSource();

        var watching = Create(ports).WatchAsync(shutdown.Token);

        Assert.False(watching.IsCompleted);
        Assert.Equal(3, ports.Asked.Count(static a => a == nameof(FakePorts.TryStartWatching)));
        Assert.Empty(ports.Reported);
    }

    [Fact]
    public async Task WatchAsync_FolderCannotBeMade_ReportsItAndNeverWatches()
    {
        var ports = new FakePorts { CanMakeFolder = false };

        await Create(ports).WatchAsync(CancellationToken.None);

        Assert.IsType<AppProblem.ThemesFolderNotMade>(Assert.Single(ports.Reported));
        Assert.Equal([nameof(FakePorts.TryMakeFolder)], ports.Asked);
    }

    [Fact]
    public async Task WatchAsync_FolderCannotBeWatched_ReportsWhyAndEnds()
    {
        var ports = new FakePorts { CanWatch = false };

        await Create(ports).WatchAsync(CancellationToken.None);

        var problem = Assert.IsType<AppProblem.ThemesNotWatched>(Assert.Single(ports.Reported));
        Assert.Equal(ThemeKind.Color, problem.Kind);
        Assert.Equal("Too many folders are watched.", problem.Reason);
    }

    [Fact]
    public async Task WatchAsync_NoFolder_WatchesNothing()
    {
        var ports = new FakePorts();

        await Create(ports, folder: null).WatchAsync(CancellationToken.None);

        Assert.Empty(ports.Asked);
        Assert.Empty(ports.Reported);
    }

    [Fact]
    public async Task WatchAsync_WatchStopped_Ends()
    {
        var ports = new FakePorts { Changes = new Queue<ThemeFolderEvent>([new ThemeFolderEvent(ThemeFolderEventKind.Stopped)]) };

        await Create(ports).WatchAsync(CancellationToken.None);

        Assert.DoesNotContain(nameof(FakePorts.ListThemeFiles), ports.Asked);
        Assert.Empty(ports.Reported);
    }

    [Fact]
    public async Task WatchAsync_Shutdown_EndsCancelledAndReportsNothing()
    {
        var ports = new FakePorts();
        using var shutdown = new CancellationTokenSource();
        var watching = Create(ports).WatchAsync(shutdown.Token);

        await shutdown.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => watching);
        Assert.Empty(ports.Reported);
    }
}

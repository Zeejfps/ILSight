using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using ILSight.UserArea;

namespace ILSight.App.Tests;

/// <summary>A theme folder held in memory, of either kind of theme: answers each port member as the test set it,
/// and keeps what was reported and, in order, what was asked of the folder.</summary>
internal sealed class FakeUserThemesPorts<TFile, TContents, TTheme> : UserThemes<TFile, TContents, TTheme>.IPorts
    where TFile : class, IUserThemeFile<TFile, TContents, TTheme>
    where TContents : class
    where TTheme : class
{
    /// <summary>The files in the folder by path, with what they hold; a null file cannot be read.</summary>
    public Dictionary<string, string?> Files { get; } = [];

    /// <summary>The files the themes name (fonts, pictures) by the path they name them by; one not here cannot be
    /// read.</summary>
    public Dictionary<string, byte[]> NamedFiles { get; } = [];

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

    public Outcome<byte[]> ReadThemeFile(string path)
    {
        var json = Files[path];
        if (json is null)
        {
            return new Outcome<byte[]>.Failed(new UnauthorizedAccessException("Access is denied."));
        }

        return new Outcome<byte[]>.Ok(Encoding.UTF8.GetBytes(json));
    }

    public IReadOnlyDictionary<string, IconAsset> ReadNamedFiles(string themePath, IReadOnlyList<string> paths)
    {
        Asked.Add(nameof(ReadNamedFiles));
        var read = new Dictionary<string, IconAsset>();
        foreach (var path in paths)
        {
            read[path] = ReadNamedFile(path);
        }
        return read;
    }

    private IconAsset ReadNamedFile(string path)
    {
        if (!NamedFiles.TryGetValue(path, out var bytes))
        {
            return IconTheming.CreateFailedAsset(ThemeProblem.AssetUnreadable, path);
        }

        return new IconAsset(bytes, Family: "memory:" + path);
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

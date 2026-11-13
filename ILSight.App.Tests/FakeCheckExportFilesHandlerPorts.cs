using ILSight.Core;
using ILSight.ExportArea;

namespace ILSight.App.Tests;

/// <summary>Looks at no disk: the folders and files the test names are there, the folders holding files; keeps what it
/// was asked to look at.</summary>
internal sealed class FakeCheckExportFilesHandlerPorts : CheckExportFilesHandler.IPorts
{
    public HashSet<string> FilledFolders { get; init; } = [];

    public HashSet<string> Files { get; init; } = [];

    public List<string> Asked { get; } = [];

    /// <summary>The folders listing fails for, and why.</summary>
    public Dictionary<string, Exception> Unlistable { get; init; } = [];

    public Task<Outcome<bool>> LookInFolderAsync(string folder, CancellationToken ct)
    {
        Asked.Add(folder);
        if (Unlistable.TryGetValue(folder, out var why))
        {
            return Task.FromResult<Outcome<bool>>(new Outcome<bool>.Failed(why));
        }

        return Task.FromResult<Outcome<bool>>(new Outcome<bool>.Ok(FilledFolders.Contains(folder)));
    }

    public Task<bool> IsFileThereAsync(string file, CancellationToken ct)
    {
        Asked.Add(file);
        return Task.FromResult(Files.Contains(file));
    }
}

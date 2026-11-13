using ILSight.ExportArea;

namespace ILSight.App.Tests;

/// <summary>Looks at no disk: the folders and files the test names are there, the folders holding files; keeps what it
/// was asked to look at.</summary>
internal sealed class FakeCheckExportFilesHandlerPorts : CheckExportFilesHandler.IPorts
{
    public HashSet<string> FilledFolders { get; init; } = [];

    public HashSet<string> Files { get; init; } = [];

    public List<string> Asked { get; } = [];

    public Task<bool> HasEntriesAsync(string folder, CancellationToken ct)
    {
        Asked.Add(folder);
        return Task.FromResult(FilledFolders.Contains(folder));
    }

    public Task<bool> IsFileThereAsync(string file, CancellationToken ct)
    {
        Asked.Add(file);
        return Task.FromResult(Files.Contains(file));
    }
}

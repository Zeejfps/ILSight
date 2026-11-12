using System.Diagnostics;
using ILSight.Core;
using ILSight.Core.Diagrams;

namespace ILSight.ExportArea;

/// <summary>Reaches the app's error reports, the clock and the disk, for <see cref="ExportWriting"/>,
/// <see cref="SaveResourceHandler"/>, <see cref="ExtractPackageFileHandler"/> and
/// <see cref="CheckExportFilesHandler"/>. An export's work runs on the thread pool and comes back on the UI thread, on a
/// token its turn's cancellation and the workflow's both cancel: whatever it threw comes back as
/// <see cref="ExportOutcome{T}.Failed"/>, its turn's cancellation as <see cref="ExportOutcome{T}.Stopped"/>, and the
/// workflow's own cancellation is let through.</summary>
public sealed class ExportInfrastructure(ErrorLog errors, BrowseSession session)
    : ExportWriting.IPorts, SaveResourceHandler.IPorts, ExtractPackageFileHandler.IPorts, CheckExportFilesHandler.IPorts
{
    public long Now => Stopwatch.GetTimestamp();

    public TimeSpan MeasureSince(long start)
    {
        return Stopwatch.GetElapsedTime(start);
    }

    public bool FolderExists(string folder)
    {
        return Directory.Exists(folder);
    }

    public void ReportError(AppProblem problem)
    {
        Errors.Report(errors, problem);
    }

    /// <remarks>A module the library could not read, for code that reads it, fails the export, saying why.</remarks>
    public Task<ExportOutcome<string>> WriteCodeFileAsync(ExportWork.CodeFile work, ExportStart start, CancellationToken ct)
    {
        return RunOverTypeSystemAsync(token => CodeFiles.SaveAsync(session, work.Node, work.Language, work.Options, work.File, token), start, ct);
    }

    public Task<ExportOutcome<ExportedProject>> WriteProjectAsync(ExportWork.Project work, ExportStart start, CancellationToken ct)
    {
        return RunAsync(token => AssemblyExport.ExportProjectAsync(work.Assembly, work.Options, work.ProjectFile, work.Extras, start.Progress, token), start, ct);
    }

    public Task<ExportOutcome<List<ExportedProject>>> WriteSolutionAsync(ExportWork.Solution work, ExportStart start, CancellationToken ct)
    {
        return RunAsync(token => AssemblyExport.ExportSolutionAsync(work.Projects, work.Options, work.SolutionFile, work.Extras, start.Progress, token), start,
            ct);
    }

    /// <remarks>An assembly the library could not read fails the export, saying why.</remarks>
    public Task<ExportOutcome<string>> WritePdbAsync(ExportWork.Pdb work, ExportStart start, CancellationToken ct)
    {
        return RunOverTypeSystemAsync(token => AssemblyExport.GeneratePdbAsync(work.Assembly, work.Options, work.File, start.Progress, token), start, ct);
    }

    public Task<ExportOutcome<List<PdbFailure>>> WritePdbsAsync(ExportWork.Pdbs work, ExportStart start, CancellationToken ct)
    {
        return RunAsync(token => AssemblyExport.GeneratePdbsAsync(work.Assemblies, work.Options, work.Folder, start.Progress, token), start, ct);
    }

    /// <remarks>An assembly the library could not read fails the export, saying why.</remarks>
    public Task<ExportOutcome<string>> WriteDiagramAsync(ExportWork.Diagram work, ExportStart start, CancellationToken ct)
    {
        return RunOverTypeSystemAsync(token => ClassDiagrams.WriteAsync(work.Assembly, work.Folder, start.Progress, token), start, ct);
    }

    public Task<ExportOutcome<ExtractedFiles>> ExtractPackageFolderAsync(ExportWork.PackageFolder work, ExportStart start, CancellationToken ct)
    {
        return RunAsync(token => PackageExtraction.ExtractFolder(work.Place.Container.Package, work.Place.Folder, work.Folder, start.Progress, token), start,
            ct);
    }

    /// <summary>Runs <paramref name="work"/> on the thread pool on a token that the cancellation of
    /// <paramref name="start"/>'s turn and of <paramref name="ct"/> both cancel, and comes back on the UI thread.</summary>
    private static async Task<ExportOutcome<T>> RunAsync<T>(Func<CancellationToken, T> work, ExportStart start, CancellationToken ct)
    {
        using var link = CancellationTokenSource.CreateLinkedTokenSource(start.Token, ct);
        var token = link.Token;
        var outcome = await Background.Run(() => work(token));
        ct.ThrowIfCancellationRequested();
        return outcome.Match(static ok => new ExportOutcome<T>.Done(ok.Value), failed => Translate<T>(failed.Error, start));
    }

    /// <inheritdoc cref="RunAsync{T}(Func{CancellationToken, T}, ExportStart, CancellationToken)"/>
    private static async Task<ExportOutcome<T>> RunAsync<T>(Func<CancellationToken, Task<T>> work, ExportStart start, CancellationToken ct)
    {
        using var link = CancellationTokenSource.CreateLinkedTokenSource(start.Token, ct);
        var token = link.Token;
        var outcome = await Background.Wait(Task.Run(() => work(token), CancellationToken.None));
        ct.ThrowIfCancellationRequested();
        return outcome.Match(static ok => new ExportOutcome<T>.Done(ok.Value), failed => Translate<T>(failed.Error, start));
    }

    /// <summary>Runs <paramref name="work"/> over a module's type system as <see cref="RunAsync{T}(Func{CancellationToken, Task{T}}, ExportStart, CancellationToken)"/>
    /// does; a module the library could not read fails it, saying why.</summary>
    private static async Task<ExportOutcome<T>> RunOverTypeSystemAsync<T>(Func<CancellationToken, Task<TypeSystemWork<T>>> work, ExportStart start,
        CancellationToken ct)
    {
        var outcome = await RunAsync(work, start, ct);
        return outcome.Match(
            static done => done.Value.Match<ExportOutcome<T>>(static d => new ExportOutcome<T>.Done(d.Value), static u => new ExportOutcome<T>.Failed(u.Module.Why)),
            static stopped => new ExportOutcome<T>.Stopped(),
            static failed => new ExportOutcome<T>.Failed(failed.Error));
    }

    /// <summary>What <paramref name="error"/>, which an export's work ended with, means: it stopped when it is the
    /// cancellation of <paramref name="start"/>'s turn, else it failed.</summary>
    private static ExportOutcome<T> Translate<T>(Exception error, ExportStart start)
    {
        if (error is OperationCanceledException && start.Token.IsCancellationRequested)
        {
            return new ExportOutcome<T>.Stopped();
        }

        return new ExportOutcome<T>.Failed(error);
    }

    public Outcome SaveResource(BrowseNode node, string path)
    {
        try
        {
            var bytes = BrowseNodes.ReadResource(node);
            File.WriteAllBytes(path, bytes);
            return new Outcome.Done();
        }
        catch (IOException e)
        {
            return new Outcome.Failed(e);
        }
        catch (UnauthorizedAccessException e)
        {
            return new Outcome.Failed(e);
        }
        catch (InvalidDataException e)
        {
            return new Outcome.Failed(e);
        }
    }

    public Outcome ExtractPackageFile(PackagePlace place, string path)
    {
        try
        {
            var bytes = AssemblyPackages.ReadFile(place.Container.Package, place.File);
            File.WriteAllBytes(path, bytes);
            return new Outcome.Done();
        }
        catch (IOException e)
        {
            return new Outcome.Failed(e);
        }
        catch (UnauthorizedAccessException e)
        {
            return new Outcome.Failed(e);
        }
        catch (InvalidDataException e)
        {
            return new Outcome.Failed(e);
        }
    }

    public async Task<bool> HasEntriesAsync(string folder, CancellationToken ct)
    {
        return await Task.Run(() => HasEntries(folder), ct);
    }

    private static bool HasEntries(string folder)
    {
        if (!Directory.Exists(folder))
        {
            return false;
        }

        return Directory.EnumerateFileSystemEntries(folder).Any();
    }

    public async Task<bool> FileExistsAsync(string file, CancellationToken ct)
    {
        return await Task.Run(() => File.Exists(file), ct);
    }
}

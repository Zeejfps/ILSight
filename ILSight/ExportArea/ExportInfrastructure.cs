using System.Diagnostics;
using ILSight.Core;
using ILSight.Core.Diagrams;

namespace ILSight.ExportArea;

/// <summary>Reaches the app's error reports, the clock and the disk, for <see cref="ExportWriting"/>,
/// <see cref="SaveResourceHandler"/>, <see cref="ExtractPackageFileHandler"/> and
/// <see cref="CheckExportFilesHandler"/>. An export's work runs on the thread pool and comes back on the UI thread, on a
/// token its turn's cancellation and the workflow's both cancel: what it threw comes back as
/// <see cref="ExportOutcome{T}.Failed"/> (the decompiler's reads of the user's assemblies, <see cref="UntrustedReads"/>),
/// its turn's cancellation as <see cref="ExportOutcome{T}.Stopped"/>, and the workflow's own cancellation is let
/// through.</summary>
public sealed class ExportInfrastructure(ErrorLog errors, RefusalLine refusals, BrowseSession session)
    : ExportWriting.IPorts, SaveResourceHandler.IPorts, ExtractPackageFileHandler.IPorts, CheckExportFilesHandler.IPorts
{
    public long Now => Stopwatch.GetTimestamp();

    public TimeSpan MeasureSince(long start)
    {
        return Stopwatch.GetElapsedTime(start);
    }

    public bool IsFolderThere(string folder)
    {
        return Directory.Exists(folder);
    }

    public void ReportError(AppProblem problem)
    {
        Errors.Report(errors, problem);
    }

    public void ShowRefusal(Refusal refusal)
    {
        refusals.Show(refusal);
    }

    /// <remarks>A module the library could not read, for code that reads it, fails the export, saying why.</remarks>
    public Task<ExportOutcome<string>> WriteCodeFileAsync(ExportWork.CodeFile work, ExportStart start, CancellationToken ct)
    {
        return RunOverTypeSystemAsync(token => CodeFiles.SaveAsync(session, work.Node, work.Language, work.Options, work.File, token), start, ct);
    }

    /// <remarks>An assembly closed before the export began stops it, writing nothing.</remarks>
    public Task<ExportOutcome<ExportedProject>> WriteProjectAsync(ExportWork.Project work, ExportStart start, CancellationToken ct)
    {
        return RunOverModulesAsync(token => AssemblyExport.ExportProjectAsync(work.Assembly, work.Options, work.ProjectFile, work.Extras, start.Progress, token),
            start, ct);
    }

    /// <remarks>An assembly closed before the export began stops it, writing nothing.</remarks>
    public Task<ExportOutcome<List<ExportedProject>>> WriteSolutionAsync(ExportWork.Solution work, ExportStart start, CancellationToken ct)
    {
        return RunOverModulesAsync(token => AssemblyExport.ExportSolutionAsync(work.Projects, work.Options, work.SolutionFile, work.Extras, start.Progress,
            token), start, ct);
    }

    /// <remarks>An assembly the library could not read fails the export, saying why.</remarks>
    public Task<ExportOutcome<string>> WritePdbAsync(ExportWork.Pdb work, ExportStart start, CancellationToken ct)
    {
        return RunOverTypeSystemAsync(token => AssemblyExport.GeneratePdbAsync(work.Assembly, work.Options, work.File, start.Progress, token), start, ct);
    }

    /// <remarks>An assembly closed before the export began stops it, writing nothing.</remarks>
    public Task<ExportOutcome<List<PdbFailure>>> WritePdbsAsync(ExportWork.Pdbs work, ExportStart start, CancellationToken ct)
    {
        return RunOverModulesAsync(token => AssemblyExport.GeneratePdbsAsync(work.Assemblies, work.Options, work.Folder, start.Progress, token), start, ct);
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
        return await CatchAsync(Background.Run(() => work(token)), start, ct);
    }

    /// <inheritdoc cref="RunAsync{T}(Func{CancellationToken, T}, ExportStart, CancellationToken)"/>
    private static async Task<ExportOutcome<T>> RunAsync<T>(Func<CancellationToken, Task<T>> work, ExportStart start, CancellationToken ct)
    {
        using var link = CancellationTokenSource.CreateLinkedTokenSource(start.Token, ct);
        var token = link.Token;
        return await CatchAsync(Background.Wait(Task.Run(() => work(token), CancellationToken.None)), start, ct);
    }

    /// <summary>What an export's <paramref name="work"/>, which decompiles the user's assemblies, came to: done with
    /// what it gave; stopped when its turn, <paramref name="start"/>, was cancelled; else failed with what it threw
    /// (<see cref="UntrustedReads"/>). The cancellation of <paramref name="ct"/>, the workflow's, is let through.</summary>
    private static async Task<ExportOutcome<T>> CatchAsync<T>(BackgroundWork<T> work, ExportStart start, CancellationToken ct)
    {
        var outcome = await UntrustedReads.CatchAsync(work, ct);
        return outcome.Match(static ok => new ExportOutcome<T>.Done(ok.Value), failed => ToFailedOrStopped<T>(failed.Error, start));
    }

    /// <summary>What <paramref name="error"/>, which an export's work ended with, means: it stopped when it is the
    /// cancellation of <paramref name="start"/>'s turn, else it failed.</summary>
    private static ExportOutcome<T> ToFailedOrStopped<T>(Exception error, ExportStart start)
    {
        if (error is OperationCanceledException && start.Token.IsCancellationRequested)
        {
            return new ExportOutcome<T>.Stopped();
        }

        return new ExportOutcome<T>.Failed(error);
    }

    /// <summary>Runs <paramref name="work"/> over a module's type system as <see cref="RunAsync{T}(Func{CancellationToken, Task{T}}, ExportStart, CancellationToken)"/>
    /// does; a module the library could not read fails it, saying why; one closed before it began stops it.</summary>
    private static async Task<ExportOutcome<T>> RunOverTypeSystemAsync<T>(Func<CancellationToken, Task<TypeSystemWork<T>>> work, ExportStart start,
        CancellationToken ct)
    {
        var outcome = await RunAsync(work, start, ct);
        return outcome.Match(
            static done => done.Value.Match<ExportOutcome<T>>(
                static d => new ExportOutcome<T>.Done(d.Value),
                static u => new ExportOutcome<T>.Failed(u.Module.Why),
                static _ => new ExportOutcome<T>.Stopped()),
            static stopped => new ExportOutcome<T>.Stopped(),
            static failed => new ExportOutcome<T>.Failed(failed.Error));
    }

    /// <summary>Runs <paramref name="work"/> over the modules it reads as
    /// <see cref="RunAsync{T}(Func{CancellationToken, Task{T}}, ExportStart, CancellationToken)"/> does; one closed before
    /// it began stops it.</summary>
    private static async Task<ExportOutcome<T>> RunOverModulesAsync<T>(Func<CancellationToken, Task<ModuleRead<T>>> work, ExportStart start,
        CancellationToken ct)
    {
        var outcome = await RunAsync(work, start, ct);
        return outcome.Match(
            static done => done.Value.Match<ExportOutcome<T>>(static read => new ExportOutcome<T>.Done(read.Value), static _ => new ExportOutcome<T>.Stopped()),
            static stopped => new ExportOutcome<T>.Stopped(),
            static failed => new ExportOutcome<T>.Failed(failed.Error));
    }

    /// <remarks>Reads the resource under a reading of its module, held until the file is written; one closed before
    /// answers <see cref="ResourceSave.Closed"/>. The resource is read (decoded, decompressed) on the thread pool and
    /// written asynchronously, so the frame thread waits on neither.</remarks>
    public async Task<ResourceSave> SaveResourceAsync(BrowseNode node, string path, CancellationToken ct)
    {
        using var reading = LoadedAssemblies.BeginReading();
        if (node.Assembly is LoadedModule module && !LoadedAssemblies.TryRead(reading, module))
        {
            return new ResourceSave.Closed { Assembly = module.FilePath.FileName };
        }

        try
        {
            var bytes = await Task.Run(() => BrowseNodes.ReadResource(node), ct);
            await File.WriteAllBytesAsync(path, bytes, ct);
            return new ResourceSave.Saved();
        }
        catch (IOException e)
        {
            return new ResourceSave.Failed { Error = e };
        }
        catch (UnauthorizedAccessException e)
        {
            return new ResourceSave.Failed { Error = e };
        }
        catch (InvalidDataException e)
        {
            return new ResourceSave.Failed { Error = e };
        }
    }

    /// <remarks>The file is read out of the package (decompressed) on the thread pool and written asynchronously, so
    /// the frame thread waits on neither.</remarks>
    public async Task<Outcome> ExtractPackageFileAsync(PackagePlace place, string path, CancellationToken ct)
    {
        try
        {
            var bytes = await Task.Run(() => AssemblyPackages.ReadFile(place.Container.Package, place.File), ct);
            await File.WriteAllBytesAsync(path, bytes, ct);
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

    /// <remarks>A folder the system refuses to list, or that fails to list, answers <see cref="Outcome{T}.Failed"/>.</remarks>
    public async Task<Outcome<bool>> LookInFolderAsync(string folder, CancellationToken ct)
    {
        try
        {
            return new Outcome<bool>.Ok(await Task.Run(() => HasEntries(folder), ct));
        }
        catch (IOException e)
        {
            return new Outcome<bool>.Failed(e);
        }
        catch (UnauthorizedAccessException e)
        {
            return new Outcome<bool>.Failed(e);
        }
    }

    private static bool HasEntries(string folder)
    {
        if (!Directory.Exists(folder))
        {
            return false;
        }

        return Directory.EnumerateFileSystemEntries(folder).Any();
    }

    public Task<bool> IsFileThereAsync(string file, CancellationToken ct)
    {
        return Task.Run(() => File.Exists(file), ct);
    }
}

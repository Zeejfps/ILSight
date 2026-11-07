using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using ILSight.Core;
using ILSight.Core.Diagrams;
using static ILSight.Navigation;

namespace ILSight;


/// <summary>Writing code, projects, solutions, portable PDBs, class diagrams and the files of packages to files in the
/// background, one export at a time in the order they were asked for, which <see cref="AppState.Export"/>
/// queues. Each that runs ends in an <see cref="ExportReport"/> in <see cref="AppState.FinishedExports"/>.
/// Resources and single files of packages are written straight away.</summary>
public static class Exports
{
    private const string ProjectExtension = ".csproj";
    private const string DiagramFolderSuffix = " diagram";

    /// <summary>Whether <paramref name="node"/> can be saved: it shows code, or it is a resource,
    /// whose bytes are saved.</summary>
    public static bool CanSaveCode([NotNullWhen(true)] BrowseNode? node)
    {
        if (node == null)
            return false;
        if (node.HasResource)
            return true;
        return NodeContentLoader.DependsOnLanguage(node);
    }

    /// <summary>Saves <paramref name="node"/> to the file at <paramref name="path"/>: a resource's
    /// bytes as they are, else its code in the current language, as one file. Projects come from the
    /// export dialog.</summary>
    public static void SaveCode(AppState appState, BrowseNode node, string path)
    {
        if (node.HasResource)
        {
            SaveResource(appState, node, path);
            return;
        }
        WriteCodeFile(appState, node, path);
    }

    /// <summary>Writes the bytes of the resource <paramref name="node"/> stands for to the file at
    /// <paramref name="path"/>.</summary>
    public static void SaveResource(AppState appState, BrowseNode node, string path)
    {
        try
        {
            var bytes = node.ReadResource();
            File.WriteAllBytes(path, bytes);
        }
        catch (Exception e) when (IsFileError(e))
        {
            appState.ReportError(new AppProblem.ResourceNotSaved(node.Text, e.Message));
        }
    }

    private static bool IsFileError(Exception e)
    {
        if (e is IOException)
            return true;
        if (e is UnauthorizedAccessException)
            return true;
        return e is InvalidDataException;
    }

    /// <summary>Writes the file of a package <paramref name="place"/> stands for, as the package holds
    /// it, to the file at <paramref name="path"/>.</summary>
    public static void ExtractPackageFile(AppState appState, PackagePlace place, string path)
    {
        try
        {
            var bytes = place.Container.Package.ReadFile(place.File);
            File.WriteAllBytes(path, bytes);
        }
        catch (Exception e) when (IsFileError(e))
        {
            appState.ReportError(new AppProblem.PackageFileNotExtracted(place.File.Name, e.Message));
        }
    }

    private static void WriteCodeFile(AppState appState, BrowseNode node, string path)
    {
        var language = appState.Language;
        var options = appState.DecompilerOptions;
        var session = appState.Session;
        var job = new ExportJob(node.Text, CodeKindOf(language), FolderOf(path));
        RunExport(appState, job, node.Text, s => SaveCodeFile(session, node, language, options, path, s.Token), (r, _) => ReportWritten(r, path));
    }

    private static ExportKind CodeKindOf(DecompilationLanguage language)
    {
        if (language == DecompilationLanguage.CSharp)
            return ExportKind.CSharpFile;
        return ExportKind.ILFile;
    }

    private static bool SaveCodeFile(BrowseSession session, BrowseNode node, DecompilationLanguage language, DecompilerOptions options,
        string path, CancellationToken cancellationToken)
    {
        NodeContentLoader.SaveCode(session, node, language, options, path, cancellationToken);
        return true;
    }

    private static string FolderOf(string path) => Path.GetDirectoryName(path)!;

    /// <summary>Queues <paramref name="job"/> and shows the exports panel, where it waits, runs and
    /// ends. Once its turn comes, does <paramref name="work"/> in the background, then records how it
    /// ended, exporting <paramref name="subject"/> as a failure tells, and lets <paramref name="complete"/>
    /// add what a finished one wrote. One taken out of the queue leaves nothing behind.</summary>
    private static void RunExport<T>(AppState appState, ExportJob job, string subject, Func<ExportStart, T> work,
        Func<ExportReport, T, ExportReport> complete) =>
        Background.Start(RunExportAsync(appState, job, subject, work, complete), appState.ReportError);

    private static async Task RunExportAsync<T>(AppState appState, ExportJob job, string subject, Func<ExportStart, T> work,
        Func<ExportReport, T, ExportReport> complete)
    {
        appState.EnqueueCommand(new ViewCommand.OpenPanel(DockView.Exports));
        var turn = await Exports.EnterQueue(appState.Export, job);
        if (turn is not ExportStart start)
            return;
        var clock = Stopwatch.StartNew();
        var outcome = await Background.Run(() => work(start));
        Exports.LeaveQueue(appState.Export, start);
        var report = outcome switch
        {
            Outcome<T>.Ok ok => complete(ReportFinished(start.Run, job, clock.Elapsed), ok.Value),
            Outcome<T>.Failed failed => ReportEnded(start.Run, job, failed.Error, subject, clock.Elapsed),
            _ => throw new UnreachableException(),
        };
        appState.FinishedExports.Add(report);
    }

    /// <summary>The report of the export <paramref name="run"/> of <paramref name="job"/> that finished,
    /// with nothing written yet.</summary>
    private static ExportReport ReportFinished(ExportRun run, ExportJob job, TimeSpan elapsed)
    {
        return new ExportReport(run, job, ExportOutcome.Finished, [], [], elapsed, Directory.Exists(job.Folder));
    }

    /// <summary>The report of the export <paramref name="run"/> of <paramref name="job"/> that ended
    /// with <paramref name="error"/>: cancelled when it was cancelled, else failed, exporting
    /// <paramref name="subject"/>.</summary>
    private static ExportReport ReportEnded(ExportRun run, ExportJob job, Exception error, string subject, TimeSpan elapsed)
    {
        var folderExists = Directory.Exists(job.Folder);
        if (error is OperationCanceledException)
            return new ExportReport(run, job, ExportOutcome.Cancelled, [], [], elapsed, folderExists);
        var problem = new ExportProblem.Failed(subject, error);
        return new ExportReport(run, job, ExportOutcome.Failed, [], [problem], elapsed, folderExists);
    }

    /// <summary><paramref name="report"/> listing <paramref name="written"/>.</summary>
    private static ExportReport ReportWritten(ExportReport report, params string[] written)
    {
        return report with { Written = written };
    }

    /// <summary>Decompiles <paramref name="assembly"/> into a C# project whose file is
    /// <paramref name="projectPath"/>, with what <paramref name="extras"/> asks for besides. The export
    /// dialog warns about a folder that holds files before this is asked for.</summary>
    public static void ExportProject(AppState appState, LoadedModule assembly, string projectPath, ProjectExtras? extras = null)
    {
        var withExtras = extras ?? ProjectExtras.None;
        var options = appState.DecompilerOptions;
        var job = new ExportJob(Path.GetFileNameWithoutExtension(projectPath), ExportKind.Project, FolderOf(projectPath));
        RunExport(appState, job, assembly.Name, s => AssemblyExport.ExportProject(assembly, options, projectPath, withExtras, s.Progress, s.Token),
            (r, project) => ReportProjects(r, [project], projectPath));
    }

    /// <summary><paramref name="report"/> listing <paramref name="first"/>, each of
    /// <paramref name="projects"/> and the PDBs written beside them as written, with the parts the
    /// projects could not decompile and the PDBs that could not be written as its problems.</summary>
    private static ExportReport ReportProjects(ExportReport report, IReadOnlyList<ExportedProject> projects, string first)
    {
        var pdbs = projects
            .Select(static p => p.Pdb)
            .OfType<ProjectPdb.Written>()
            .Select(static p => p.Path);
        var written = projects
            .Select(static p => p.ProjectPath)
            .Prepend(first)
            .Concat(pdbs)
            .Distinct()
            .ToList();
        var pdbProblems = projects
            .Select(static p => p.Pdb)
            .OfType<ProjectPdb.Failed>()
            .Select(static p => ProblemOf(p.Failure));
        var problems = projects
            .SelectMany(static p => p.Errors)
            .Select(static ExportProblem (e) => new ExportProblem.NotDecompiled(e))
            .Concat(pdbProblems)
            .ToList();
        return report with { Written = written, Problems = problems };
    }

    /// <summary>Decompiles each of <paramref name="projects"/> into a C# project in a folder of its own
    /// beside the solution at <paramref name="solutionPath"/>.</summary>
    private static void ExportSolution(AppState appState, List<SolutionProject> projects, string solutionPath, ProjectExtras extras)
    {
        var options = appState.DecompilerOptions;
        var job = new ExportJob(Path.GetFileNameWithoutExtension(solutionPath), ExportKind.Solution, FolderOf(solutionPath));
        RunExport(appState, job, "", s => AssemblyExport.ExportSolution(projects, options, solutionPath, extras, s.Progress, s.Token),
            (r, exported) => ReportProjects(r, exported, solutionPath));
    }

    /// <summary>The assembly of the node the active tab shows, or null when it shows none that was read.</summary>
    public static LoadedModule? FindActiveAssembly(DocumentTabs tabs)
    {
        if (tabs.Active?.Node?.Assembly is not LoadedModule assembly)
            return null;
        return assembly;
    }

    /// <summary>Exports what <paramref name="request"/> asks for: one assembly as a project straight
    /// into the folder, several as a solution with a folder per project.</summary>
    public static void StartExport(AppState appState, ExportRequest request)
    {
        var folder = request.Folder;
        var projects = request.Projects;
        if (request.IsSolution)
        {
            var solutionPath = Path.Combine(folder, request.SolutionFileName);
            ExportSolution(appState, projects, solutionPath, request.Extras);
            return;
        }
        var project = projects[0];
        var projectPath = Path.Combine(folder, AssemblyExport.CleanProjectName(project.Name) + ProjectExtension);
        ExportProject(appState, project.Assembly, projectPath, request.Extras);
    }

    /// <summary>Writes a portable PDB of <paramref name="assembly"/>'s decompiled C# to
    /// <paramref name="path"/>.</summary>
    public static void GeneratePdb(AppState appState, LoadedModule assembly, string path)
    {
        var options = appState.DecompilerOptions;
        var job = new ExportJob(assembly.Name, ExportKind.Pdb, FolderOf(path));
        RunExport(appState, job, assembly.Name, s => GeneratePdbFile(assembly, options, path, s), (r, _) => ReportWritten(r, path));
    }

    private static bool GeneratePdbFile(LoadedModule assembly, DecompilerOptions options, string path, ExportStart start)
    {
        AssemblyExport.GeneratePdb(assembly, options, path, start.Progress, start.Token);
        return true;
    }

    /// <summary>Writes the HTML class diagrammer of <paramref name="assembly"/> into a folder of its
    /// own in <paramref name="folder"/>, named after it, and opens it once written.</summary>
    public static void CreateDiagram(AppState appState, LoadedModule assembly, string folder)
    {
        var diagramFolder = Path.Combine(folder, assembly.Name + DiagramFolderSuffix);
        var job = new ExportJob(assembly.Name, ExportKind.Diagram, diagramFolder);
        RunExport(appState, job, assembly.Name, s => ClassDiagrams.Write(assembly, diagramFolder, s.Progress, s.Token),
            (r, page) => OpenDiagram(appState, r, page));
    }

    /// <summary><paramref name="report"/> listing the diagrammer's <paramref name="page"/>, which is
    /// opened.</summary>
    private static ExportReport OpenDiagram(AppState appState, ExportReport report, string page)
    {
        appState.EnqueueCommand(new ViewCommand.OpenFile(page));
        return ReportWritten(report, page);
    }

    /// <summary>Writes a portable PDB of each open assembly into <paramref name="folder"/>, named
    /// after the assembly. Assemblies that cannot have one, or fail, are the report's problems.</summary>
    public static void GeneratePdbs(AppState appState, string folder)
    {
        var options = appState.DecompilerOptions;
        var assemblies = appState.Assemblies.Assemblies;
        var job = new ExportJob(Path.GetFileName(folder), ExportKind.Pdbs, folder);
        RunExport(appState, job, "", s => AssemblyExport.GeneratePdbs(assemblies, options, folder, s.Progress, s.Token),
            (r, failures) => ReportPdbs(r, assemblies, failures, folder));
    }

    /// <summary><paramref name="report"/> listing the PDB of each of <paramref name="assemblies"/> that
    /// is not among <paramref name="failures"/> as written, and the failures as its problems.</summary>
    private static ExportReport ReportPdbs(ExportReport report, IReadOnlyList<LoadedModule> assemblies, List<PdbFailure> failures,
        string folder)
    {
        var failed = failures
            .Select(static f => f.Assembly)
            .ToHashSet();
        var written = assemblies
            .Where(a => !failed.Contains(a))
            .Select(a => Path.Combine(folder, a.PdbFileName))
            .ToList();
        var problems = failures.ConvertAll(ProblemOf);
        return report with { Written = written, Problems = problems };
    }

    private static ExportProblem ProblemOf(PdbFailure failure) =>
        failure switch
        {
            PdbFailure.NoDebugEntry f => new ExportProblem.NoPdbEntry(f.Assembly.Name),
            PdbFailure.NotWritten f => new ExportProblem.PdbNotWritten(f.Assembly.Name, f.Error),
            _ => throw new UnreachableException(),
        };

    /// <summary>Writes every file in the folder of a package <paramref name="place"/> stands for, shown as
    /// <paramref name="name"/>, into <paramref name="folder"/>, at its path from that folder. Files that
    /// cannot be written there are the report's problems.</summary>
    public static void ExtractPackageFolder(AppState appState, string name, PackageFolderPlace place, string folder)
    {
        var job = new ExportJob(name, ExportKind.Files, folder);
        RunExport(appState, job, name, s => place.Container.Package.ExtractFolder(place.Folder, folder, s.Progress, s.Token),
            (r, extracted) => ReportExtracted(r, extracted));
    }

    /// <summary><paramref name="report"/> listing the files <paramref name="extracted"/> wrote, and those
    /// it could not as its problems.</summary>
    private static ExportReport ReportExtracted(ExportReport report, ExtractedFiles extracted)
    {
        var problems = extracted.Failures
            .Select(ProblemOf)
            .ToList();
        return report with { Written = extracted.Written, Problems = problems };
    }

    private static ExportProblem ProblemOf(ExtractFailure failure) =>
        failure switch
        {
            ExtractFailure.OutsideFolder f => new ExportProblem.OutsideFolder(f.File.FullName),
            ExtractFailure.NotWritten f => new ExportProblem.FileNotWritten(f.File.FullName, f.Error),
            _ => throw new UnreachableException(),
        };

    /// <summary>Shows the report of <paramref name="report"/> in a new tab.</summary>
    public static void ShowExportReport(AppState appState, ExportReport report)
    {
        var tab = OpenNewTab(appState);
        appState.Tabs.MakeActive(tab);
        tab.ShowReport(report);
    }

    /// <summary>The runs of every export in the order the exports panel lists them: the running one,
    /// the waiting ones next first, then the finished ones newest first.</summary>
    public static List<ExportRun> ListExports(ExportQueue queue, List<ExportReport> finished)
    {
        var runs = new List<ExportRun>();
        var running = queue.Running;
        if (running != null)
            runs.Add(running.Export.Run);
        foreach (var waiting in queue.Waiting)
            runs.Add(waiting.Run);
        for (var i = finished.Count - 1; i >= 0; i--)
            runs.Add(finished[i].Run);
        return runs;
    }

    /// <summary>The finished export <paramref name="run"/>, or null when it has not finished or is gone.</summary>
    public static ExportReport? FindFinishedExport(List<ExportReport> finished, ExportRun run) => finished.Find(r => r.Run == run);

    /// <summary>The folder the export <paramref name="run"/> writes into, wherever it is in the
    /// queue, or null when it is gone.</summary>
    public static string? FindExportFolder(ExportQueue queue, List<ExportReport> finished, ExportRun run)
    {
        var report = FindFinishedExport(finished, run);
        if (report != null)
            return report.Folder;
        var queued = FindQueuedExport(queue, run);
        if (queued == null)
            return null;
        return queued.Job.Folder;
    }

    private static QueuedExport? FindQueuedExport(ExportQueue queue, ExportRun run)
    {
        var running = queue.Running;
        if (running != null && running.Export.Run == run)
            return running.Export;
        return queue.Waiting.FirstOrDefault(q => q.Run == run);
    }

    /// <summary>Does what activating the export <paramref name="run"/> means: shows the report of a
    /// finished one; nothing for one still running or waiting.</summary>
    public static void ActivateExport(AppState appState, ExportRun run)
    {
        var finished = FindFinishedExport(appState.FinishedExports, run);
        if (finished == null)
            return;
        ShowExportReport(appState, finished);
    }

    /// <summary>Takes the export <paramref name="run"/> off the panel: out of the queue when it waits,
    /// off the history when it finished. A running one stays; only cancelling stops it.</summary>
    public static void RemoveExport(AppState appState, ExportRun run)
    {
        var finished = FindFinishedExport(appState.FinishedExports, run);
        if (finished != null)
        {
            RemoveFinishedExport(appState, finished);
            return;
        }
        Exports.WithdrawExport(appState.Export, run);
    }

    /// <summary>Takes <paramref name="report"/> off the list of finished exports.</summary>
    public static void RemoveFinishedExport(AppState appState, ExportReport report) => appState.FinishedExports.Remove(report);

    public static void ClearFinishedExports(AppState appState) => appState.FinishedExports.Clear();

    /// <summary>Queues <paramref name="job"/>, which has one part until it reports otherwise. The task
    /// completes when its turn comes: at once when nothing runs.</summary>
    public static Task<ExportTurn> EnterQueue(ExportQueue queue, ExportJob job)
    {
        queue.LastRun++;
        var started = new TaskCompletionSource<ExportTurn>(TaskCreationOptions.RunContinuationsAsynchronously);
        queue.Turns.Add(new ExportQueue.Turn(new QueuedExport(new ExportRun(queue.LastRun), job), started));
        if (!queue.IsRunning)
            StartNextExport(queue);
        return started.Task;
    }

    private static void StartNextExport(ExportQueue queue)
    {
        var turn = queue.Turns[0];
        queue.Turns.RemoveAt(0);
        var export = turn.Export;
        var active = new ExportQueue.Active { Shown = new RunningExport(export, new ExportProgress(export.Job.Name, 1, 1, null), false) };
        queue.ActiveExport = active;
        turn.Started.SetResult(new ExportStart(export.Run, active.Cancel.Token, CreateProgress(queue, export.Run)));
    }

    private static Progress<ExportProgress> CreateProgress(ExportQueue queue, ExportRun run)
    {
        return new Progress<ExportProgress>(p => TakeProgress(queue, run, p));
    }

    private static void TakeProgress(ExportQueue queue, ExportRun run, ExportProgress progress)
    {
        var active = FindActiveExport(queue, run);
        if (active == null)
            return;
        var shown = active.Shown;
        var furthest = GetFurthest(shown.Progress.Fraction, progress.Fraction);
        active.Shown = shown with { Progress = progress with { Fraction = furthest } };
    }

    /// <summary>The further of two fractions of one export: reports sent from several threads can
    /// arrive out of order, and the bar must not step back.</summary>
    private static float? GetFurthest(float? shown, float? reported)
    {
        if (shown == null)
            return reported;
        if (reported == null)
            return shown;
        return Math.Max(shown.Value, reported.Value);
    }

    /// <summary>Asks the running export to stop; the waiting ones still get their turn.</summary>
    public static void CancelRunning(ExportQueue queue)
    {
        if (queue.ActiveExport == null)
            return;
        queue.ActiveExport.Shown = queue.ActiveExport.Shown with { IsCancelling = true };
        queue.ActiveExport.Cancel.Cancel();
    }

    /// <summary>Takes the waiting export <paramref name="run"/> out of the queue.</summary>
    public static void WithdrawExport(ExportQueue queue, ExportRun run)
    {
        var index = queue.Turns.FindIndex(t => t.Export.Run == run);
        if (index < 0)
            return;
        var turn = queue.Turns[index];
        queue.Turns.RemoveAt(index);
        turn.Started.SetResult(new ExportWithdrawn());
    }

    /// <summary>Ends the turn of the export <paramref name="start"/> began, when it is the running
    /// one, and starts the next.</summary>
    public static void LeaveQueue(ExportQueue queue, ExportStart start)
    {
        var active = FindActiveExport(queue, start.Run);
        if (active == null)
            return;
        active.Cancel.Dispose();
        queue.ActiveExport = null;
        if (queue.Turns.Count == 0)
            return;
        StartNextExport(queue);
    }

    /// <summary>The running export's state when <paramref name="run"/> is the running one; null otherwise.</summary>
    private static ExportQueue.Active? FindActiveExport(ExportQueue queue, ExportRun run)
    {
        if (queue.ActiveExport == null)
            return null;
        if (queue.ActiveExport.Shown.Export.Run != run)
            return null;
        return queue.ActiveExport;
    }
}

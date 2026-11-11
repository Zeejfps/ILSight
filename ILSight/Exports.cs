using ZGF.Gui.Immediate.Widgets;
using static ILSight.Exports;
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
        if (node is null)
        {
            return false;
        }

        if (node.HasResource)
        {
            return true;
        }

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
            var bytes = BrowseNodes.ReadResource(node);
            File.WriteAllBytes(path, bytes);
        }
        catch (Exception e) when (IsFileError(e))
        {
            Errors.Report(appState, new AppProblem.ResourceNotSaved(node.Text, e.Message));
        }
    }

    private static bool IsFileError(Exception e)
    {
        if (e is IOException)
        {
            return true;
        }

        if (e is UnauthorizedAccessException)
        {
            return true;
        }

        return e is InvalidDataException;
    }

    /// <summary>Writes the file of a package <paramref name="place"/> stands for, as the package holds
    /// it, to the file at <paramref name="path"/>.</summary>
    public static void ExtractPackageFile(AppState appState, PackagePlace place, string path)
    {
        try
        {
            var bytes = AssemblyPackages.ReadFile(place.Container.Package, place.File);
            File.WriteAllBytes(path, bytes);
        }
        catch (Exception e) when (IsFileError(e))
        {
            Errors.Report(appState, new AppProblem.PackageFileNotExtracted(place.File.Name, e.Message));
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
        {
            return ExportKind.CSharpFile;
        }

        return ExportKind.ILFile;
    }

    private static bool SaveCodeFile(BrowseSession session, BrowseNode node, DecompilationLanguage language, DecompilerOptions options,
        string path, CancellationToken cancellationToken)
    {
        NodeContentLoader.SaveCode(session, node, language, options, path, cancellationToken);
        return true;
    }

    /// <summary>The folder of <paramref name="path"/>, which a save dialog gave, and so names a file in one.</summary>
    private static string FolderOf(string path)
    {
        var folder = Path.GetDirectoryName(path);
        if (folder is null)
        {
            throw new UnreachableException($"'{path}', which a save dialog gave, names no file in a folder.");
        }

        return folder;
    }

    /// <summary>Queues <paramref name="job"/> and shows the exports panel, where it waits, runs and
    /// ends. Once its turn comes, does <paramref name="work"/> in the background, then records how it
    /// ended, exporting <paramref name="subject"/> as a failure tells, and lets <paramref name="complete"/>
    /// add what a finished one wrote. One taken out of the queue leaves nothing behind.</summary>
    private static void RunExport<T>(AppState appState, ExportJob job, string subject, Func<ExportStart, T> work,
        Func<ExportReport, T, ExportReport> complete)
    {
        Background.Start(appState.Work, nameof(RunExportAsync), RunExportAsync(appState, job, subject, work, complete), problem => Errors.Report(appState, problem));
    }

    private static async Task RunExportAsync<T>(AppState appState, ExportJob job, string subject, Func<ExportStart, T> work,
        Func<ExportReport, T, ExportReport> complete)
    {
        appState.ViewCommands.Enqueue(new ViewCommand.OpenPanel(DockView.Exports));
        var turn = await appState.Export.EnterAsync(job);
        if (turn is not ExportStart start)
        {
            return;
        }

        var clock = Stopwatch.StartNew();
        var outcome = await Background.Run(() => work(start));
        start.EndTurn();
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
        var finished = new ExportEnd.Finished { Written = [], Problems = [] };
        return new ExportReport(run, job, finished, elapsed, Directory.Exists(job.Folder));
    }

    /// <summary>The report of the export <paramref name="run"/> of <paramref name="job"/> that ended
    /// with <paramref name="error"/>: cancelled when it was cancelled, else failed, exporting
    /// <paramref name="subject"/>.</summary>
    private static ExportReport ReportEnded(ExportRun run, ExportJob job, Exception error, string subject, TimeSpan elapsed)
    {
        var folderExists = Directory.Exists(job.Folder);
        if (error is OperationCanceledException)
        {
            return new ExportReport(run, job, new ExportEnd.Cancelled(), elapsed, folderExists);
        }

        var failed = new ExportEnd.Failed { Subject = subject, Error = error };
        return new ExportReport(run, job, failed, elapsed, folderExists);
    }

    /// <summary><paramref name="report"/> listing <paramref name="written"/>.</summary>
    private static ExportReport ReportWritten(ExportReport report, params string[] written)
    {
        return ReportFinished(report, written, []);
    }

    /// <summary><paramref name="report"/> of a finished export, listing <paramref name="written"/> and
    /// <paramref name="problems"/>.</summary>
    private static ExportReport ReportFinished(ExportReport report, IReadOnlyList<string> written, IReadOnlyList<ExportProblem> problems)
    {
        return report with { End = new ExportEnd.Finished { Written = written, Problems = problems } };
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
        return ReportFinished(report, written, problems);
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
        {
            return null;
        }

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
        appState.ViewCommands.Enqueue(new ViewCommand.OpenFile(page));
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
            .Select(a => Path.Combine(folder, LoadedAssemblies.GetPdbFileName(a)))
            .ToList();
        var problems = failures.ConvertAll(ProblemOf);
        return ReportFinished(report, written, problems);
    }

    private static ExportProblem ProblemOf(PdbFailure failure)
    {
        return failure switch
        {
            PdbFailure.NoDebugEntry f => new ExportProblem.NoPdbEntry(f.Assembly.Name),
            PdbFailure.NotWritten f => new ExportProblem.PdbNotWritten(f.Assembly.Name, f.Error),
            _ => throw new UnreachableException(),
        };
    }

    /// <summary>Writes every file in the folder of a package <paramref name="place"/> stands for, shown as
    /// <paramref name="name"/>, into <paramref name="folder"/>, at its path from that folder. Files that
    /// cannot be written there are the report's problems.</summary>
    public static void ExtractPackageFolder(AppState appState, string name, PackageFolderPlace place, string folder)
    {
        var job = new ExportJob(name, ExportKind.Files, folder);
        RunExport(appState, job, name, s => AssemblyPackages.ExtractFolder(place.Container.Package, place.Folder, folder, s.Progress, s.Token),
            (r, extracted) => ReportExtracted(r, extracted));
    }

    /// <summary><paramref name="report"/> listing the files <paramref name="extracted"/> wrote, and those
    /// it could not as its problems.</summary>
    private static ExportReport ReportExtracted(ExportReport report, ExtractedFiles extracted)
    {
        var problems = extracted.Failures
            .Select(ProblemOf)
            .ToList();
        return ReportFinished(report, extracted.Written, problems);
    }

    private static ExportProblem ProblemOf(ExtractFailure failure)
    {
        return failure switch
        {
            ExtractFailure.OutsideFolder f => new ExportProblem.OutsideFolder(f.File.FullName),
            ExtractFailure.NotWritten f => new ExportProblem.FileNotWritten(f.File.FullName, f.Error),
            _ => throw new UnreachableException(),
        };
    }

    /// <summary>Shows the report of <paramref name="report"/> in a new tab.</summary>
    public static void ShowExportReport(AppState appState, ExportReport report)
    {
        var tab = OpenNewTab(appState);
        Tabs.MakeActive(appState.Tabs, tab);
        Tabs.ShowReport(tab, report);
    }

    /// <summary>The runs of every export in the order the exports panel lists them: the running one,
    /// the waiting ones next first, then the finished ones newest first.</summary>
    public static List<ExportRun> ListExports(ExportQueue queue, List<ExportReport> finished)
    {
        var runs = new List<ExportRun>();
        var running = queue.Running;
        if (running is not null)
        {
            runs.Add(running.Export.Run);
        }

        foreach (var waiting in queue.Waiting)
        {
            runs.Add(waiting.Run);
        }

        for (var i = finished.Count - 1; i >= 0; i--)
        {
            runs.Add(finished[i].Run);
        }

        return runs;
    }

    /// <summary>The finished export <paramref name="run"/>, or null when it has not finished or is gone.</summary>
    public static ExportReport? FindFinishedExport(List<ExportReport> finished, ExportRun run)
    {
        return finished.Find(r => r.Run == run);
    }

    /// <summary>The folder the export <paramref name="run"/> writes into, wherever it is in the
    /// queue, or null when it is gone.</summary>
    public static string? FindExportFolder(ExportQueue queue, List<ExportReport> finished, ExportRun run)
    {
        var report = FindFinishedExport(finished, run);
        if (report is not null)
        {
            return report.Folder;
        }

        var queued = FindQueuedExport(queue, run);
        if (queued is null)
        {
            return null;
        }

        return queued.Job.Folder;
    }

    private static QueuedExport? FindQueuedExport(ExportQueue queue, ExportRun run)
    {
        var running = queue.Running;
        if (running is not null && running.Export.Run == run)
        {
            return running.Export;
        }

        return queue.Waiting.FirstOrDefault(q => q.Run == run);
    }

    /// <summary>Does what activating the export <paramref name="run"/> means: shows the report of a
    /// finished one; nothing for one still running or waiting.</summary>
    public static void ActivateExport(AppState appState, ExportRun run)
    {
        var finished = FindFinishedExport(appState.FinishedExports, run);
        if (finished is null)
        {
            return;
        }

        ShowExportReport(appState, finished);
    }

    /// <summary>Takes the export <paramref name="run"/> off the panel: out of the queue when it waits,
    /// off the history when it finished. A running one stays; only cancelling stops it.</summary>
    public static void RemoveExport(AppState appState, ExportRun run)
    {
        var finished = FindFinishedExport(appState.FinishedExports, run);
        if (finished is not null)
        {
            RemoveFinishedExport(appState, finished);
            return;
        }
        appState.Export.Withdraw(run);
    }

    /// <summary>Takes <paramref name="report"/> off the list of finished exports.</summary>
    public static void RemoveFinishedExport(AppState appState, ExportReport report)
    {
        appState.FinishedExports.Remove(report);
    }

    public static void ClearFinishedExports(AppState appState)
    {
        appState.FinishedExports.Clear();
    }

    private const string DefaultSolutionName = "Solution";

    /// <summary>Opens the dialog on <paramref name="assemblies"/> with <paramref name="picked"/>
    /// ticked, or the only one when there is just one, unless there are none. The folder stays as it
    /// was last time.</summary>
    public static void OpenExportSetup(ExportSetup setup, IReadOnlyList<LoadedModule> assemblies, LoadedModule? picked)
    {
        if (assemblies.Count == 0)
        {
            return;
        }

        setup.Choices.Clear();
        foreach (var project in AssemblyExport.NameProjects(assemblies))
        {
            setup.Choices.Add(new ExportChoice { Assembly = project.Assembly, Name = project.Name });
        }

        TickFirstChoice(setup, picked);
        setup.RowSelection.Clear();
        setup.SolutionName = "";
        setup.IsOpen = true;
        setup.Page = ExportPage.Main;
        if (setup.IncludedCount == 0)
        {
            ChooseAssemblies(setup);
        }
    }

    private static void TickFirstChoice(ExportSetup setup, LoadedModule? picked)
    {
        foreach (var choice in setup.Choices)
        {
            choice.Included = choice.Assembly == picked;
        }

        if (setup.Choices.Count == 1)
        {
            setup.Choices[0].Included = true;
        }
    }

    public static void CloseExportSetup(ExportSetup setup)
    {
        setup.IsOpen = false;
    }

    /// <summary>Shows the Choose page, its list arranged afresh.</summary>
    public static void ChooseAssemblies(ExportSetup setup)
    {
        ExportDialogView.Arrange(setup.Picker, setup.Choices);
        setup.Page = ExportPage.Choose;
    }

    /// <summary>Goes back from the Choose page to what is exported.</summary>
    public static void FinishChoosing(ExportSetup setup)
    {
        setup.Page = ExportPage.Main;
    }

    /// <summary>Unticks the assemblies whose rows are picked out, and picks out the row now where the
    /// first of them was.</summary>
    public static void RemoveSelectedRows(ExportSetup setup)
    {
        var order = ListIncludedChoices(setup);
        var removed = setup.RowSelection.InOrder(order);
        if (removed.Count == 0)
        {
            return;
        }

        var at = order.IndexOf(removed[0]);
        foreach (var choice in removed)
        {
            choice.Included = false;
        }

        setup.RowSelection.SelectAt(at, ListIncludedChoices(setup));
    }

    /// <summary>The ticked assemblies, in the order they were opened.</summary>
    public static List<ExportChoice> ListIncludedChoices(ExportSetup setup)
    {
        return setup.Choices
            .Where(static c => c.Included)
            .ToList();
    }

    /// <summary>Ticks every assembly when <paramref name="all"/>, else none.</summary>
    public static void IncludeAll(ExportSetup setup, bool all)
    {
        foreach (var choice in setup.Choices)
        {
            choice.Included = all;
        }
    }

    public static string GetSolutionNameOrDefault(ExportSetup setup)
    {
        var typed = setup.SolutionName.Trim();
        if (typed.Length > 0)
        {
            return typed;
        }

        return GetDefaultSolutionName(setup.Folder);
    }

    /// <summary>The name a solution in <paramref name="folder"/> gets when none is typed: the folder's.</summary>
    public static string GetDefaultSolutionName(string folder)
    {
        var trimmed = folder.Trim().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var name = Path.GetFileName(trimmed);
        if (string.IsNullOrEmpty(name))
        {
            return DefaultSolutionName;
        }

        return name;
    }

    /// <summary>Whether the folder already holds files, which the export may replace. Looked up
    /// again only when the folder changes.</summary>
    public static bool CheckFolderHoldsFiles(ExportSetup setup)
    {
        if (setup.FolderCheck.TryGet(setup.Folder, out var holdsFiles))
        {
            return holdsFiles;
        }

        holdsFiles = HoldsFiles(setup.Folder.Trim());
        setup.FolderCheck = new Memo<string, bool>(setup.Folder, holdsFiles);
        return holdsFiles;
    }

    private static bool HoldsFiles(string folder)
    {
        if (!Path.IsPathFullyQualified(folder))
        {
            return false;
        }

        if (!Directory.Exists(folder))
        {
            return false;
        }

        return Directory.EnumerateFileSystemEntries(folder).Any();
    }

    /// <summary>Whether a key file is named that is not there. Looked up again only when the name changes.</summary>
    public static bool CheckKeyFileMissing(ExportSetup setup)
    {
        if (setup.KeyFileCheck.TryGet(setup.StrongNameKeyFile, out var isMissing))
        {
            return isMissing;
        }

        isMissing = IsFileMissing(setup.StrongNameKeyFile.Trim());
        setup.KeyFileCheck = new Memo<string, bool>(setup.StrongNameKeyFile, isMissing);
        return isMissing;
    }

    private static bool IsFileMissing(string file)
    {
        if (file.Length == 0)
        {
            return false;
        }

        return !File.Exists(file);
    }

    /// <summary>The first thing that keeps the export from starting, or null: it needs at least one
    /// assembly, a folder, a name for each project that no other takes, and no key file that is not there.</summary>
    public static ExportBlocker? FindBlocker(ExportSetup setup)
    {
        if (setup.IncludedCount == 0)
        {
            return new ExportBlocker.NoAssembly();
        }

        if (!setup.HasFolder)
        {
            return new ExportBlocker.NoFolder();
        }

        if (setup.HasNamelessProject)
        {
            return new ExportBlocker.NamelessProject();
        }

        var clash = setup.Clash;
        if (clash is not null)
        {
            return new ExportBlocker.Clash(clash);
        }

        if (CheckKeyFileMissing(setup))
        {
            return new ExportBlocker.KeyFileMissing();
        }

        return null;
    }

    public static bool CanExport(ExportSetup setup)
    {
        return FindBlocker(setup) is null;
    }

    /// <summary>What each project is written with besides its code.</summary>
    public static ProjectExtras GetExtras(ExportSetup setup)
    {
        var key = setup.StrongNameKeyFile.Trim();
        var keyFile = key.Length == 0 ? null : key;
        return new ProjectExtras(keyFile, setup.GeneratePdb, setup.EmbedSourcesInPdb);
    }

    /// <summary>The export the dialog asks for.</summary>
    public static ExportRequest CreateRequest(ExportSetup setup)
    {
        return new ExportRequest(setup.Folder.Trim(), ListIncludedProjects(setup), GetExtras(setup), setup.SolutionFileName);
    }

    /// <summary>The ticked assemblies as the projects to export, in the dialog's order.</summary>
    public static List<SolutionProject> ListIncludedProjects(ExportSetup setup)
    {
        return setup.Choices
            .Where(static c => c.Included)
            .Select(static c => new SolutionProject(c.Assembly, c.Name.Trim()))
            .ToList();
    }
}

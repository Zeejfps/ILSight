using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using ILSight.Core;

namespace ILSight.ExportArea;


/// <summary>The exports of code, projects, solutions, portable PDBs, class diagrams and the files of packages: which
/// are asked for and queued in <see cref="AppState.Export"/>, to be written one at a time in the order they were asked
/// for (<see cref="ExportWriting"/>), the reports of those that ended in <see cref="AppState.FinishedExports"/>, and the
/// export dialog's choices. Resources and single files of packages are written straight away
/// (<see cref="SaveResourceHandler"/>, <see cref="ExtractPackageFileHandler"/>).</summary>
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

        return node.IsCode;
    }

    /// <summary>What saving <paramref name="node"/> to the file at <paramref name="path"/> writes: a resource's bytes as
    /// they are, else its code in <paramref name="setup"/>'s language, as one file.</summary>
    public static CodeSave ToCodeSave(BrowseNode node, DecompilerSetup setup, string path)
    {
        if (node.HasResource)
        {
            return new CodeSave.ResourceBytes { Node = node, File = path };
        }

        var work = new ExportWork.CodeFile { Node = node, Language = setup.Language, Options = setup.Options, File = path };
        return new CodeSave.Code { Work = work };
    }

    /// <summary>Queues the export <paramref name="job"/> and brings the exports panel to the front, where it waits,
    /// runs and ends; its turn comes once the exports queued before it have ended (<see cref="ExportWriting"/>).</summary>
    public static ExportTicket Enter(ExportQueue queue, Queue<ViewCommand> viewCommands, ExportJob job)
    {
        viewCommands.Enqueue(new ViewCommand.OpenPanel(DockView.Exports));
        return queue.Enter(job);
    }

    /// <summary><paramref name="report"/> listing <paramref name="written"/>.</summary>
    public static ExportReport ReportWritten(ExportReport report, params string[] written)
    {
        return ReportFinished(report, written, []);
    }

    /// <summary><paramref name="report"/> of a finished export, listing <paramref name="written"/> and
    /// <paramref name="problems"/>.</summary>
    private static ExportReport ReportFinished(ExportReport report, IReadOnlyList<string> written, IReadOnlyList<ExportProblem> problems)
    {
        return report with { End = new ExportEnd.Finished { Written = written, Problems = problems } };
    }

    /// <summary><paramref name="report"/> listing <paramref name="first"/>, each of
    /// <paramref name="projects"/> and the PDBs written beside them as written, with the parts the
    /// projects could not decompile and the PDBs that could not be written as its problems.</summary>
    public static ExportReport ReportProjects(ExportReport report, IReadOnlyList<ExportedProject> projects, string first)
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

    /// <summary>The assembly of the node the active tab shows, or null when it shows none that was read.</summary>
    public static LoadedModule? FindActiveAssembly(Navigation navigation)
    {
        if (navigation.ActiveTab?.Node?.Assembly is not LoadedModule assembly)
        {
            return null;
        }

        return assembly;
    }

    /// <summary>What <paramref name="request"/> asks to export, decompiled with <paramref name="options"/>: one
    /// assembly as a project straight into the folder, several as a solution with a folder per project.</summary>
    public static ExportWork ToWork(ExportRequest request, DecompilerOptions options)
    {
        var folder = request.Folder;
        var projects = request.Projects;
        if (request.IsSolution)
        {
            var solutionPath = Path.Combine(folder, request.SolutionFileName);
            return new ExportWork.Solution { Projects = projects, Options = options, SolutionFile = solutionPath, Extras = request.Extras };
        }

        var project = projects[0];
        var projectPath = Path.Combine(folder, ExportNames.CleanProjectName(project.Name) + ProjectExtension);
        return new ExportWork.Project { Assembly = project.Assembly, Options = options, ProjectFile = projectPath, Extras = request.Extras };
    }

    /// <summary><paramref name="report"/> listing the diagrammer's <paramref name="page"/>, which is opened through
    /// <paramref name="viewCommands"/>.</summary>
    public static ExportReport OpenDiagram(Queue<ViewCommand> viewCommands, ExportReport report, string page)
    {
        viewCommands.Enqueue(new ViewCommand.OpenFile(page));
        return ReportWritten(report, page);
    }

    /// <summary>The folder the class diagrammer of <paramref name="assembly"/> is written into: a folder of its own in
    /// <paramref name="folder"/>, named after it.</summary>
    public static string GetDiagramFolder(LoadedModule assembly, string folder)
    {
        return Path.Combine(folder, assembly.Name + DiagramFolderSuffix);
    }

    /// <summary><paramref name="report"/> listing the PDB of each of <paramref name="assemblies"/> that
    /// is not among <paramref name="failures"/> as written, and the failures as its problems.</summary>
    public static ExportReport ReportPdbs(ExportReport report, IReadOnlyList<LoadedModule> assemblies, List<PdbFailure> failures,
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
        return ReportFinished(report, written, problems);
    }

    private static ExportProblem ProblemOf(PdbFailure failure)
    {
        return failure.Match<ExportProblem>(
            static f => new ExportProblem.NoPdbEntry(f.Assembly.Name),
            static f => new ExportProblem.PdbNotWritten { Assembly = f.Assembly.Name, Error = f.Error });
    }

    /// <summary><paramref name="report"/> listing the files <paramref name="extracted"/> wrote, and those
    /// it could not as its problems.</summary>
    public static ExportReport ReportExtracted(ExportReport report, ExtractedFiles extracted)
    {
        var problems = extracted.Failures
            .Select(ProblemOf)
            .ToList();
        return ReportFinished(report, extracted.Written, problems);
    }

    private static ExportProblem ProblemOf(ExtractFailure failure)
    {
        return failure.Match<ExportProblem>(
            static f => new ExportProblem.OutsideFolder(f.File.FullName),
            static f => new ExportProblem.FileNotWritten { Path = f.File.FullName, Error = f.Error });
    }

    /// <summary>Shows the report of <paramref name="report"/> in a new tab.</summary>
    public static void ShowExportReport(AppState appState, ExportReport report)
    {
        appState.Navigation.ShowReport(report);
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
        foreach (var project in ExportNames.NameProjects(assemblies))
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

    /// <summary>The first thing that keeps the export from starting, or null: it needs at least one
    /// assembly, a folder, a name for each project that no other takes, and no key file that is not
    /// there, as <paramref name="files"/>, the look at the disk, says once made; null before it was asked for.</summary>
    public static ExportBlocker? FindBlocker(ExportSetup setup, ExportFileLook? files)
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

        if (files is null)
        {
            return new ExportBlocker.Checking();
        }

        return files.Match<ExportBlocker?>(
            static _ => new ExportBlocker.Checking(),
            static looked => FindFileBlocker(looked.Facts),
            static failed => new ExportBlocker.CheckFailed(failed.Why));
    }

    /// <summary>What the disk says that keeps the export from starting: a key file named that is not there.</summary>
    private static ExportBlocker? FindFileBlocker(ExportFileFacts files)
    {
        if (files.IsKeyFileMissing)
        {
            return new ExportBlocker.KeyFileMissing();
        }

        return null;
    }

    public static bool CanExport(ExportSetup setup, ExportFileLook? files)
    {
        return FindBlocker(setup, files) is null;
    }

    /// <summary>What each project is written with besides its code.</summary>
    public static ProjectExtras GetExtras(ExportSetup setup)
    {
        var key = setup.StrongNameKeyFile.Trim();
        var keyFile = key.Length == 0 ? null : key;
        return new ProjectExtras
        {
            StrongNameKeyFile = keyFile,
            Pdb = setup.GeneratePdb,
            EmbedSourcesInPdb = setup.EmbedSourcesInPdb,
        };
    }

    /// <summary>The export the dialog asks for.</summary>
    public static ExportRequest CreateRequest(ExportSetup setup)
    {
        return new ExportRequest
        {
            Folder = setup.Folder.Trim(),
            Projects = ListIncludedProjects(setup),
            Extras = GetExtras(setup),
            SolutionFileName = setup.SolutionFileName,
        };
    }

    /// <summary>The ticked assemblies as the projects to export, in the dialog's order.</summary>
    public static List<SolutionProject> ListIncludedProjects(ExportSetup setup)
    {
        return setup.Choices
            .Where(static c => c.Included)
            .Select(static c => new SolutionProject { Assembly = c.Assembly, Name = c.Name.Trim() })
            .ToList();
    }
}

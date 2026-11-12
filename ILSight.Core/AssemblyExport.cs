using ICSharpCode.Decompiler.TypeSystem;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp.ProjectDecompiler;
using ICSharpCode.Decompiler.DebugInfo;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.Solution;

namespace ILSight.Core;

/// <summary>Writing assemblies out as C# projects, solutions and portable PDBs, reporting how far
/// it has got and stopping when cancelled.</summary>
public static class AssemblyExport
{
    /// <summary>Decompiles <paramref name="assembly"/> into a project whose file is
    /// <paramref name="projectPath"/>, with its code and resources in the folder beside it, and
    /// writes what <paramref name="extras"/> asks for besides.</summary>
    /// <exception cref="ArgumentException"><paramref name="projectPath"/> is no absolute path of a file.</exception>
    public static async Task<ExportedProject> ExportProjectAsync(LoadedModule assembly, DecompilerOptions options, string projectPath, ProjectExtras? extras = null,
        IProgress<ExportProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        var part = Part(progress, assembly, 0, WholeExport);
        return await ExportProjectAsync(assembly, options, FullFilePath.Of(projectPath), extras ?? ProjectExtras.None, part, cancellationToken);
    }

    /// <summary>Exports the project, then the PDB when one is asked for; each takes half of the
    /// part's share of the export.</summary>
    private static async Task<ExportedProject> ExportProjectAsync(LoadedModule assembly, DecompilerOptions options, FullFilePath projectPath,
        ProjectExtras extras, PartProgress? part, CancellationToken cancellationToken)
    {
        var (projectPart, pdbPart) = SplitForPdb(part, extras.Pdb);
        var project = WriteProject(assembly, options, projectPath, extras.StrongNameKeyFile, projectPart, cancellationToken);
        if (!extras.Pdb)
        {
            return project;
        }

        return await WithPdbAsync(project, options, projectPath.Folder, extras.EmbedSourcesInPdb, pdbPart, cancellationToken);
    }

    private static (PartProgress? Project, PartProgress? Pdb) SplitForPdb(PartProgress? part, bool pdb)
    {
        if (part is null)
        {
            return (null, null);
        }

        if (!pdb)
        {
            return (part, null);
        }

        return GetHalves(part);
    }

    private static ExportedProject WriteProject(LoadedModule assembly, DecompilerOptions options, FullFilePath projectPath, string? strongNameKeyFile,
        IProgress<DecompilationProgress>? progress, CancellationToken cancellationToken)
    {
        var folder = projectPath.Folder;
        Directory.CreateDirectory(folder);
        var decompiler = CreateProjectDecompiler(assembly, options, cancellationToken);
        decompiler.ProgressIndicator = progress;
        decompiler.StrongNameKeyFile = strongNameKeyFile;
        using var projectFile = new StreamWriter(projectPath.Text);
        var id = decompiler.DecompileProject(LoadedAssemblies.GetFile(assembly), folder, projectFile, cancellationToken);
        var errors = decompiler.Errors
            .Select(static e => new ExportError { Message = e.Message, Details = e.ToString() })
            .ToList();
        return new ExportedProject { Assembly = assembly, ProjectPath = projectPath.Text, Errors = errors, Id = id };
    }

    private static WholeProjectDecompiler CreateProjectDecompiler(LoadedModule assembly, DecompilerOptions options, CancellationToken cancellationToken)
    {
        var settings = Settings.Create(options);
        var symbols = NodeDecompiler.DebugSymbolsFor(assembly, settings);
        return new BamlProjectDecompiler(assembly, settings, symbols, cancellationToken);
    }

    /// <summary><paramref name="project"/> with a portable PDB of its assembly written beside it, or
    /// with why it could not be.</summary>
    private static async Task<ExportedProject> WithPdbAsync(ExportedProject project, DecompilerOptions options, string folder, bool embedSources,
        IProgress<DecompilationProgress>? progress, CancellationToken cancellationToken)
    {
        var failure = await TryGeneratePdbAsync(project.Assembly, options, folder, embedSources, progress, cancellationToken);
        if (failure is not null)
        {
            return project with { Pdb = new ProjectPdb.Failed(failure) };
        }

        var path = Path.Combine(folder, project.Assembly.PdbFileName);
        return project with { Pdb = new ProjectPdb.Written(path) };
    }

    /// <summary>Decompiles each of <paramref name="projects"/> into a project in a folder of its own,
    /// named as the project, beside <paramref name="solutionPath"/>, then writes the solution that
    /// holds them.</summary>
    /// <exception cref="ArgumentException"><paramref name="solutionPath"/> is no absolute path of a file.</exception>
    public static async Task<List<ExportedProject>> ExportSolutionAsync(IReadOnlyList<SolutionProject> projects, DecompilerOptions options, string solutionPath,
        ProjectExtras? extras = null, IProgress<ExportProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        var withExtras = extras ?? ProjectExtras.None;
        var folder = FullFilePath.Of(solutionPath).Folder;
        var assemblies = projects.Select(static p => p.Assembly).ToList();
        var bounds = PartBounds(assemblies);
        var exported = new List<ExportedProject>();
        for (var i = 0; i < projects.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var project = projects[i];
            var projectPath = FullFilePath.Of(ExportNames.ProjectPathIn(folder, project.Name));
            var part = Part(progress, project.Assembly, i, bounds);
            exported.Add(await ExportProjectAsync(project.Assembly, options, projectPath, withExtras, part, cancellationToken));
        }
        var items = exported.ConvertAll(ToSolutionItem);
        SolutionCreator.WriteSolutionFile(solutionPath, items);
        return exported;
    }

    private static ProjectItem ToSolutionItem(ExportedProject project)
    {
        var id = project.Id;
        return new ProjectItem(project.ProjectPath, id.PlatformName, id.Guid, id.TypeGuid);
    }

    /// <summary>Decompiles <paramref name="assembly"/> and writes a portable PDB at <paramref name="path"/>
    /// that maps its IL to that code, with the code embedded. The file is replaced only once the PDB
    /// is whole.</summary>
    /// <remarks>Once the read of the assembly's type system ended: for an assembly the library could not read, says so
    /// and writes nothing.</remarks>
    public static Task<TypeSystemWork<string>> GeneratePdbAsync(LoadedModule assembly, DecompilerOptions options, string path,
        IProgress<ExportProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        var part = Part(progress, assembly, 0, WholeExport);
        return LoadedAssemblies.WithTypeSystemAsync(assembly, typeSystem => GeneratePdb(assembly, typeSystem, options, path, embedSources: true, part,
            cancellationToken), cancellationToken);
    }

    private static string GeneratePdb(LoadedModule assembly, DecompilerTypeSystem typeSystem, DecompilerOptions options, string path, bool embedSources,
        IProgress<DecompilationProgress>? progress, CancellationToken cancellationToken)
    {
        var settings = Settings.Create(options);
        var decompiler = NodeDecompiler.CreateCSharpDecompiler(assembly, typeSystem, settings, cancellationToken);
        var writer = new PortablePdbWriter { Progress = progress, EmbedSourceFiles = embedSources };
        AtomicFile.Write(path, stream => writer.WritePdb(PortableExecutableOf(assembly), decompiler, settings, stream));
        return path;
    }

    private static PEFile PortableExecutableOf(LoadedModule assembly)
    {
        var pe = LoadedAssemblies.FindPEFile(assembly);
        if (pe is null)
        {
            throw new InvalidOperationException($"'{assembly.FilePath}' is no portable executable, so no PDB can map its IL.");
        }

        return pe;
    }

    /// <summary>Writes a portable PDB of each of <paramref name="assemblies"/> into
    /// <paramref name="folder"/>, named after the assembly, and lists the ones it could not.</summary>
    public static async Task<List<PdbFailure>> GeneratePdbsAsync(IReadOnlyList<LoadedModule> assemblies, DecompilerOptions options, string folder,
        IProgress<ExportProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        var bounds = PartBounds(assemblies);
        var failures = new List<PdbFailure>();
        for (var i = 0; i < assemblies.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var assembly = assemblies[i];
            var part = Part(progress, assembly, i, bounds);
            var failure = await TryGeneratePdbAsync(assembly, options, folder, embedSources: true, part, cancellationToken);
            if (failure is not null)
            {
                failures.Add(failure);
            }
        }
        return failures;
    }

    /// <summary>Writes a portable PDB of <paramref name="assembly"/> into <paramref name="folder"/>, once the read of its
    /// type system ended; why it could not, the library failing on it among the reasons, else null.</summary>
    private static async Task<PdbFailure?> TryGeneratePdbAsync(LoadedModule assembly, DecompilerOptions options, string folder, bool embedSources,
        IProgress<DecompilationProgress>? progress, CancellationToken cancellationToken)
    {
        if (!assembly.CanGeneratePdb)
        {
            return new PdbFailure.NoDebugEntry { Assembly = assembly };
        }

        var read = await LoadedAssemblies.ReadTypeSystemAsync(assembly, cancellationToken);
        return read.Match(
            r => TryGeneratePdb(assembly, r.TypeSystem, options, folder, embedSources, progress, cancellationToken),
            u => new PdbFailure.NotWritten
            {
                Assembly = assembly,
                Error = new ExportError
                {
                    Message = u.Thrown.SourceException.Message,
                    Details = u.Thrown.SourceException.ToString(),
                },
            });
    }

    private static PdbFailure? TryGeneratePdb(LoadedModule assembly, DecompilerTypeSystem typeSystem, DecompilerOptions options, string folder,
        bool embedSources, IProgress<DecompilationProgress>? progress, CancellationToken cancellationToken)
    {
        var path = Path.Combine(folder, assembly.PdbFileName);
        try
        {
            GeneratePdb(assembly, typeSystem, options, path, embedSources, progress, cancellationToken);
            return null;
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return new PdbFailure.NotWritten
            {
                Assembly = assembly,
                Error = new ExportError { Message = e.Message, Details = e.ToString() },
            };
        }
    }

    /// <summary>Where the one part of an export of a single assembly starts and ends in it.</summary>
    private static readonly float[] WholeExport = [0f, 1f];

    /// <summary>Where each part of an export of <paramref name="assemblies"/> starts in it, and where
    /// the last ends: each assembly takes the share of its types among all of theirs.</summary>
    internal static float[] PartBounds(IReadOnlyList<LoadedModule> assemblies)
    {
        var bounds = new float[assemblies.Count + 1];
        var total = assemblies.Sum(WeightOf);
        var before = 0;
        for (var i = 0; i < assemblies.Count; i++)
        {
            bounds[i] = (float)before / total;
            before += WeightOf(assemblies[i]);
        }
        bounds[assemblies.Count] = 1f;
        return bounds;
    }

    private static int WeightOf(LoadedModule assembly)
    {
        var types = LoadedAssemblies.GetFile(assembly).Metadata.TypeDefinitions.Count;
        return Math.Max(1, types);
    }

    /// <summary>Tells <paramref name="progress"/> that the part at <paramref name="index"/>,
    /// <paramref name="assembly"/>, starts where <paramref name="bounds"/> say, and gives what reports
    /// the decompiler's progress within it.</summary>
    private static PartProgress? Part(IProgress<ExportProgress>? progress, LoadedModule assembly, int index, float[] bounds)
    {
        if (progress is null)
        {
            return null;
        }

        var part = new PartProgress { Progress = progress, Name = assembly.Name, Part = index + 1, Parts = bounds.Length - 1, From = bounds[index], To = bounds[index + 1] };
        StartPart(part);
        return part;
    }

    /// <summary>The decompiler's progress within one part of an export, which spans
    /// <see cref="From"/> to <see cref="To"/> of the whole, passed on as the export's. The decompiler's
    /// <see cref="IProgress{T}"/> demands <see cref="Report"/>.</summary>
    private sealed class PartProgress : IProgress<DecompilationProgress>
    {
        public required IProgress<ExportProgress> Progress { get; init; }
        public required string Name { get; init; }
        public required int Part { get; init; }
        public required int Parts { get; init; }
        public required float From { get; init; }
        public required float To { get; init; }

        public void Report(DecompilationProgress value)
        {
            ReportPart(this, value);
        }
    }

    private static void StartPart(PartProgress part)
    {
        part.Progress.Report(new ExportProgress
        {
            Name = part.Name,
            Part = part.Part,
            Parts = part.Parts,
            Fraction = part.From,
        });
    }

    /// <summary>The span of <paramref name="part"/> cut in two, for two steps that each take half of it.</summary>
    private static (PartProgress First, PartProgress Second) GetHalves(PartProgress part)
    {
        var middle = (part.From + part.To) / 2;
        var first = new PartProgress { Progress = part.Progress, Name = part.Name, Part = part.Part, Parts = part.Parts, From = part.From, To = middle };
        var second = new PartProgress { Progress = part.Progress, Name = part.Name, Part = part.Part, Parts = part.Parts, From = middle, To = part.To };
        return (first, second);
    }

    private static void ReportPart(PartProgress part, DecompilationProgress value)
    {
        var fraction = GetFractionAt(part, value.UnitsCompleted, value.TotalUnits);
        part.Progress.Report(new ExportProgress
        {
            Name = part.Name,
            Part = part.Part,
            Parts = part.Parts,
            Fraction = fraction,
        });
    }

    private static float GetFractionAt(PartProgress part, int done, int total)
    {
        if (total <= 0)
        {
            return part.From;
        }

        var share = Math.Clamp((float)done / total, 0f, 1f);
        return part.From + (part.To - part.From) * share;
    }
}

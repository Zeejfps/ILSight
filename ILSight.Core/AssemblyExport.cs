using System.Diagnostics.CodeAnalysis;
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
    /// writes what <paramref name="extras"/> asks for besides. A unit reading the assembly: closed, writing nothing,
    /// when it was released (closed) before. What the library throws decompiling or writing it, the disk's failures
    /// among them, is <see cref="Outcome{T}.Failed"/>; the cancellation of <paramref name="cancellationToken"/> is let
    /// through, and so is a bug of the app's code the library calls back (its resolver: <see cref="CallbackFailure"/>).</summary>
    /// <exception cref="ArgumentException"><paramref name="projectPath"/> is no absolute path of a file.</exception>
    public static async Task<Outcome<ModuleRead<ExportedProject>>> ExportProjectAsync(LoadedModule assembly, DecompilerOptions options,
        string projectPath, ProjectExtras? extras = null, IProgress<ExportProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        var path = FullFilePath.Of(projectPath);
        using var reading = LoadedAssemblies.BeginReading();
        if (!LoadedAssemblies.TryRead(reading, assembly))
        {
            return new Outcome<ModuleRead<ExportedProject>>.Ok(new ModuleRead<ExportedProject>.Closed { Module = assembly });
        }

        // What the app's code the library calls back throws (the resolver) marks this: a bug, which goes on to the edge
        // rather than reading as the library's failure on the assembly, which the export says without a record (CF10).
        var callbacks = new CallbackFailure();
        var part = BeginPart(progress, assembly, 0, WholeExport);
        ExportedProject project;
        try
        {
            project = await ExportProjectAsync(reading, assembly, options, path, extras ?? ProjectExtras.None, part, callbacks, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception e) when (!callbacks.Failed)
        {
            return new Outcome<ModuleRead<ExportedProject>>.Failed(e);
        }

        return new Outcome<ModuleRead<ExportedProject>>.Ok(new ModuleRead<ExportedProject>.Read { Value = project });
    }

    private static async Task<ExportedProject> ExportProjectAsync(ModuleReadLease reading, LoadedModule assembly, DecompilerOptions options,
        FullFilePath projectPath, ProjectExtras extras, PartProgress? part, CallbackFailure callbacks, CancellationToken cancellationToken)
    {
        var (projectPart, pdbPart) = SplitForPdb(part, extras.Pdb);
        var project = WriteProject(reading, assembly, options, projectPath, extras.StrongNameKeyFile, projectPart, callbacks, cancellationToken);
        if (!extras.Pdb)
        {
            return project;
        }

        return await WithPdbAsync(reading, project, options, projectPath.Folder, extras.EmbedSourcesInPdb, pdbPart, callbacks, cancellationToken);
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

    private static ExportedProject WriteProject(ModuleReadLease reading, LoadedModule assembly, DecompilerOptions options, FullFilePath projectPath, string? strongNameKeyFile,
        IProgress<DecompilationProgress>? progress, CallbackFailure callbacks, CancellationToken cancellationToken)
    {
        var folder = projectPath.Folder;
        Directory.CreateDirectory(folder);
        var decompiler = CreateProjectDecompiler(reading, assembly, options, callbacks, cancellationToken);
        decompiler.ProgressIndicator = progress;
        decompiler.StrongNameKeyFile = strongNameKeyFile;
        using var projectFile = new StreamWriter(projectPath.Text);
        var id = decompiler.DecompileProject(LoadedAssemblies.GetFile(assembly), folder, projectFile, cancellationToken);
        var errors = decompiler.Errors
            .Select(static e => new ExportError { Error = e })
            .ToList();
        return new ExportedProject { Assembly = assembly, ProjectPath = projectPath.Text, Errors = errors, Id = id };
    }

    private static WholeProjectDecompiler CreateProjectDecompiler(ModuleReadLease reading, LoadedModule assembly, DecompilerOptions options,
        CallbackFailure callbacks, CancellationToken cancellationToken)
    {
        var settings = Settings.Create(options);
        var symbols = NodeDecompiler.DebugSymbolsFor(assembly, settings);
        return new BamlProjectDecompiler(assembly, LoadedAssemblies.CreateResolver(reading, assembly, callbacks), settings, symbols, cancellationToken);
    }

    private static async Task<ExportedProject> WithPdbAsync(ModuleReadLease reading, ExportedProject project, DecompilerOptions options, string folder,
        bool embedSources, IProgress<DecompilationProgress>? progress, CallbackFailure callbacks, CancellationToken cancellationToken)
    {
        var failure = await TryGeneratePdbAsync(reading, project.Assembly, options, folder, embedSources, progress, callbacks, cancellationToken);
        if (failure is not null)
        {
            return project with { Pdb = new ProjectPdb.Failed(failure) };
        }

        var path = Path.Combine(folder, project.Assembly.PdbFileName);
        return project with { Pdb = new ProjectPdb.Written(path) };
    }

    /// <summary>Decompiles each of <paramref name="projects"/> into a project in a folder of its own,
    /// named as the project, beside <paramref name="solutionPath"/>, then writes the solution that
    /// holds them. A unit reading every assembly: closed, writing nothing, with the first of them released (closed)
    /// before. What the library throws decompiling or writing them is <see cref="Outcome{T}.Failed"/>, as
    /// <see cref="ExportProjectAsync(LoadedModule, DecompilerOptions, string, ProjectExtras, IProgress{ExportProgress}, CancellationToken)"/>
    /// says.</summary>
    /// <exception cref="ArgumentException"><paramref name="solutionPath"/> is no absolute path of a file.</exception>
    public static async Task<Outcome<ModuleRead<List<ExportedProject>>>> ExportSolutionAsync(IReadOnlyList<SolutionProject> projects,
        DecompilerOptions options, string solutionPath, ProjectExtras? extras = null, IProgress<ExportProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var folder = FullFilePath.Of(solutionPath).Folder;
        var assemblies = projects.Select(static p => p.Assembly).ToList();
        using var reading = LoadedAssemblies.BeginReading();
        if (!TryReadAll(reading, assemblies, out var closed))
        {
            return new Outcome<ModuleRead<List<ExportedProject>>>.Ok(new ModuleRead<List<ExportedProject>>.Closed { Module = closed });
        }

        // The app's callbacks' failures go on to the edge, as for one project (CF10).
        var callbacks = new CallbackFailure();
        List<ExportedProject> exported;
        try
        {
            exported = await ExportSolutionAsync(reading, projects, assemblies, options, folder, solutionPath, extras ?? ProjectExtras.None, progress,
                callbacks, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception e) when (!callbacks.Failed)
        {
            return new Outcome<ModuleRead<List<ExportedProject>>>.Failed(e);
        }

        return new Outcome<ModuleRead<List<ExportedProject>>>.Ok(new ModuleRead<List<ExportedProject>>.Read { Value = exported });
    }

    private static bool TryReadAll(ModuleReadLease reading, IReadOnlyList<LoadedModule> assemblies, [NotNullWhen(false)] out LoadedModule? closed)
    {
        foreach (var assembly in assemblies)
        {
            if (!LoadedAssemblies.TryRead(reading, assembly))
            {
                closed = assembly;
                return false;
            }
        }

        closed = null;
        return true;
    }

    private static async Task<List<ExportedProject>> ExportSolutionAsync(ModuleReadLease reading, IReadOnlyList<SolutionProject> projects,
        IReadOnlyList<LoadedModule> assemblies, DecompilerOptions options, string folder, string solutionPath, ProjectExtras withExtras,
        IProgress<ExportProgress>? progress, CallbackFailure callbacks, CancellationToken cancellationToken)
    {
        var bounds = PartBounds(assemblies);
        var exported = new List<ExportedProject>();
        for (var i = 0; i < projects.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var project = projects[i];
            var projectPath = FullFilePath.Of(ExportNames.ProjectPathIn(folder, project.Name));
            var part = BeginPart(progress, project.Assembly, i, bounds);
            var exportedProject = await ExportProjectAsync(reading, project.Assembly, options, projectPath, withExtras, part, callbacks, cancellationToken);
            exported.Add(exportedProject);
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
    /// and writes nothing. What the library throws decompiling or writing it is <see cref="Outcome{T}.Failed"/>; the
    /// cancellation of <paramref name="cancellationToken"/> is let through.</remarks>
    public static async Task<Outcome<TypeSystemWork<string>>> GeneratePdbAsync(LoadedModule assembly, DecompilerOptions options, string path,
        IProgress<ExportProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        // The app's callbacks' failures go on to the edge (CF10).
        var callbacks = new CallbackFailure();
        // The type system these settings read is awaited and matched before the read starts (CJ8).
        var settings = Settings.Create(options);
        TypeSystemWork<string> written;
        try
        {
            written = await LoadedAssemblies.WithTypeSystemAsync(assembly, settings, (_, typeSystem) =>
            {
                var partProgress = BeginPart(progress, assembly, 0, WholeExport);
                return GeneratePdb(assembly, typeSystem, settings, path, embedSources: true, partProgress, cancellationToken);
            }, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception e) when (!callbacks.Failed)
        {
            return new Outcome<TypeSystemWork<string>>.Failed(e);
        }

        return new Outcome<TypeSystemWork<string>>.Ok(written);
    }

    // chosenTypeSystem is the type system settings read, which the unit awaited before its read began.
    private static string GeneratePdb(LoadedModule assembly, DecompilerTypeSystem chosenTypeSystem, DecompilerSettings settings, string path,
        bool embedSources, IProgress<DecompilationProgress>? progress, CancellationToken cancellationToken)
    {
        var decompiler = NodeDecompiler.CreateCSharpDecompiler(assembly, chosenTypeSystem, settings, cancellationToken);
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
    /// <paramref name="folder"/>, named after the assembly, and lists the ones it could not. A unit reading every
    /// assembly: closed, writing nothing, with the first of them released (closed) before.</summary>
    public static async Task<ModuleRead<List<PdbFailure>>> GeneratePdbsAsync(IReadOnlyList<LoadedModule> assemblies, DecompilerOptions options,
        string folder, IProgress<ExportProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        using var reading = LoadedAssemblies.BeginReading();
        if (!TryReadAll(reading, assemblies, out var closed))
        {
            return new ModuleRead<List<PdbFailure>>.Closed { Module = closed };
        }

        // The app's callbacks' failures go on to the edge rather than reading as an assembly's PDB not written (CF10).
        var failures = await GeneratePdbsAsync(reading, assemblies, options, folder, progress, new CallbackFailure(), cancellationToken);
        return new ModuleRead<List<PdbFailure>>.Read { Value = failures };
    }

    private static async Task<List<PdbFailure>> GeneratePdbsAsync(ModuleReadLease reading, IReadOnlyList<LoadedModule> assemblies, DecompilerOptions options,
        string folder, IProgress<ExportProgress>? progress, CallbackFailure callbacks, CancellationToken cancellationToken)
    {
        var bounds = PartBounds(assemblies);
        var failures = new List<PdbFailure>();
        for (var i = 0; i < assemblies.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var assembly = assemblies[i];
            var part = BeginPart(progress, assembly, i, bounds);
            var failure = await TryGeneratePdbAsync(reading, assembly, options, folder, embedSources: true, part, callbacks, cancellationToken);
            if (failure is not null)
            {
                failures.Add(failure);
            }
        }
        return failures;
    }

    private static async Task<PdbFailure?> TryGeneratePdbAsync(ModuleReadLease reading, LoadedModule assembly, DecompilerOptions options, string folder,
        bool embedSources, IProgress<DecompilationProgress>? progress, CallbackFailure callbacks, CancellationToken cancellationToken)
    {
        if (!assembly.CanGeneratePdb)
        {
            return new PdbFailure.NoDebugEntry { Assembly = assembly };
        }

        // The type system these settings read, awaited and matched before the read starts (CJ8).
        var settings = Settings.Create(options);
        var read = await LoadedAssemblies.ReadChosenTypeSystemAsync(reading, assembly, settings, cancellationToken);
        return read.Match(
            r => TryGeneratePdb(assembly, r.TypeSystem, settings, folder, embedSources, progress, callbacks, cancellationToken),
            u => new PdbFailure.NotWritten
            {
                Assembly = assembly,
                Error = new ExportError { Error = u.Thrown.SourceException },
            },
            static c => throw new InvalidOperationException($"'{c.Module.FilePath}' was exported after its release: the export holds a reading of each assembly it exports."));
    }

    private static PdbFailure? TryGeneratePdb(LoadedModule assembly, DecompilerTypeSystem chosenTypeSystem, DecompilerSettings settings, string folder,
        bool embedSources, IProgress<DecompilationProgress>? progress, CallbackFailure callbacks, CancellationToken cancellationToken)
    {
        var path = Path.Combine(folder, assembly.PdbFileName);
        try
        {
            GeneratePdb(assembly, chosenTypeSystem, settings, path, embedSources, progress, cancellationToken);
            return null;
        }
        catch (Exception e) when (e is not OperationCanceledException && !callbacks.Failed)
        {
            return new PdbFailure.NotWritten
            {
                Assembly = assembly,
                Error = new ExportError { Error = e },
            };
        }
    }

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

    private static PartProgress? BeginPart(IProgress<ExportProgress>? progress, LoadedModule assembly, int index, float[] bounds)
    {
        if (progress is null)
        {
            return null;
        }

        var part = new PartProgress { Progress = progress, Name = assembly.Name, Part = index + 1, Parts = bounds.Length - 1, From = bounds[index], To = bounds[index + 1] };
        StartPart(part);
        return part;
    }

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

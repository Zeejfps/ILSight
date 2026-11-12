using System.Diagnostics;
using ILSight.Core;

namespace ILSight.ExportArea;

/// <summary>What one export the exports panel queues writes, one case per kind, with what it is written from: the
/// export as the panel shows it (<see cref="Job"/>) and what a failure says it was exporting (<see cref="Subject"/>)
/// follow from it.</summary>
public abstract record ExportWork
{
    private ExportWork()
    {
    }

    /// <summary>The export as the user asked for it: what it is named after, what it writes, and its folder.</summary>
    public abstract ExportJob Job { get; }

    /// <summary>What a failure says the export was exporting; empty when its kind says it.</summary>
    public abstract string Subject { get; }

    /// <summary>The code of <see cref="Node"/> in <see cref="Language"/>, as one file at <see cref="File"/>, which a
    /// save dialog gave.</summary>
    public sealed record CodeFile : ExportWork
    {
        public required BrowseNode Node { get; init; }

        public required DecompilationLanguage Language { get; init; }

        public required DecompilerOptions Options { get; init; }

        public required string File { get; init; }

        public override ExportJob Job => new ExportJob(Node.Text, CodeKindOf(Language), FolderOf(File));

        public override string Subject => Node.Text;
    }

    /// <summary><see cref="Assembly"/> decompiled into a C# project whose file is <see cref="ProjectFile"/>, with what
    /// <see cref="Extras"/> asks for besides.</summary>
    public sealed record Project : ExportWork
    {
        public required LoadedModule Assembly { get; init; }

        public required DecompilerOptions Options { get; init; }

        public required string ProjectFile { get; init; }

        public required ProjectExtras Extras { get; init; }

        public override ExportJob Job => new ExportJob(Path.GetFileNameWithoutExtension(ProjectFile), ExportKind.Project, FolderOf(ProjectFile));

        public override string Subject => Assembly.Name;
    }

    /// <summary>Each of <see cref="Projects"/> decompiled into a C# project in a folder of its own beside the solution
    /// at <see cref="SolutionFile"/>.</summary>
    public sealed record Solution : ExportWork
    {
        public required IReadOnlyList<SolutionProject> Projects { get; init; }

        public required DecompilerOptions Options { get; init; }

        public required string SolutionFile { get; init; }

        public required ProjectExtras Extras { get; init; }

        public override ExportJob Job => new ExportJob(Path.GetFileNameWithoutExtension(SolutionFile), ExportKind.Solution, FolderOf(SolutionFile));

        public override string Subject => "";
    }

    /// <summary>A portable PDB of <see cref="Assembly"/>'s decompiled C#, written to <see cref="File"/>.</summary>
    public sealed record Pdb : ExportWork
    {
        public required LoadedModule Assembly { get; init; }

        public required DecompilerOptions Options { get; init; }

        public required string File { get; init; }

        public override ExportJob Job => new ExportJob(Assembly.Name, ExportKind.Pdb, FolderOf(File));

        public override string Subject => Assembly.Name;
    }

    /// <summary>A portable PDB of each of <see cref="Assemblies"/>, written into <see cref="Folder"/> named after the
    /// assembly.</summary>
    public sealed record Pdbs : ExportWork
    {
        public required IReadOnlyList<LoadedModule> Assemblies { get; init; }

        public required DecompilerOptions Options { get; init; }

        public required string Folder { get; init; }

        public override ExportJob Job => new ExportJob(Path.GetFileName(Folder), ExportKind.Pdbs, Folder);

        public override string Subject => "";
    }

    /// <summary>The HTML class diagrammer of <see cref="Assembly"/>, written into <see cref="Folder"/>, a folder of its
    /// own named after it, and opened once written.</summary>
    public sealed record Diagram : ExportWork
    {
        public required LoadedModule Assembly { get; init; }

        public required string Folder { get; init; }

        public override ExportJob Job => new ExportJob(Assembly.Name, ExportKind.Diagram, Folder);

        public override string Subject => Assembly.Name;
    }

    /// <summary>Every file in the folder of a package <see cref="Place"/> stands for, shown as <see cref="Name"/>,
    /// written into <see cref="Folder"/> at its path from that folder.</summary>
    public sealed record PackageFolder : ExportWork
    {
        public required string Name { get; init; }

        public required PackageFolderPlace Place { get; init; }

        public required string Folder { get; init; }

        public override ExportJob Job => new ExportJob(Name, ExportKind.Files, Folder);

        public override string Subject => Name;
    }

    public TResult Match<TResult>(Func<CodeFile, TResult> codeFile, Func<Project, TResult> project, Func<Solution, TResult> solution,
        Func<Pdb, TResult> pdb, Func<Pdbs, TResult> pdbs, Func<Diagram, TResult> diagram, Func<PackageFolder, TResult> packageFolder)
    {
        return this switch
        {
            CodeFile c => codeFile(c),
            Project p => project(p),
            Solution s => solution(s),
            Pdb p => pdb(p),
            Pdbs p => pdbs(p),
            Diagram d => diagram(d),
            PackageFolder f => packageFolder(f),
            _ => throw new UnreachableException($"ExportWork.Match does not handle {GetType().Name}."),
        };
    }

    private static ExportKind CodeKindOf(DecompilationLanguage language)
    {
        if (language == DecompilationLanguage.CSharp)
        {
            return ExportKind.CSharpFile;
        }

        return ExportKind.ILFile;
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
}

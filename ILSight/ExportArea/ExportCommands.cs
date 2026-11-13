using ILSight.Core;

namespace ILSight.ExportArea;

/// <summary>The edge where the exports the screen asks for start, once the frame is drawn: each is queued and its
/// writing started as a workflow (<see cref="ExportWriting"/>), which waits for its turn; a resource or a single file of
/// a package starts writing at once, as a workflow of its own, counted in the app's background work until it
/// ends.</summary>
[Edge]
public static class ExportCommands
{
    /// <summary>Saves <paramref name="node"/> to the file at <paramref name="path"/>: a resource's bytes as they are,
    /// else its code in the current language, as one file (<see cref="Exports.ToCodeSave"/>). Projects come from the
    /// export dialog.</summary>
    public static void SaveCode(AppState appState, BrowseNode node, string path)
    {
        var save = Exports.ToCodeSave(node, appState.Decompiler.Setup, path);
        save.Switch(appState,
            static (a, resource) => SaveResource(a, resource.Node, resource.File),
            static (a, code) => StartWriting(a, code.Work));
    }

    /// <summary>Starts writing the bytes of the resource <paramref name="node"/> stands for to the file at
    /// <paramref name="path"/> (<see cref="SaveResourceHandler"/>), off the frame thread, on a token the app's shutdown
    /// cancels.</summary>
    public static void SaveResource(AppState appState, BrowseNode node, string path)
    {
        appState.Workflows.Start(nameof(SaveResourceHandler), ct => appState.SaveResourceHandler.HandleAsync(node, path, ct));
    }

    /// <summary>Starts writing the file of a package <paramref name="place"/> stands for, as the package holds it, to
    /// the file at <paramref name="path"/> (<see cref="ExtractPackageFileHandler"/>), off the frame thread, on a token
    /// the app's shutdown cancels.</summary>
    public static void ExtractPackageFile(AppState appState, PackagePlace place, string path)
    {
        appState.Workflows.Start(nameof(ExtractPackageFileHandler), ct => appState.ExtractPackageFileHandler.HandleAsync(place, path, ct));
    }

    /// <summary>Exports what <paramref name="request"/> asks for: one assembly as a project straight into the folder,
    /// several as a solution with a folder per project.</summary>
    public static void StartExport(AppState appState, ExportRequest request)
    {
        StartWriting(appState, Exports.ToWork(request, appState.Decompiler.Setup.Options));
    }

    /// <summary>Writes a portable PDB of <paramref name="assembly"/>'s decompiled C# to <paramref name="path"/>.</summary>
    public static void GeneratePdb(AppState appState, LoadedModule assembly, string path)
    {
        StartWriting(appState, new ExportWork.Pdb { Assembly = assembly, Options = appState.Decompiler.Setup.Options, File = path });
    }

    /// <summary>Writes the HTML class diagrammer of <paramref name="assembly"/> into a folder of its own in
    /// <paramref name="folder"/>, named after it, and opens it once written.</summary>
    public static void CreateDiagram(AppState appState, LoadedModule assembly, string folder)
    {
        StartWriting(appState, new ExportWork.Diagram { Assembly = assembly, Folder = Exports.GetDiagramFolder(assembly, folder) });
    }

    /// <summary>Writes a portable PDB of each open assembly into <paramref name="folder"/>, named after the assembly.
    /// Assemblies that cannot have one, or fail, are the report's problems.</summary>
    public static void GeneratePdbs(AppState appState, string folder)
    {
        var work = new ExportWork.Pdbs { Assemblies = appState.Assemblies.Assemblies, Options = appState.Decompiler.Setup.Options, Folder = folder };
        StartWriting(appState, work);
    }

    /// <summary>Writes every file in the folder of a package <paramref name="place"/> stands for, shown as
    /// <paramref name="name"/>, into <paramref name="folder"/>, at its path from that folder. Files that cannot be
    /// written there are the report's problems.</summary>
    public static void ExtractPackageFolder(AppState appState, string name, PackageFolderPlace place, string folder)
    {
        StartWriting(appState, new ExportWork.PackageFolder { Name = name, Place = place, Folder = folder });
    }

    /// <summary>Queues <paramref name="work"/> and brings the exports panel to the front (<see cref="Exports.Enter"/>),
    /// then starts writing it once its turn comes (<see cref="ExportWriting"/>), counted in the app's background work
    /// until it ends.</summary>
    private static void StartWriting(AppState appState, ExportWork work)
    {
        var ticket = Exports.Enter(appState.Export, appState.ViewCommands, work.Job);
        appState.Workflows.StartSettling(nameof(ExportWriting),
            ct => appState.ExportWriting.HandleAsync(appState.FinishedExports, appState.ViewCommands, work, ticket, ct),
            _ => appState.Export.Drop(ticket.Run));
    }
}

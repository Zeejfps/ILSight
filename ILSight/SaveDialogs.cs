using ZGF.Desktop;
using ZGF.Gui.Immediate;
using ZGF.Gui.Immediate.Widgets;
using ILSight.Core;
using ILSight.CommandArea;
using ILSight.ExportArea;

namespace ILSight;

/// <summary>The dialogs that ask where code, resources, PDBs, class diagrams and the files of packages are written.</summary>
internal static class SaveDialogs
{
    /// <summary>Asks where to save the active tab's node, when it can be saved: a resource's bytes
    /// under its own name, else its code as one file, in <paramref name="setup"/>'s language and with its options, the
    /// setup as the user asks (<see cref="PickCodeFile"/>).</summary>
    public static void PickActiveCodeFile(Ui ui, Appearance appearance, IAppCommandWriter commands, Navigation navigation, DecompilerSetup setup)
    {
        var node = navigation.ActiveTab?.Node;
        if (!Exports.CanSaveCode(node))
        {
            return;
        }

        if (node.HasResource)
        {
            ResourceImageView.PickExport(ui, appearance, commands, node);
            return;
        }
        PickCodeFile(ui, appearance, commands, setup, node);
    }

    /// <summary>Asks where to save the code of <paramref name="node"/> as one file, in <paramref name="setup"/>'s
    /// language and with its options: the file is named, and its filters chosen, for that language, and the save the
    /// answer queues carries the same setup, so a language or option changed while the dialog is open (it doesn't
    /// block) reaches neither the name nor the code. The dialog's title says so to the user: it saves the code in that
    /// language as shown ("Save C# code as shown").</summary>
    public static void PickCodeFile(Ui ui, Appearance appearance, IAppCommandWriter commands, DecompilerSetup setup, BrowseNode node)
    {
        var t = appearance.Strings;
        var fileName = node.SuggestCodeFileName(setup.Language);
        var title = t.ExportSaveCodeAsShownTitle(language: DecompilationLanguages.Label(setup.Language));
        ui.PickSaveFile(title, null, fileName, CodeFilters(t, setup.Language),
            path => Commands.Later(commands, (node, setup, path), static (a, s) => ExportCommands.SaveCode(a, s.node, s.setup, s.path)));
    }

    private static FileFilter[] CodeFilters(Strings t, DecompilationLanguage language)
    {
        if (language == DecompilationLanguage.CSharp)
        {
            return [new FileFilter(t.FileFilterCsharp, ["*.cs"]), new FileFilter(t.FileFilterAll, ["*.*"])];
        }

        return [new FileFilter(t.FileFilterIl, ["*.il"]), new FileFilter(t.FileFilterAll, ["*.*"])];
    }

    /// <summary>Asks where to write the portable PDB of <paramref name="assembly"/>; nothing is chosen here from the
    /// decompiler's options, which the PDB takes as they stand once the file is picked
    /// (<see cref="ExportCommands.GeneratePdb"/>).</summary>
    public static void PickPdbFile(Ui ui, Appearance appearance, IAppCommandWriter commands, LoadedModule assembly)
    {
        var t = appearance.Strings;
        FileFilter[] filters = [new FileFilter(t.FileFilterPdb, ["*.pdb"])];
        ui.PickSaveFile(t.ExportPdbTitle, null, assembly.PdbFileName, filters,
            path => Commands.Later(commands, (assembly, path), static (a, s) => ExportCommands.GeneratePdb(a, s.assembly, s.path)));
    }

    /// <summary>Asks for the folder the class diagram of <paramref name="assembly"/> is written into.</summary>
    public static void PickDiagramFolder(Ui ui, Appearance appearance, IAppCommandWriter commands, LoadedModule assembly)
    {
        var t = appearance.Strings;
        ui.PickFolder(t.ExportDiagramTitle, null, folder => Commands.Later(commands, (assembly, folder), static (a, s) => ExportCommands.CreateDiagram(a, s.assembly, s.folder)));
    }

    /// <summary>Asks for the folder the portable PDBs of every open assembly are written into; nothing is chosen here
    /// from the decompiler's options, which the PDBs take as they stand once the folder is picked
    /// (<see cref="ExportCommands.GeneratePdbs"/>).</summary>
    public static void PickPdbFolder(Ui ui, Appearance appearance, IAppCommandWriter commands)
    {
        var t = appearance.Strings;
        ui.PickFolder(t.ExportPdbsTitle, null, folder => Commands.Later(commands, folder, static (a, f) => ExportCommands.GeneratePdbs(a, f)));
    }

    /// <summary>Asks where to write the file of a package <paramref name="place"/> stands for.</summary>
    public static void PickPackageFile(Ui ui, Appearance appearance, IAppCommandWriter commands, PackagePlace place)
    {
        var t = appearance.Strings;
        FileFilter[] filters = [new FileFilter(t.FileFilterAll, ["*.*"])];
        ui.PickSaveFile(t.ExportExtractFileTitle, null, place.File.Name, filters,
            path => Commands.Later(commands, (place, path), static (a, s) => ExportCommands.ExtractPackageFile(a, s.place, s.path)));
    }

    /// <summary>Asks for the folder the files of the package folder <paramref name="place"/> stands for,
    /// shown as <paramref name="name"/>, are extracted into.</summary>
    public static void PickExtractFolder(Ui ui, Appearance appearance, IAppCommandWriter commands, string name, PackageFolderPlace place)
    {
        var t = appearance.Strings;
        ui.PickFolder(t.ExportExtractFolderTitle, null,
            folder => Commands.Later(commands, (name, place, folder), static (a, s) => ExportCommands.ExtractPackageFolder(a, s.name, s.place, s.folder)));
    }
}

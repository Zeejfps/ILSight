using ICSharpCode.Decompiler.TypeSystem;

namespace ILSight.Core;

/// <summary>Saving a node's code to a file: I/O, which only the app's adapters call (listed in
/// <c>rulelab.ambient_types</c>).</summary>
public static class CodeFiles
{
    /// <summary>Writes <paramref name="node"/>'s code in <paramref name="language"/> to the file at
    /// <paramref name="path"/>, as <see cref="NodeContentLoader.ToCodeExportAsync"/> says what it is, once the read of
    /// its module's type system ended: the path written, or, for code that reads it, the module the library could not
    /// read, with nothing written. Throws when decompiling or writing fails. The file is replaced only once the code is
    /// whole. A unit reading the node's module: closed, writing nothing, when it was released (closed) before.</summary>
    public static async Task<TypeSystemWork<string>> SaveAsync(BrowseSession session, BrowseNode node, DecompilationLanguage language,
        DecompilerOptions options, string path, CancellationToken cancellationToken = default)
    {
        using var reading = LoadedAssemblies.BeginReading();
        if (node.Assembly is LoadedModule own && !LoadedAssemblies.TryRead(reading, own))
        {
            return new TypeSystemWork<string>.Closed { Module = own };
        }

        var export = await NodeContentLoader.ToCodeExportAsync(session, node, cancellationToken);
        var module = ModuleOf(export);
        if (module is null)
        {
            return Save(export, null, language, options, path, cancellationToken);
        }

        var read = await LoadedAssemblies.ReadTypeSystemAsync(reading, module, cancellationToken);
        return read.Match(
            r => Save(export, r.TypeSystem, language, options, path, cancellationToken),
            u => SaveUnreadable(export, module, u, language, options, path, cancellationToken),
            static c => new TypeSystemWork<string>.Closed { Module = c.Module });
    }

    /// <summary>The code of <paramref name="export"/>, whose module the library could not read: nothing, for code that
    /// reads it; else what it writes without the module's type system.</summary>
    private static TypeSystemWork<string> SaveUnreadable(CodeExport export, LoadedModule module, TypeSystemRead.Unreadable unreadable,
        DecompilationLanguage language, DecompilerOptions options, string path, CancellationToken cancellationToken)
    {
        if (export is CodeExport.Document document && !NodeDecompiler.IsReadThroughTypeSystem(document.Request))
        {
            return Save(export, null, language, options, path, cancellationToken);
        }

        return new TypeSystemWork<string>.Unreadable { Module = new UnreadableModule { Module = module, Why = unreadable.Thrown.SourceException } };
    }

    private static TypeSystemWork<string> Save(CodeExport export, DecompilerTypeSystem? typeSystem, DecompilationLanguage language, DecompilerOptions options,
        string path, CancellationToken cancellationToken)
    {
        AtomicFile.Write(path, stream => NodeDecompiler.WriteCode(export, typeSystem, language, options, stream, cancellationToken));
        return new TypeSystemWork<string>.Done { Value = path };
    }

    /// <summary>The module <paramref name="export"/>'s code is read from; null for a package's document.</summary>
    private static LoadedModule? ModuleOf(CodeExport export)
    {
        if (export is CodeExport.WholeModule whole)
        {
            return whole.Module;
        }

        if (export is CodeExport.Namespace ns)
        {
            return ns.Module;
        }

        return NodeDecompiler.ModuleOf(((CodeExport.Document)export).Request);
    }
}

using ICSharpCode.Decompiler.TypeSystem;

namespace ILSight.Core;

/// <summary>Saving a node's code to a file: I/O, which only the app's adapters call (listed in
/// <c>rulelab.ambient_types</c>).</summary>
public static class CodeFiles
{
    /// <summary>Writes <paramref name="node"/>'s code in <paramref name="language"/> to the file at
    /// <paramref name="path"/>, as <see cref="NodeContentLoader.ToCodeExportAsync"/> says what it is, once the read of
    /// its module's type system ended: the path written, or, for code that reads it, the module the library could not
    /// read, with nothing written. What the library throws decompiling, or the disk writing, is
    /// <see cref="Outcome{T}.Failed"/>; the cancellation of <paramref name="cancellationToken"/> is let through. A bug of the
    /// app's code the library calls back (a builder, a method's C#, the resolver: <see cref="CallbackFailure"/>) is let
    /// through too. The file
    /// is replaced only once the code is whole. A unit reading the node's module: closed, writing nothing, when it was
    /// released (closed) before. The notes the app writes beside the code are in <paramref name="notes"/>' words.</summary>
    public static async Task<Outcome<TypeSystemWork<string>>> SaveAsync(BrowseSession session, BrowseNode node, DecompilationLanguage language,
        DecompilerOptions options, IDocumentNotes notes, string path, CancellationToken cancellationToken = default)
    {
        using var reading = LoadedAssemblies.BeginReading();
        if (node.Assembly is LoadedModule own && !LoadedAssemblies.TryRead(reading, own))
        {
            return new Outcome<TypeSystemWork<string>>.Ok(new TypeSystemWork<string>.Closed { Module = own });
        }

        // What the app's code the decompiler calls back throws (a builder, a method's C#, the resolver) marks this: a bug,
        // which goes on to the edge rather than reading as the library's failure on the code saved (CF10).
        var callbacks = new CallbackFailure();
        TypeSystemWork<string> written;
        try
        {
            written = await SaveUnderReadingAsync(session, reading, node, language, options, notes, path, callbacks, cancellationToken);
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

    private static async Task<TypeSystemWork<string>> SaveUnderReadingAsync(BrowseSession session, ModuleReadLease reading, BrowseNode node,
        DecompilationLanguage language, DecompilerOptions options, IDocumentNotes notes, string path, CallbackFailure callbacks, CancellationToken cancellationToken)
    {
        var export = await NodeContentLoader.ToCodeExportAsync(session, node, cancellationToken);
        var module = FindModule(export);
        if (module is null)
        {
            return Save(reading, export, null, language, options, notes, path, callbacks, cancellationToken);
        }

        var read = await LoadedAssemblies.ReadTypeSystemAsync(reading, module, cancellationToken);
        return read.Match(
            r => Save(reading, export, r.TypeSystem, language, options, notes, path, callbacks, cancellationToken),
            u => SaveUnreadable(reading, export, module, u, language, options, notes, path, callbacks, cancellationToken),
            static c => new TypeSystemWork<string>.Closed { Module = c.Module });
    }

    private static TypeSystemWork<string> SaveUnreadable(ModuleReadLease reading, CodeExport export, LoadedModule module, TypeSystemRead.Unreadable unreadable,
        DecompilationLanguage language, DecompilerOptions options, IDocumentNotes notes, string path, CallbackFailure callbacks, CancellationToken cancellationToken)
    {
        if (export is CodeExport.Document document && !NodeDecompiler.IsReadThroughTypeSystem(document.Request))
        {
            return Save(reading, export, null, language, options, notes, path, callbacks, cancellationToken);
        }

        return new TypeSystemWork<string>.Unreadable { Module = new UnreadableModule { Module = module, Why = unreadable.Thrown.SourceException } };
    }

    private static TypeSystemWork<string> Save(ModuleReadLease reading, CodeExport export, DecompilerTypeSystem? typeSystem, DecompilationLanguage language, DecompilerOptions options,
        IDocumentNotes notes, string path, CallbackFailure callbacks, CancellationToken cancellationToken)
    {
        AtomicFile.Write(path, stream => NodeDecompiler.WriteCode(reading, export, typeSystem, language, options, notes, callbacks, stream, cancellationToken));
        return new TypeSystemWork<string>.Done { Value = path };
    }

    // A package's document is read from no module.
    private static LoadedModule? FindModule(CodeExport export)
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

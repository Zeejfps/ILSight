namespace ILSight.Core;

/// <summary>Saving a node's code to a file: I/O, which only the app's adapters call (listed in
/// <c>rulelab.ambient_types</c>).</summary>
public static class CodeFiles
{
    /// <summary>Writes <paramref name="node"/>'s code in <paramref name="language"/> to the file at
    /// <paramref name="path"/>, as <see cref="NodeContentLoader.ToCodeExport"/> says what it is. Throws when decompiling
    /// or writing fails. The file is replaced only once the code is whole.</summary>
    public static void Save(BrowseSession session, BrowseNode node, DecompilationLanguage language, DecompilerOptions options, string path,
        CancellationToken cancellationToken = default)
    {
        var export = NodeContentLoader.ToCodeExport(session, node);
        AtomicFile.Write(path, stream => NodeDecompiler.WriteCode(export, language, options, stream, cancellationToken));
    }
}

using ILSight.Core.ResourceArea;

namespace ILSight.Core;

/// <summary>The app's words for the notes it writes into a document beside what the document shows: each a message in
/// the app's language, filled with the values the core gives as they are, without the comment marker the core writes
/// it under. The decompiled code, and what the decompiler library writes into it, are content and never go through
/// these.</summary>
public interface IDocumentNotes
{
    /// <summary>That the code was read with <paramref name="reference"/> by name only, as its file
    /// <paramref name="file"/> could not be read, <paramref name="why"/>, and that reloading <paramref name="module"/>
    /// reads it again: the module-unreadable cause's note.</summary>
    string ReferenceUnreadable(string reference, string file, string why, string module);

    /// <summary>That the code was read with <paramref name="reference"/> by name only, as it was not found: the
    /// not-found reference's note.</summary>
    string ReferenceNotFound(string reference);

    /// <summary>The heading of a document saying why <paramref name="name"/> could not be decompiled.</summary>
    string DecompileFailed(string name);

    /// <summary>What made a decompilation fail, by <paramref name="message"/>, the exception's message as written.</summary>
    string FailureCause(string message);

    /// <summary>That <paramref name="file"/> could not be read as an assembly, by <paramref name="why"/>
    /// (<see cref="DescribeUnopened"/>); or, when null, as it is no assembly or package that can be read.</summary>
    string AssemblyUnreadable(string file, string? why);

    /// <summary>Why the file of <paramref name="error"/> couldn't be opened, as a sentence the notes above say as their
    /// why: what the system or the library said, a cause of ILSight's own in the app's words, else by its message as
    /// written; where the loader found the cause itself, that cause in the app's words.</summary>
    string DescribeUnopened(AssemblyLoadException error);

    /// <summary>That the assembly is a reference assembly, holding metadata and no code.</summary>
    string ReferenceAssembly();

    /// <summary>What a module of the assembly holding metadata is.</summary>
    string ModuleWithMetadata();

    /// <summary>What a module of the assembly holding no metadata (a native library) is.</summary>
    string ModuleWithoutMetadata();

    /// <summary>That an imported method calls <paramref name="entryPoint"/> in <paramref name="module"/>.</summary>
    string Calls(string entryPoint, string module);

    /// <summary>That a reference's file was not found.</summary>
    string ReferenceNotResolved();

    /// <summary>The file at <paramref name="path"/> a reference resolves to.</summary>
    string ResolvesTo(string path);

    /// <summary>Why the resource's picture can't be shown, <paramref name="fault"/>: a cause of ILSight's own decoders,
    /// or a listed library's failure, its message as written.</summary>
    string PictureUnreadable(PictureFault fault);

    /// <summary>That a resource's bytes are shown up to <paramref name="bytes"/>.</summary>
    string Truncated(int bytes);

    /// <summary>The assembly's architecture: <paramref name="architecture"/>'s platform as it is named, and the
    /// bitness it prefers in the app's words.</summary>
    string Architecture(ModuleArchitecture architecture);

    /// <summary>The framework the assembly was built for, <paramref name="framework"/>.</summary>
    string TargetFramework(string framework);

    /// <summary>The assembly's entry point, <paramref name="method"/>, which the document links where it stands in the
    /// text.</summary>
    string EntryPoint(string method);

    /// <summary>That a type is forwarded to <paramref name="destination"/>.</summary>
    string ForwardedTo(string destination);
}

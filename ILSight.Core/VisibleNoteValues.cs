using ILSight.Core.ResourceArea;

namespace ILSight.Core;

/// <summary>The app's words for its notes (<see cref="IDocumentNotes"/>), every value said in them first made to stay on
/// one line (<see cref="OneLineText"/>): a note runs from its comment marker to its line's end and keeps its line count
/// in every language, so a name or path read from metadata or the file system (which an obfuscated assembly can fill
/// with line breaks) is said as <see cref="OneLineText.Name"/> writes it, and an exception's message as
/// <see cref="OneLineText.Message"/> does. The one place every note's values pass through
/// (<see cref="DocumentNote.Words"/>).</summary>
internal sealed class VisibleNoteValues(IDocumentNotes words) : IDocumentNotes
{
    public string ReferenceUnreadable(string reference, string file, string why, string module)
    {
        return words.ReferenceUnreadable(OneLineText.Name(reference), OneLineText.Name(file), OneLineText.Message(why), OneLineText.Name(module));
    }

    public string ReferenceNotFound(string reference)
    {
        return words.ReferenceNotFound(OneLineText.Name(reference));
    }

    public string DecompileFailed(string name)
    {
        return words.DecompileFailed(OneLineText.Name(name));
    }

    public string FailureCause(string message)
    {
        return words.FailureCause(OneLineText.Message(message));
    }

    public string AssemblyUnreadable(string file, string? why)
    {
        return words.AssemblyUnreadable(OneLineText.Name(file), why is null ? null : OneLineText.Message(why));
    }

    // A why the notes above are given, and make one line there.
    public string DescribeUnopened(AssemblyLoadException error)
    {
        return words.DescribeUnopened(error);
    }

    public string ReferenceAssembly()
    {
        return words.ReferenceAssembly();
    }

    public string ModuleWithMetadata()
    {
        return words.ModuleWithMetadata();
    }

    public string ModuleWithoutMetadata()
    {
        return words.ModuleWithoutMetadata();
    }

    public string Calls(string entryPoint, string module)
    {
        return words.Calls(OneLineText.Name(entryPoint), OneLineText.Name(module));
    }

    public string ReferenceNotResolved()
    {
        return words.ReferenceNotResolved();
    }

    public string ResolvesTo(string path)
    {
        return words.ResolvesTo(OneLineText.Name(path));
    }

    public string PictureUnreadable(PictureFault fault)
    {
        var shown = fault is PictureFault.LibraryFailure failure ? failure with { Message = OneLineText.Message(failure.Message) } : fault;
        return words.PictureUnreadable(shown);
    }

    public string Truncated(int bytes)
    {
        return words.Truncated(bytes);
    }

    public string Architecture(ModuleArchitecture architecture)
    {
        return words.Architecture(architecture);
    }

    public string TargetFramework(string framework)
    {
        return words.TargetFramework(OneLineText.Name(framework));
    }

    public string EntryPoint(string method)
    {
        return words.EntryPoint(OneLineText.Name(method));
    }

    public string ForwardedTo(string destination)
    {
        return words.ForwardedTo(OneLineText.Name(destination));
    }
}

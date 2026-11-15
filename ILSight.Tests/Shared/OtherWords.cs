using ILSight.Core;

namespace ILSight.Tests;

/// <summary>Each note worded otherwise than in English (<see cref="EnglishNotes"/>), the entry point's link at its
/// start: a second language for the tests of a document's notes said again.</summary>
internal sealed class OtherWords : IDocumentNotes
{
    private static readonly IDocumentNotes English = EnglishNotes.Instance;

    public string ReferenceUnreadable(string reference, string file, string why, string module)
    {
        return "[x] " + English.ReferenceUnreadable(reference, file, why, module);
    }

    public string ReferenceNotFound(string reference)
    {
        return "[x] " + English.ReferenceNotFound(reference);
    }

    public string DecompileFailed(string name)
    {
        return "[x] " + English.DecompileFailed(name);
    }

    public string FailureCause(string message)
    {
        return "[x] " + English.FailureCause(message);
    }

    public string AssemblyUnreadable(string file, string? why)
    {
        return "[x] " + English.AssemblyUnreadable(file, why);
    }

    public string ReferenceAssembly()
    {
        return "[x] " + English.ReferenceAssembly();
    }

    public string ModuleWithMetadata()
    {
        return "[x] " + English.ModuleWithMetadata();
    }

    public string ModuleWithoutMetadata()
    {
        return "[x] " + English.ModuleWithoutMetadata();
    }

    public string Calls(string entryPoint, string module)
    {
        return "[x] " + English.Calls(entryPoint, module);
    }

    public string ReferenceNotResolved()
    {
        return "[x] " + English.ReferenceNotResolved();
    }

    public string ResolvesTo(string path)
    {
        return "[x] " + English.ResolvesTo(path);
    }

    public string Truncated(int bytes)
    {
        return "[x] " + English.Truncated(bytes);
    }

    public string Architecture(ModuleArchitecture architecture)
    {
        return "[x] " + English.Architecture(architecture);
    }

    public string TargetFramework(string framework)
    {
        return "[x] " + English.TargetFramework(framework);
    }

    public string EntryPoint(string method)
    {
        return $"{method} is where it starts";
    }

    public string ForwardedTo(string destination)
    {
        return "[x] " + English.ForwardedTo(destination);
    }
}

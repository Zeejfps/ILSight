using ILSight.Core;
using ILSight.Core.ResourceArea;

namespace ILSight.Tests;

/// <summary>The app's words, but for an assembly's architecture, which throws: a bug of the app's in what the builder
/// writes for the decompiler's module preview, inside the read's catch.</summary>
internal sealed class BrokenArchitectureNotes : IDocumentNotes
{
    public const string Message = "The words for an architecture are broken.";
    private static readonly IDocumentNotes English = EnglishNotes.Instance;

    public string Architecture(ModuleArchitecture architecture)
    {
        throw new InvalidOperationException(Message);
    }

    public string ReferenceUnreadable(string reference, string file, string why, string module)
    {
        return English.ReferenceUnreadable(reference, file, why, module);
    }

    public string ReferenceNotFound(string reference)
    {
        return English.ReferenceNotFound(reference);
    }

    public string DecompileFailed(string name)
    {
        return English.DecompileFailed(name);
    }

    public string FailureCause(string message)
    {
        return English.FailureCause(message);
    }

    public string AssemblyUnreadable(string file, string? why)
    {
        return English.AssemblyUnreadable(file, why);
    }

    public string DescribeUnopened(AssemblyLoadException error)
    {
        return English.DescribeUnopened(error);
    }

    public string ReferenceAssembly()
    {
        return English.ReferenceAssembly();
    }

    public string ModuleWithMetadata()
    {
        return English.ModuleWithMetadata();
    }

    public string ModuleWithoutMetadata()
    {
        return English.ModuleWithoutMetadata();
    }

    public string Calls(string entryPoint, string module)
    {
        return English.Calls(entryPoint, module);
    }

    public string ReferenceNotResolved()
    {
        return English.ReferenceNotResolved();
    }

    public string ResolvesTo(string path)
    {
        return English.ResolvesTo(path);
    }

    public string PictureUnreadable(PictureFault fault)
    {
        return English.PictureUnreadable(fault);
    }

    public string Truncated(int bytes)
    {
        return English.Truncated(bytes);
    }

    public string TargetFramework(string framework)
    {
        return English.TargetFramework(framework);
    }

    public string EntryPoint(string method)
    {
        return English.EntryPoint(method);
    }

    public string ForwardedTo(string destination)
    {
        return English.ForwardedTo(destination);
    }
}

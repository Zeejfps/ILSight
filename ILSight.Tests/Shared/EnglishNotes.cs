using ILSight.Core;

namespace ILSight.Tests;

/// <summary>The app's notes in English, as its catalog words them (en.json), for the core's tests, which read no
/// catalog: the app's own are tested through <c>DocumentNoteTexts</c>.</summary>
internal sealed class EnglishNotes : IDocumentNotes
{
    public static EnglishNotes Instance { get; } = new();

    public string ReferenceUnreadable(string reference, string file, string why, string module)
    {
        return $"Warning: the reference {reference} was read by name only: {file} could not be read. {why} Reload {module} once it can be read.";
    }

    public string ReferenceNotFound(string reference)
    {
        return $"Warning: the reference {reference} was read by name only: it was not found.";
    }

    public string DecompileFailed(string name)
    {
        return $"Error decompiling {name}";
    }

    public string FailureCause(string message)
    {
        return $"Cause: {message}";
    }

    public string AssemblyUnreadable(string file, string? why)
    {
        return why is null ? $"{file} isn't a .NET assembly or package that can be read." : $"{file} couldn't be read ({why}).";
    }

    public string ReferenceAssembly()
    {
        return "This assembly is marked as 'reference assembly', which means that it only contains metadata and no executable code.";
    }

    public string ModuleWithMetadata()
    {
        return "A module of this assembly, holding metadata";
    }

    public string ModuleWithoutMetadata()
    {
        return "A native library, or a module holding no metadata";
    }

    public string Calls(string entryPoint, string module)
    {
        return $"Calls {entryPoint} in {module}";
    }

    public string ReferenceNotResolved()
    {
        return "Not found";
    }

    public string ResolvesTo(string path)
    {
        return $"Resolves to {path}";
    }

    public string Truncated(int bytes)
    {
        return $"Truncated after {bytes} bytes.";
    }

    public string Architecture(ModuleArchitecture architecture)
    {
        var bits = architecture.PreferredBits;
        if (bits is null)
        {
            return $"Architecture: {architecture.Platform}";
        }

        return $"Architecture: {architecture.Platform} ({bits.Value}-bit preferred)";
    }

    public string TargetFramework(string framework)
    {
        return $"Target framework: {framework}";
    }

    public string EntryPoint(string method)
    {
        return $"Entry point: {method}";
    }

    public string ForwardedTo(string destination)
    {
        return $"Forwarded to {destination}";
    }
}

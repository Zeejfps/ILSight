using ILSight.Core;
using ILSight.Core.ResourceArea;

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

    public string DescribeUnopened(AssemblyLoadException error)
    {
        return error.Fault.Match(
            _ => DescribeInner(error),
            _ => $"{error.FilePath.Text} isn't a .NET assembly.",
            p => $"{p.Container.Text} isn't a package.",
            i => $"{i.Package.Text} holds no file {i.FullName}.");
    }

    // What the system or the library said, as written: the core's tests make no cause of ILSight's own an inner one.
    private static string DescribeInner(AssemblyLoadException error)
    {
        var why = error.InnerException;
        return why is null ? error.Message : why.Message;
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

    public string PictureUnreadable(PictureFault fault)
    {
        return fault.Match(
            static c => $"The {c.Format} is cut short.",
            static p => $"The {p.Format}'s pixels are cut short.",
            static s => $"A {s.Width}×{s.Height} picture is out of range.",
            static _ => "A palette index is out of range.",
            static _ => "Compressed BMPs are not supported.",
            static b => $"{b.Bits}-bit BMPs are not supported.",
            static n => $"The {n.Format} holds no picture.",
            static _ => "The GIF's code size is out of range.",
            static c => $"Unknown PNG color type {c.ColorType}.",
            static _ => "This is no PNG, JPEG, BMP, GIF, ICO, CUR or SVG picture.",
            static l => $"The picture cannot be read: {l.Message}");
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

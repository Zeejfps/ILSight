using System.Text;

namespace ILSight.Core;

/// <summary>The app's words for its notes (<see cref="IDocumentNotes"/>), every value said in them first made visible on
/// one line: a note runs from its comment marker to its line's end and keeps its line count in every language, so a
/// value from outside (a name or a path read from metadata or the file system, which an obfuscated assembly can fill
/// with line breaks) says each line break and other control character it holds as an escape (<c>\n</c>,
/// <c>\u000B</c>), never as itself and never as a space, and each name stays the one it is. The one place every note's
/// values pass through (<see cref="DocumentNote.Words"/>).</summary>
internal sealed class VisibleNoteValues(IDocumentNotes words) : IDocumentNotes
{
    /// <summary><paramref name="value"/> with each line break and other control character it holds escaped visibly;
    /// the value itself when it holds none.</summary>
    internal static string Visible(string value)
    {
        if (!value.Any(IsEscaped))
        {
            return value;
        }

        var text = new StringBuilder(value.Length + 8);
        foreach (var c in value)
        {
            text.Append(Escape(c));
        }

        return text.ToString();
    }

    private const char LineSeparator = (char)0x2028;
    private const char ParagraphSeparator = (char)0x2029;

    // A control character (line breaks among them), or a line or paragraph separator, which a text view may break at.
    private static bool IsEscaped(char c)
    {
        return char.IsControl(c) || c is LineSeparator or ParagraphSeparator;
    }

    private static string Escape(char c)
    {
        return c switch
        {
            '\n' => "\\n",
            '\r' => "\\r",
            '\t' => "\\t",
            _ when IsEscaped(c) => $"\\u{(int)c:X4}",
            _ => c.ToString(),
        };
    }

    public string ReferenceUnreadable(string reference, string file, string why, string module)
    {
        return words.ReferenceUnreadable(Visible(reference), Visible(file), Visible(why), Visible(module));
    }

    public string ReferenceNotFound(string reference)
    {
        return words.ReferenceNotFound(Visible(reference));
    }

    public string DecompileFailed(string name)
    {
        return words.DecompileFailed(Visible(name));
    }

    public string FailureCause(string message)
    {
        return words.FailureCause(Visible(message));
    }

    public string AssemblyUnreadable(string file, string? why)
    {
        return words.AssemblyUnreadable(Visible(file), why is null ? null : Visible(why));
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
        return words.Calls(Visible(entryPoint), Visible(module));
    }

    public string ReferenceNotResolved()
    {
        return words.ReferenceNotResolved();
    }

    public string ResolvesTo(string path)
    {
        return words.ResolvesTo(Visible(path));
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
        return words.TargetFramework(Visible(framework));
    }

    public string EntryPoint(string method)
    {
        return words.EntryPoint(Visible(method));
    }

    public string ForwardedTo(string destination)
    {
        return words.ForwardedTo(Visible(destination));
    }
}

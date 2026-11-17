using ILSight.Core.ResourceArea;

namespace ILSight.Core;

/// <summary>A note the app writes into a document beside what the document shows, kept as its cause: which note it is
/// and the values it says, as the core read them. Its words are the app's (<see cref="IDocumentNotes"/>), found again
/// in whichever language the document is shown in (<see cref="Documents.Reword"/>); the code beside it never
/// changes.</summary>
public union DocumentNote(DocumentNote.ReferenceUnreadable, DocumentNote.ReferenceNotFound, DocumentNote.DecompileFailed, DocumentNote.FailureCause,
    DocumentNote.AssemblyUnreadable, DocumentNote.ReferenceAssembly, DocumentNote.ModuleKind, DocumentNote.Calls, DocumentNote.Resolution,
    DocumentNote.PictureUnreadable, DocumentNote.Truncated, DocumentNote.ArchitectureOf, DocumentNote.TargetFramework, DocumentNote.EntryPoint,
    DocumentNote.ForwardedTo)
{
    /// <summary><paramref name="text"/>, an exception's message said in a note, on one line
    /// (<see cref="OneLineText.Message"/>): a note runs to its line's end, and keeps its line count in every language;
    /// a name or path is never joined so, but written where every value is (<see cref="Words"/>).</summary>
    internal static string OneLine(string text)
    {
        return OneLineText.Message(text);
    }

    /// <summary>What goes before the comment marker on the note's line, past what the line held before it.</summary>
    internal string Lead => this switch
    {
        ForwardedTo => " ",
        ReferenceUnreadable or ReferenceNotFound or DecompileFailed or FailureCause or AssemblyUnreadable or ReferenceAssembly or ModuleKind
            or Calls or Resolution or PictureUnreadable or Truncated or ArchitectureOf or TargetFramework or EntryPoint => "",
    };

    /// <summary>The note's words in <paramref name="notes"/>' language, without the comment marker, every value it says
    /// on the note's one line, its line breaks and other control characters escaped visibly
    /// (<see cref="VisibleNoteValues"/>).</summary>
    public string Words(IDocumentNotes notes)
    {
        return Say(new VisibleNoteValues(notes));
    }

    private string Say(IDocumentNotes notes)
    {
        return this switch
        {
            ReferenceUnreadable unreadable => notes.ReferenceUnreadable(unreadable.Reference, unreadable.File, notes.DescribeUnopened(unreadable.Why),
                unreadable.Module),
            ReferenceNotFound notFound => notes.ReferenceNotFound(notFound.Reference),
            DecompileFailed failed => notes.DecompileFailed(failed.Name),
            FailureCause cause => notes.FailureCause(cause.Message),
            AssemblyUnreadable assembly => notes.AssemblyUnreadable(assembly.File,
                assembly.Error.InnerException is null ? null : notes.DescribeUnopened(assembly.Error)),
            ReferenceAssembly => notes.ReferenceAssembly(),
            ModuleKind kind => kind.ContainsMetadata ? notes.ModuleWithMetadata() : notes.ModuleWithoutMetadata(),
            Calls calls => notes.Calls(calls.Entry, calls.ModuleName),
            Resolution resolution => resolution.Path is null ? notes.ReferenceNotResolved() : notes.ResolvesTo(resolution.Path),
            PictureUnreadable picture => notes.PictureUnreadable(picture.Fault),
            Truncated truncated => notes.Truncated(truncated.Bytes),
            ArchitectureOf architecture => notes.Architecture(architecture.Architecture),
            TargetFramework framework => notes.TargetFramework(framework.Framework),
            EntryPoint entry => notes.EntryPoint(entry.Method),
            ForwardedTo forwarded => notes.ForwardedTo(forwarded.Destination),
        };
    }

    /// <summary>The runs the note is written as in <paramref name="notes"/>' language, from its lead to the end of its
    /// line: the comment marker and the words, as a comment; an entry point's method a link to it, wherever the
    /// language's words put it.</summary>
    internal IReadOnlyList<NoteRun> Runs(IDocumentNotes notes)
    {
        return this switch
        {
            EntryPoint entry => EntryPointRuns(entry, notes),
            ReferenceUnreadable or ReferenceNotFound or DecompileFailed or FailureCause or AssemblyUnreadable or ReferenceAssembly or ModuleKind
                or Calls or Resolution or PictureUnreadable or Truncated or ArchitectureOf or TargetFramework or ForwardedTo => CommentRuns(notes),
        };
    }

    private IReadOnlyList<NoteRun> CommentRuns(IDocumentNotes notes)
    {
        return [new NoteRun { Text = Lead + DocumentBuilder.CommentMarker + Words(notes), Kind = SpanKind.Comment }];
    }

    private IReadOnlyList<NoteRun> EntryPointRuns(EntryPoint entry, IDocumentNotes notes)
    {
        var words = Words(notes);
        var method = OneLineText.Name(entry.Method);
        var at = words.IndexOf(method, StringComparison.Ordinal);
        if (at < 0)
        {
            return CommentRuns(notes);
        }

        var runs = new List<NoteRun>(3)
        {
            new NoteRun { Text = DocumentBuilder.CommentMarker + words[..at], Kind = SpanKind.Comment },
            new NoteRun { Text = method, Kind = SpanKind.Method, Reference = entry.Target },
        };
        var rest = words[(at + method.Length)..];
        if (rest.Length > 0)
        {
            runs.Add(new NoteRun { Text = rest, Kind = SpanKind.Comment });
        }

        return runs;
    }

    /// <summary>That the code was read with <see cref="Reference"/> by name only, as its file could not be read.</summary>
    public sealed record ReferenceUnreadable
    {
        public required string Reference { get; init; }

        public required string File { get; init; }

        /// <summary>Why the file couldn't be read, said as <see cref="IDocumentNotes.DescribeUnopened"/> says it.</summary>
        public required AssemblyLoadException Why { get; init; }

        public required string Module { get; init; }
    }

    /// <summary>That the code was read with <see cref="Reference"/> by name only, as it was not found.</summary>
    public sealed record ReferenceNotFound(string Reference);

    /// <summary>The heading of a document saying why <see cref="Name"/> could not be decompiled.</summary>
    public sealed record DecompileFailed(string Name);

    /// <summary>What made the decompilation fail: the exception's message as it was written, on one line; its type and
    /// stack trace stay on the document's <see cref="DecompiledDocument.Error"/>, never in its text.</summary>
    public sealed record FailureCause(string Message);

    /// <summary>That <see cref="File"/> could not be read as an assembly: why, where the system, the library or the
    /// package said (<see cref="Error"/>'s inner exception), as <see cref="IDocumentNotes.DescribeUnopened"/> says it;
    /// else as it is no assembly or package that can be read.</summary>
    public sealed record AssemblyUnreadable
    {
        public required string File { get; init; }

        public required AssemblyLoadException Error { get; init; }
    }

    /// <summary>That the assembly holds metadata and no code.</summary>
    public sealed record ReferenceAssembly;

    /// <summary>What a module of the assembly is: one holding metadata, or a native library.</summary>
    public sealed record ModuleKind(bool ContainsMetadata);

    /// <summary>That an imported method calls <see cref="Entry"/> in <see cref="ModuleName"/>.</summary>
    public sealed record Calls
    {
        public required string Entry { get; init; }

        public required string ModuleName { get; init; }
    }

    /// <summary>Where a reference's file is: <see cref="Path"/>, or not found when null.</summary>
    public sealed record Resolution(string? Path);

    /// <summary>That the resource's picture can't be shown, by <see cref="Fault"/>; its bytes are shown instead.</summary>
    public sealed record PictureUnreadable(PictureFault Fault);

    /// <summary>That a resource's bytes are shown up to <see cref="Bytes"/>.</summary>
    public sealed record Truncated(int Bytes);

    /// <summary>The assembly's architecture, <see cref="Architecture"/>.</summary>
    public sealed record ArchitectureOf(ModuleArchitecture Architecture);

    /// <summary>The framework the assembly was built for.</summary>
    public sealed record TargetFramework(string Framework);

    /// <summary>The assembly's entry point, <see cref="Method"/>, a link to <see cref="Target"/> wherever the
    /// language's words put it.</summary>
    public sealed record EntryPoint
    {
        public required string Method { get; init; }

        public required CodeReference Target { get; init; }
    }

    /// <summary>That a type is forwarded to <see cref="Destination"/>, after the type's name on its line.</summary>
    public sealed record ForwardedTo(string Destination);
}

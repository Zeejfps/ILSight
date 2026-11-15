namespace ILSight.Core;

/// <summary>A note the app writes into a document beside what the document shows, kept as its cause: which note it is
/// and the values it says, as the core read them. Its words are the app's (<see cref="IDocumentNotes"/>), found again
/// in whichever language the document is shown in (<see cref="Documents.Reword"/>); the code beside it never
/// changes.</summary>
public abstract record DocumentNote
{
    private DocumentNote()
    {
    }

    /// <summary><paramref name="text"/>, a value said in a note, on one line, each line break a space: a note runs to
    /// its line's end, and keeps its line count in every language, so an exception's message, prose, never breaks it;
    /// a name or path is never joined so, but escaped where every value is (<see cref="Words"/>).</summary>
    internal static string OneLine(string text)
    {
        return text.ReplaceLineEndings(" ");
    }

    /// <summary>What goes before the comment marker on the note's line, past what the line held before it.</summary>
    internal virtual string Lead => "";

    /// <summary>The note's words in <paramref name="notes"/>' language, without the comment marker, every value it says
    /// on the note's one line, its line breaks and other control characters escaped visibly
    /// (<see cref="VisibleNoteValues"/>).</summary>
    public string Words(IDocumentNotes notes)
    {
        return Say(new VisibleNoteValues(notes));
    }

    /// <summary>The note's words in <paramref name="notes"/>' language, given the values it says.</summary>
    private protected abstract string Say(IDocumentNotes notes);

    /// <summary>The runs the note is written as in <paramref name="notes"/>' language, from its lead to the end of its
    /// line: the comment marker and the words, as a comment.</summary>
    internal virtual IReadOnlyList<NoteRun> Runs(IDocumentNotes notes)
    {
        return [new NoteRun { Text = Lead + DocumentBuilder.CommentMarker + Words(notes), Kind = SpanKind.Comment }];
    }

    /// <summary>That the code was read with <see cref="Reference"/> by name only, as its file could not be read.</summary>
    public sealed record ReferenceUnreadable : DocumentNote
    {
        public required string Reference { get; init; }

        public required string File { get; init; }

        public required string Why { get; init; }

        public required string Module { get; init; }

        private protected override string Say(IDocumentNotes notes)
        {
            return notes.ReferenceUnreadable(Reference, File, Why, Module);
        }
    }

    /// <summary>That the code was read with <see cref="Reference"/> by name only, as it was not found.</summary>
    public sealed record ReferenceNotFound(string Reference) : DocumentNote
    {
        private protected override string Say(IDocumentNotes notes)
        {
            return notes.ReferenceNotFound(Reference);
        }
    }

    /// <summary>The heading of a document saying why <see cref="Name"/> could not be decompiled.</summary>
    public sealed record DecompileFailed(string Name) : DocumentNote
    {
        private protected override string Say(IDocumentNotes notes)
        {
            return notes.DecompileFailed(Name);
        }
    }

    /// <summary>What made the decompilation fail: the exception's message as it was written, on one line; its type and
    /// stack trace stay on the document's <see cref="DecompiledDocument.Error"/>, never in its text.</summary>
    public sealed record FailureCause(string Message) : DocumentNote
    {
        private protected override string Say(IDocumentNotes notes)
        {
            return notes.FailureCause(Message);
        }
    }

    /// <summary>That <see cref="File"/> could not be read as an assembly: <see cref="Why"/>, the message of what the
    /// reading threw, on one line; or, when null, as it is no assembly or package that can be read.</summary>
    public sealed record AssemblyUnreadable : DocumentNote
    {
        public required string File { get; init; }

        public required string? Why { get; init; }

        private protected override string Say(IDocumentNotes notes)
        {
            return notes.AssemblyUnreadable(File, Why);
        }
    }

    /// <summary>That the assembly holds metadata and no code.</summary>
    public sealed record ReferenceAssembly : DocumentNote
    {
        private protected override string Say(IDocumentNotes notes)
        {
            return notes.ReferenceAssembly();
        }
    }

    /// <summary>What a module of the assembly is: one holding metadata, or a native library.</summary>
    public sealed record ModuleKind(bool ContainsMetadata) : DocumentNote
    {
        private protected override string Say(IDocumentNotes notes)
        {
            return ContainsMetadata ? notes.ModuleWithMetadata() : notes.ModuleWithoutMetadata();
        }
    }

    /// <summary>That an imported method calls <see cref="Entry"/> in <see cref="ModuleName"/>.</summary>
    public sealed record Calls : DocumentNote
    {
        public required string Entry { get; init; }

        public required string ModuleName { get; init; }

        private protected override string Say(IDocumentNotes notes)
        {
            return notes.Calls(Entry, ModuleName);
        }
    }

    /// <summary>Where a reference's file is: <see cref="Path"/>, or not found when null.</summary>
    public sealed record Resolution(string? Path) : DocumentNote
    {
        private protected override string Say(IDocumentNotes notes)
        {
            return Path is null ? notes.ReferenceNotResolved() : notes.ResolvesTo(Path);
        }
    }

    /// <summary>That a resource's bytes are shown up to <see cref="Bytes"/>.</summary>
    public sealed record Truncated(int Bytes) : DocumentNote
    {
        private protected override string Say(IDocumentNotes notes)
        {
            return notes.Truncated(Bytes);
        }
    }

    /// <summary>The assembly's architecture, <see cref="Architecture"/>.</summary>
    public sealed record ArchitectureOf(ModuleArchitecture Architecture) : DocumentNote
    {
        private protected override string Say(IDocumentNotes notes)
        {
            return notes.Architecture(Architecture);
        }
    }

    /// <summary>The framework the assembly was built for.</summary>
    public sealed record TargetFramework(string Framework) : DocumentNote
    {
        private protected override string Say(IDocumentNotes notes)
        {
            return notes.TargetFramework(Framework);
        }
    }

    /// <summary>The assembly's entry point, <see cref="Method"/>, a link to <see cref="Target"/> wherever the
    /// language's words put it.</summary>
    public sealed record EntryPoint : DocumentNote
    {
        public required string Method { get; init; }

        public required CodeReference Target { get; init; }

        private protected override string Say(IDocumentNotes notes)
        {
            return notes.EntryPoint(Method);
        }

        internal override IReadOnlyList<NoteRun> Runs(IDocumentNotes notes)
        {
            var words = Words(notes);
            var method = VisibleNoteValues.Visible(Method);
            var at = words.IndexOf(method, StringComparison.Ordinal);
            if (at < 0)
            {
                return base.Runs(notes);
            }

            var runs = new List<NoteRun>(3)
            {
                new NoteRun { Text = DocumentBuilder.CommentMarker + words[..at], Kind = SpanKind.Comment },
                new NoteRun { Text = method, Kind = SpanKind.Method, Reference = Target },
            };
            var rest = words[(at + method.Length)..];
            if (rest.Length > 0)
            {
                runs.Add(new NoteRun { Text = rest, Kind = SpanKind.Comment });
            }

            return runs;
        }
    }

    /// <summary>That a type is forwarded to <see cref="Destination"/>, after the type's name on its line.</summary>
    public sealed record ForwardedTo(string Destination) : DocumentNote
    {
        internal override string Lead => " ";

        private protected override string Say(IDocumentNotes notes)
        {
            return notes.ForwardedTo(Destination);
        }
    }
}

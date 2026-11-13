using System.Diagnostics;

namespace ILSight;

/// <summary>Something the user asked for that the app refused, and why: said where they acted, in the status bar
/// (<see cref="RefusalLine"/>), worded in the language the screen shows (<see cref="Words"/>). A refusal is no failure:
/// it isn't added to the errors.</summary>
public abstract record Refusal
{
    private Refusal()
    {
    }

    /// <summary>The resource <see cref="Resource"/> was not saved: its assembly, the file <see cref="Assembly"/>, was
    /// closed before it was read.</summary>
    public sealed record ResourceNotSaved : Refusal
    {
        public required string Resource { get; init; }

        public required string Assembly { get; init; }
    }

    /// <summary>The PDB <see cref="Pdb"/> was not read for the assembly, the file <see cref="Assembly"/>: it was closed
    /// before it was read.</summary>
    public sealed record PdbNotRead : Refusal
    {
        public required string Pdb { get; init; }

        public required string Assembly { get; init; }
    }

    /// <summary>The file at <see cref="Path"/>, dropped on the window, was not opened: it is not a full path.</summary>
    public sealed record NotFullPath : Refusal
    {
        public required string Path { get; init; }
    }

    /// <summary>What was refused and why, in <paramref name="t"/>'s language.</summary>
    public (string What, string Why) Words(Strings t)
    {
        return this switch
        {
            ResourceNotSaved r => (t.ErrorSaveResource(r.Resource), t.DocumentClosed(r.Assembly)),
            PdbNotRead r => (t.ErrorSelectPdb(r.Pdb, r.Assembly), t.DocumentClosed(r.Assembly)),
            NotFullPath r => (t.ErrorOpenFile(r.Path), t.ErrorNotFullPath),
            _ => throw new UnreachableException($"Refusal.Words does not handle {GetType().Name}."),
        };
    }
}

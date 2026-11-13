using System.Diagnostics;

namespace ILSight.ExportArea;

/// <summary>What keeps an export from starting.</summary>
public abstract record ExportBlocker
{
    private ExportBlocker()
    {
    }

    public sealed record NoAssembly : ExportBlocker;

    public sealed record NoFolder : ExportBlocker;

    public sealed record NamelessProject : ExportBlocker;

    /// <summary>Two ticked assemblies take the project name <see cref="Name"/>.</summary>
    public sealed record Clash(string Name) : ExportBlocker;

    public sealed record KeyFileMissing : ExportBlocker;

    /// <summary>What the disk says about the folder and key file named is still being looked at.</summary>
    public sealed record Checking : ExportBlocker;

    /// <summary>The look at the disk for the folder and key file named failed, as <see cref="Why"/> says; the user can
    /// ask it again.</summary>
    public sealed record CheckFailed(string Why) : ExportBlocker;

    /// <summary>What the handler of whichever case this is makes of it and <paramref name="state"/>, which it is
    /// handed, so that a handler that needs the caller's values can be static and allocate nothing.</summary>
    public TResult Match<TState, TResult>(TState state, Func<TState, NoAssembly, TResult> noAssembly,
        Func<TState, NoFolder, TResult> noFolder, Func<TState, NamelessProject, TResult> namelessProject,
        Func<TState, Clash, TResult> clash, Func<TState, KeyFileMissing, TResult> keyFileMissing,
        Func<TState, Checking, TResult> checking, Func<TState, CheckFailed, TResult> checkFailed)
    {
        return this switch
        {
            NoAssembly c => noAssembly(state, c),
            NoFolder c => noFolder(state, c),
            NamelessProject c => namelessProject(state, c),
            Clash c => clash(state, c),
            KeyFileMissing c => keyFileMissing(state, c),
            Checking c => checking(state, c),
            CheckFailed c => checkFailed(state, c),
            _ => throw new UnreachableException($"ExportBlocker.Match does not handle {GetType().Name}."),
        };
    }
}

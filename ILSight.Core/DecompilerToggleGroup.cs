using System.Diagnostics;

namespace ILSight.Core;

/// <summary>What a <see cref="DecompilerToggle"/> is about.</summary>
public abstract record DecompilerToggleGroup
{
    private DecompilerToggleGroup()
    {
    }

    /// <summary>A C# feature, used only from <see cref="IntroducedIn"/> on.</summary>
    public sealed record Language(CSharpVersion IntroducedIn) : DecompilerToggleGroup;

    public sealed record Other : DecompilerToggleGroup;

    /// <summary>Clean-ups for code the F# compiler emits.</summary>
    public sealed record FSharp : DecompilerToggleGroup;

    /// <summary>How exported projects are laid out; does not change decompiled code.</summary>
    public sealed record ProjectExport : DecompilerToggleGroup;

    /// <summary>What the handler of whichever case this is makes of it.</summary>
    public TResult Match<TResult>(Func<Language, TResult> language, Func<Other, TResult> other,
        Func<FSharp, TResult> fSharp, Func<ProjectExport, TResult> projectExport)
    {
        return this switch
        {
            Language c => language(c),
            Other c => other(c),
            FSharp c => fSharp(c),
            ProjectExport c => projectExport(c),
            _ => throw new UnreachableException($"DecompilerToggleGroup.Match does not handle {GetType().Name}."),
        };
    }

    /// <summary>What the handler of whichever case this is makes of it and <paramref name="state"/>, which it is
    /// handed, so that a handler that needs the caller's values can be static and allocate nothing.</summary>
    public TResult Match<TState, TResult>(TState state, Func<TState, Language, TResult> language,
        Func<TState, Other, TResult> other, Func<TState, FSharp, TResult> fSharp,
        Func<TState, ProjectExport, TResult> projectExport)
    {
        return this switch
        {
            Language c => language(state, c),
            Other c => other(state, c),
            FSharp c => fSharp(state, c),
            ProjectExport c => projectExport(state, c),
            _ => throw new UnreachableException($"DecompilerToggleGroup.Match does not handle {GetType().Name}."),
        };
    }
}

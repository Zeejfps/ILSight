using System.Diagnostics;
using ILSight.Core;

namespace ILSight.CodeViewArea;

/// <summary>What the user asked a code view to do, besides changing the size of its code.</summary>
public abstract record CodeViewIntent
{
    private CodeViewIntent()
    {
    }

    /// <summary>Whether it can be done in the code of an assembly that was closed (<see cref="TabLoad.ShowClosed"/>),
    /// reading nothing of it: not, unless the case says so, as following, analyzing or showing a reference looks the
    /// assembly up.</summary>
    public virtual bool CanRunWhenClosed => false;

    /// <summary>Go to <see cref="Target"/>, in a new tab when <see cref="InNewTab"/>.</summary>
    public sealed record Navigate : CodeViewIntent
    {
        public required CodeReference.Navigable Target { get; init; }

        public required bool InNewTab { get; init; }
    }

    /// <summary>Marks or unmarks <see cref="Line"/>, which the document already holds.</summary>
    public sealed record ToggleBookmark(int Line) : CodeViewIntent
    {
        public override bool CanRunWhenClosed => true;
    }

    public sealed record Analyze(CodeReference.Entity Entity) : CodeViewIntent;

    public sealed record ShowInMetadata(CodeReference.Navigable Target) : CodeViewIntent;

    public sealed record ShowInApiReference(CodeReference.Entity Entity) : CodeViewIntent;

    public sealed record ShowInDiagram(CodeReference.Entity Type) : CodeViewIntent;

    public sealed record ShowInNewDiagram(CodeReference.Entity Type) : CodeViewIntent;

    /// <summary>Does what the handler of whichever case this is does with it and <paramref name="state"/>, which it
    /// is handed, so that a handler that needs the caller's values can be static and allocate nothing.</summary>
    public void Switch<TState>(TState state, Action<TState, Navigate> navigate,
        Action<TState, ToggleBookmark> toggleBookmark, Action<TState, Analyze> analyze,
        Action<TState, ShowInMetadata> showInMetadata, Action<TState, ShowInApiReference> showInApiReference,
        Action<TState, ShowInDiagram> showInDiagram, Action<TState, ShowInNewDiagram> showInNewDiagram)
    {
        switch (this)
        {
            case Navigate c:
                navigate(state, c);
                return;
            case ToggleBookmark c:
                toggleBookmark(state, c);
                return;
            case Analyze c:
                analyze(state, c);
                return;
            case ShowInMetadata c:
                showInMetadata(state, c);
                return;
            case ShowInApiReference c:
                showInApiReference(state, c);
                return;
            case ShowInDiagram c:
                showInDiagram(state, c);
                return;
            case ShowInNewDiagram c:
                showInNewDiagram(state, c);
                return;
            default:
                throw new UnreachableException($"CodeViewIntent.Switch does not handle {GetType().Name}.");
        }
    }
}

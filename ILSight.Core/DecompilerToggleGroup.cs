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
}

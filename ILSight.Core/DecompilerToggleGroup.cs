namespace ILSight.Core;

/// <summary>What a <see cref="DecompilerToggle"/> is about.</summary>
public union DecompilerToggleGroup(DecompilerToggleGroup.Language, DecompilerToggleGroup.Other,
    DecompilerToggleGroup.FSharp, DecompilerToggleGroup.ProjectExport)
{
    /// <summary>A C# feature, used only from <see cref="IntroducedIn"/> on.</summary>
    public sealed record Language(CSharpVersion IntroducedIn);

    public sealed record Other;

    /// <summary>Clean-ups for code the F# compiler emits.</summary>
    public sealed record FSharp;

    /// <summary>How exported projects are laid out; does not change decompiled code.</summary>
    public sealed record ProjectExport;
}

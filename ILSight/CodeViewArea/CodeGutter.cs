namespace ILSight.CodeViewArea;

/// <summary>Where a code view's gutter puts things, from its left edge: the bookmark margin, the line numbers of
/// <see cref="Digits"/> digits, the fold chevrons, then the text. All zero but the text's padding where the view
/// shows no gutter.</summary>
internal readonly struct CodeGutter
{
    public required float MarkWidth { get; init; }

    public required int Digits { get; init; }

    public required float FoldLeft { get; init; }

    public required float FoldWidth { get; init; }

    public required float Width { get; init; }

    public required float TextLeft { get; init; }
}

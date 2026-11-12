namespace ILSight.CodeView;

/// <summary>What the pointer is over in a code view this frame: nothing; the text of a line, on one of its spans
/// or none and on a collapsed fold's chip or none; a fold's chevron; or the bookmark margin of a line. A struct
/// with a case for each, as it is made every frame.</summary>
internal readonly struct CodeHover
{
    private enum Place : byte
    {
        Nothing,
        Text,
        Chevron,
        Mark,
    }

    private readonly Place _place;
    private readonly int _line;
    private readonly int? _span;
    private readonly int? _fold;

    private CodeHover(Place place, int line, int? span, int? fold)
    {
        _place = place;
        _line = line;
        _span = span;
        _fold = fold;
    }

    public static CodeHover Nothing => default;

    /// <summary>Over the text of <paramref name="line"/>, on its span <paramref name="span"/> or none, and on
    /// the chip of the collapsed fold <paramref name="chip"/> or none.</summary>
    public static CodeHover OnText(int line, int? span, int? chip)
    {
        return new CodeHover(Place.Text, line, span, chip);
    }

    public static CodeHover OnChevron(int fold)
    {
        return new CodeHover(Place.Chevron, 0, null, fold);
    }

    public static CodeHover OnMark(int line)
    {
        return new CodeHover(Place.Mark, line, null, null);
    }

    /// <summary>The span under the pointer, or null.</summary>
    public CodeSpanAt? Span => _place == Place.Text && _span is not null ? new CodeSpanAt(_line, _span.Value) : null;

    /// <summary>The collapsed fold whose chip is under the pointer, or null.</summary>
    public int? Chip => _place == Place.Text ? _fold : null;

    /// <summary>The fold whose chevron is under the pointer, or null.</summary>
    public int? Chevron => _place == Place.Chevron ? _fold : null;

    /// <summary>The line whose bookmark margin is under the pointer, or null.</summary>
    public int? Mark => _place == Place.Mark ? _line : null;

    /// <summary>Whether a press here works a control, a chip, chevron or mark, rather than the text.</summary>
    public bool IsOnControl => Chip is not null || Chevron is not null || Mark is not null;
}

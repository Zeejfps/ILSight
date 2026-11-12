namespace ILSight;

/// <summary>The press held on a code view, or last held: on a bookmark mark, a fold's chevron or a collapsed
/// fold, which a drag or click leaves alone; or on the text.</summary>
internal abstract record CodePress
{
    private CodePress()
    {
    }

    public sealed record OnControl : CodePress;

    /// <summary>A press on the text at <see cref="At"/>, with Ctrl held when <see cref="ForNewTab"/>, on the
    /// span <see cref="Span"/> of a line or none.</summary>
    public sealed record OnText(TextPosition At, bool ForNewTab, CodeSpanAt? Span) : CodePress
    {
        /// <summary>Whether the press has moved off where it started, dragging a selection.</summary>
        public bool Moved;
    }
}

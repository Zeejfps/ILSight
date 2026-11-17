namespace ILSight.CodeViewArea;

/// <summary>The press held on a code view, or last held: on a bookmark mark, a fold's chevron or a collapsed
/// fold, which a drag or click leaves alone; or on the text.</summary>
internal union CodePress(CodePress.OnControl, CodePress.OnText)
{
    public sealed record OnControl;

    /// <summary>A press on the text at <see cref="At"/>, with Ctrl held when <see cref="ForNewTab"/>, on the
    /// span <see cref="Span"/> of a line or none.</summary>
    public sealed record OnText
    {
        public required TextPosition At { get; init; }

        public required bool ForNewTab { get; init; }

        public required CodeSpanAt? Span { get; init; }

        /// <summary>Whether the press has moved off where it started, dragging a selection.</summary>
        public bool Moved;
    }
}

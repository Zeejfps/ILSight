using System.Diagnostics;
using ZGF.Svg;

namespace ILSight;

/// <summary>An icon as an icon theme file writes it, with the file and line it is written on: a glyph of
/// a font, or a picture read from an SVG file.</summary>
public abstract record IconValue
{
    private IconValue()
    {
    }

    public required string File { get; init; }

    public required int Line { get; init; }

    /// <summary>The codepoint <see cref="Codepoint"/> of the font the theme declares as <see cref="Font"/>.</summary>
    public sealed record Glyph : IconValue
    {
        public required string Font { get; init; }

        public required int Codepoint { get; init; }
    }

    /// <summary>The picture <see cref="Svg"/> read from the file at <see cref="SvgPath"/>.</summary>
    public sealed record Picture : IconValue
    {
        public required string SvgPath { get; init; }

        public required SvgDocument Svg { get; init; }
    }

    /// <summary>What the handler of whichever case this is makes of it and <paramref name="state"/>, which it is
    /// handed, so that a handler that needs the caller's values can be static and allocate nothing.</summary>
    public TResult Match<TState, TResult>(TState state, Func<TState, Glyph, TResult> glyph,
        Func<TState, Picture, TResult> picture)
    {
        return this switch
        {
            Glyph c => glyph(state, c),
            Picture c => picture(state, c),
            _ => throw new UnreachableException($"IconValue.Match does not handle {GetType().Name}."),
        };
    }

    /// <summary>Does what the handler of whichever case this is does with it and <paramref name="state"/>, which it
    /// is handed, so that a handler that needs the caller's values can be static and allocate nothing.</summary>
    public void Switch<TState>(TState state, Action<TState, Glyph> glyph, Action<TState, Picture> picture)
    {
        switch (this)
        {
            case Glyph c:
                glyph(state, c);
                return;
            case Picture c:
                picture(state, c);
                return;
            default:
                throw new UnreachableException($"IconValue.Switch does not handle {GetType().Name}.");
        }
    }
}

using ZGF.Gui;
using ILSight.Core;

namespace ILSight;

/// <summary>The text styles a code tooltip is set in: prose at the theme's text size, headings
/// smaller and muted, code in the code palette's colors.</summary>
internal sealed class TooltipStyles
{
    public required TextStyle Plain { get; init; }
    public required TextStyle Bold { get; init; }
    public required TextStyle Italic { get; init; }
    public required TextStyle Heading { get; init; }
    public required TextStyle Container { get; init; }
    public required TextStyle CodeBlock { get; init; }

    /// <summary>Code colored as the palette colors a kind of span.</summary>
    public required Func<SpanKind, TextStyle> Code { get; init; }

    /// <summary>A type or member named in prose, colored as code colors a kind of span.</summary>
    public required Func<SpanKind, TextStyle> Reference { get; init; }
}

namespace ILSight.Core;

/// <summary>A span <see cref="HighlightingTokenWriter"/> started on a <see cref="DocumentBuilder"/>
/// (<see cref="DocumentBuilder.StartSpan"/>): disposing it ends the span.</summary>
internal ref struct SpanScope
{
    public required DocumentBuilder Builder { get; init; }

    public void Dispose()
    {
        Builder.EndSpan();
    }
}

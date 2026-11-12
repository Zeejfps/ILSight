namespace ILSight.Core;

/// <summary>How a <see cref="DocumentSpan"/> is colored.</summary>
public enum SpanKind : byte
{
    Plain,
    Keyword,
    Type,
    Namespace,
    Method,
    Field,
    Property,
    Event,
    Parameter,
    Local,
    String,
    Number,
    Comment,
    Preprocessor,
    ILOpcode,
    ILLabel,
}

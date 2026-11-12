namespace ILSight.Core;

internal abstract record UsedEntry
{
    private UsedEntry()
    {
    }

    public sealed record Type(string FullName, CodeReference? Target, IReadOnlyList<UsedEntry> Used) : UsedEntry;

    public sealed record Member(string Label, SpanKind Kind, CodeReference? Target) : UsedEntry;

    public sealed record Forwarded(ForwardedLine Line) : UsedEntry;
}

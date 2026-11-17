namespace ILSight.Core;

internal union UsedEntry(UsedEntry.Type, UsedEntry.Member, UsedEntry.Forwarded)
{
    public sealed record Type
    {
        public required string FullName { get; init; }

        public required CodeReference? Target { get; init; }

        public required IReadOnlyList<UsedEntry> Used { get; init; }
    }

    public sealed record Member
    {
        public required string Label { get; init; }

        public required SpanKind Kind { get; init; }

        public required CodeReference? Target { get; init; }
    }

    public sealed record Forwarded(ForwardedLine Line);
}

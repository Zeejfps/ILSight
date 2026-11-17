namespace ILSight.Core.AnalysisArea;

/// <summary>What an analysis finds: a symbol, or a module it should have searched that the library could not read, or
/// one it read only in part, named beside what the others gave.</summary>
public union AnalysisFind(AnalysisFind.Found, AnalysisFind.Unreadable, AnalysisFind.PartlyRead)
{
    public sealed record Found
    {
        public required AnalysisSymbol Symbol { get; init; }
    }

    public sealed record Unreadable
    {
        /// <summary>The module's file name.</summary>
        public required string Name { get; init; }

        /// <summary>Why the library could not read it.</summary>
        public required string Why { get; init; }

        /// <summary>What the library threw, so its record has the diagnostic (CF3).</summary>
        public required Exception Error { get; init; }
    }

    /// <summary>A module the analysis read but for <see cref="Count"/> parts the library couldn't read (a method body,
    /// an instruction, a token, a method's locals, an attribute's value), the first for <see cref="Why"/>.</summary>
    public sealed record PartlyRead
    {
        /// <summary>The module's file name.</summary>
        public required string Name { get; init; }

        public required int Count { get; init; }

        public required string Why { get; init; }

        /// <summary>What the library threw on the first part, so its record has the diagnostic (CF3).</summary>
        public required Exception Error { get; init; }
    }
}

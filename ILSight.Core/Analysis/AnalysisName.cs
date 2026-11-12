namespace ILSight.Core.Analysis;

/// <summary>What an analysis finds, which names it in the interface.</summary>
public enum AnalysisName
{
    AppliedTo,
    ImplementedBy,
    OverriddenBy,
    ReadBy,
    AssignedBy,
    AddressTakenBy,
    Implements,
    Overrides,
    UsedBy,
    Uses,
    ExposedBy,
    ExtensionMethods,
    InstantiatedBy,
}

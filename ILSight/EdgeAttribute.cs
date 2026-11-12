namespace ILSight;

/// <summary>
/// Marks an edge (RULES.md "Edges"): a type the framework hands control to, where work starts, everything is caught
/// and use cases and adapters are wired. RuleLab's checks treat the code of a marked type, and of the types nested in
/// it, as edge code; they know the attribute by its name. On the type, the marking moves with it when a file is split,
/// which a file name in .editorconfig doesn't.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
internal sealed class EdgeAttribute : Attribute
{
}

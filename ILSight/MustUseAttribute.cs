namespace ILSight;

/// <summary>
/// Marks a type whose values the caller must act on: dropping one leaves state stuck (a ticket for I/O only the
/// caller starts: dropped, the tab says "loading" for good). RL0807 reports a marked value used as a statement,
/// discarded with <c>_ =</c>, returned by a lambda whose delegate returns nothing, or put in a local nothing reads.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
public sealed class MustUseAttribute : Attribute
{
}

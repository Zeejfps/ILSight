namespace ILSight.Core;

/// <summary>
/// Marks a type whose values the caller must act on: dropping one leaves state stuck (modules taken off the list that
/// nothing releases). RL0807 reports a marked value used as a statement, discarded with <c>_ =</c>, returned by a lambda
/// whose delegate returns nothing, or put in a local nothing reads.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
internal sealed class MustUseAttribute : Attribute
{
}

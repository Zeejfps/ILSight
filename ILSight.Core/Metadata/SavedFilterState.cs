// Derived from ILSpy (MIT): see LICENSE-ILSpy.txt

namespace ILSight.Core.Metadata
{
	/// <summary>What a <see cref="FilterState"/> constrains, to keep between runs: the values each
	/// narrowed group allows and the flags not left don't-care, by name.</summary>
	public sealed record SavedFilterState(Dictionary<string, uint[]>? Groups, Dictionary<string, TriState>? Flags, MatchMode Mode);
}

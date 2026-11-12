// Derived from ILSpy (MIT): see LICENSE-ILSpy.txt

namespace ILSight.Core.Metadata
{
	/// <summary>
	/// Static description of how a <see cref="FlagsAttribute"/> enum's bits decompose into
	/// mutually-exclusive sub-ranges (mutex groups carved out by <c>*Mask</c> fields) and
	/// independent single-bit flags. The filter UI renders from this shape; the matcher
	/// applies it bitwise.
	/// </summary>
	public sealed record FlagsSchema
	{
		public required Type EnumType { get; init; }

		public required IReadOnlyList<MutexGroup> MutexGroups { get; init; }

		public required IReadOnlyList<IndependentFlag> IndependentFlags { get; init; }
	}
}

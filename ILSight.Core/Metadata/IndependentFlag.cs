// Derived from ILSpy (MIT): see LICENSE-ILSpy.txt

namespace ILSight.Core.Metadata
{
	/// <summary>
	/// One single-bit flag whose presence is independent of any mutex group. Filtering on
	/// these is tri-state (don't care / required / excluded).
	/// </summary>
	public sealed record IndependentFlag
	{
		public required string Name { get; init; }

		public required uint Bit { get; init; }
	}
}

// Derived from ILSpy (MIT): see LICENSE-ILSpy.txt

namespace ILSight.Core.Metadata
{
	/// <summary>
	/// One single-bit flag whose presence is independent of any mutex group. Filtering on
	/// these is tri-state (don't care / required / excluded).
	/// </summary>
	public sealed record IndependentFlag(string Name, uint Bit);
}

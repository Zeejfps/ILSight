// Derived from ILSpy (MIT): see LICENSE-ILSpy.txt

namespace ILSight.Core.Metadata
{
	/// <summary>
	/// One sub-range of an enum carved out by a <c>*Mask</c> field. A row's value carries
	/// exactly one of <see cref="Values"/>, isolated by ANDing with <see cref="Mask"/>.
	/// </summary>
	public sealed record MutexGroup(
		string Name,
		uint Mask,
		IReadOnlyList<MutexValue> Values);
}

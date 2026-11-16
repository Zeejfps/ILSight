// Derived from ILSpy (MIT): see LICENSE-ILSpy.txt

namespace ILSight.Core.MetadataArea
{
	public sealed record MutexValue
	{
		public required string Label { get; init; }

		public required uint Value { get; init; }
	}
}

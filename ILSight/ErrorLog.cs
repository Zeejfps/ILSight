using ILSight.Core;

namespace ILSight;

/// <summary>The recent errors, newest last, the oldest dropped past a capacity. UI thread only.</summary>
public sealed class ErrorLog
{
    private const int Capacity = 50;

    public readonly BoundedList<AppError> Entries = new(Capacity);

    /// <summary>Changes whenever an error is added; clearing leaves it.</summary>
    public int Revision => Entries.Added;
}

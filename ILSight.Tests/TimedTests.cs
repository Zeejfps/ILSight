namespace ILSight.Tests;

/// <summary>Tests that time their work run here: after the parallel ones, one at a time, so other tests in this
/// assembly don't load the machine while the clock runs.</summary>
[CollectionDefinition(nameof(TimedTests), DisableParallelization = true)]
public sealed class TimedTests
{
}

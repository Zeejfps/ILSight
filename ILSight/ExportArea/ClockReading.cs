namespace ILSight.ExportArea;

/// <summary>A reading of a clock that only goes forward, in the clock's own ticks: only the clock that read it tells
/// how long it has been since (<see cref="ExportWriting.IPorts.MeasureSince"/>).</summary>
public readonly record struct ClockReading(long Ticks);

namespace ILSight.Core.ResourceArea;

/// <summary>The read of a resource's bytes (<see cref="ResourceData.ReadData"/>): a type of the app's own, so a catch
/// around invoking it is known to hold the library's read.</summary>
internal delegate byte[] ResourceBytesRead();

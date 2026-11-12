namespace ILSight.Core.Resources;

/// <summary>A resource's bytes, which can be read to save them.</summary>
/// <param name="Description">What the resource is, as a resources folder lists it.</param>
/// <param name="FileName">The name a saved copy is offered under.</param>
/// <param name="ReadData">What a saved copy holds, read only when asked for.</param>
internal sealed record ResourceData(string Description, string FileName, Func<byte[]> ReadData);

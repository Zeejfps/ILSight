namespace ILSight.Core;

/// <summary>An assembly of the running .NET runtime, offered to open from a picker.</summary>
public sealed record RuntimeAssembly(string Name, string Framework, AssemblyPath FilePath);

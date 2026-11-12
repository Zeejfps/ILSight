namespace ILSight.Core;

/// <summary>Where a type or member is declared: in a type, by its keyword such as <c>class</c> or
/// <c>record struct</c> and its full name, or in a namespace.</summary>
public sealed record CodeContainer(string Keyword, string Name, bool IsNamespace);

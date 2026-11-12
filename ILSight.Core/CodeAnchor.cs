namespace ILSight.Core;

/// <summary>
/// A line of decompiled code named by what is on it rather than where it is: the definition of the
/// entity with metadata <see cref="Token"/> in the assembly at <see cref="FilePath"/>, or the statement
/// at <see cref="ILOffset"/> of that method. It finds its line again with
/// <see cref="Documents.FindLine"/> in any document showing it, in C# or IL, after the
/// assembly is reloaded or the text is laid out differently.
/// </summary>
public readonly record struct CodeAnchor(CodeAnchorKind Kind, AssemblyPath FilePath, int Token, int ILOffset = 0);

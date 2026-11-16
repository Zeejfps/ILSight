namespace ILSight.Core;

/// <summary>Thrown when the definition a reference points at is in a reference the type system read by name only, whose
/// file is there but could not be read: the lookup needs that file, so it fails with why (the file's
/// <see cref="AssemblyLoadException"/>, inner) rather than saying the definition isn't there.</summary>
public sealed class DefinitionUnreadException(string message, AssemblyLoadException inner) : Exception(message, inner);

namespace ILSight.Core;

/// <summary>Thrown when the definition a reference points at is in a reference the type system read by name only, whose
/// file is there but could not be read: the lookup needs that file, so it fails with why rather than saying the
/// definition isn't there. <see cref="Assembly"/> and <see cref="Unread"/> (the file's
/// <see cref="AssemblyLoadException"/>, also the inner exception) are the cause the app words, the message its English
/// diagnostic.</summary>
public sealed class DefinitionUnreadException(string assembly, AssemblyLoadException unread, string diagnostic) : Exception(diagnostic, unread)
{
    /// <summary>The name of the reference holding the definition.</summary>
    public string Assembly { get; } = assembly;

    /// <summary>Why the reference's file could not be read.</summary>
    public AssemblyLoadException Unread { get; } = unread;
}

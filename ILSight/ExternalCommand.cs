using ILSight.Core;

namespace ILSight;

/// <summary>What the app is asked to do from outside it, by the arguments of this run or of a later run
/// that hands it to the app already open. It is all that passes between runs; how a run starts is in
/// <see cref="LaunchConfig"/>.</summary>
/// <param name="Language">The language to decompile to, or null to keep the one the settings have.</param>
/// <param name="AssemblyList">The name of the assembly list to open, made when there is none by that
/// name, or null to open the one the settings have open.</param>
/// <param name="ClearList">Whether to take every assembly off the list before opening
/// <paramref name="AssemblyPaths"/>.</param>
/// <param name="NavigateTo">The documentation id of the namespace, type or member to show, such as
/// <c>T:System.String</c>, looked for in <paramref name="AssemblyPaths"/>, or in every open assembly when
/// there are none; null to show the last file opened.</param>
/// <param name="AssemblyPaths">The files to open.</param>
/// <param name="Ignored">The arguments that ask for nothing the app can do, as they were given: an option
/// it does not have, a language it does not have, an option with no value, or a file that is no path.</param>
public sealed record ExternalCommand(
    DecompilationLanguage? Language,
    string? AssemblyList,
    bool ClearList,
    string? NavigateTo,
    List<AssemblyPath> AssemblyPaths,
    List<string> Ignored)
{
    /// <summary>The files to open and what to show in them.</summary>
    public FilesToOpen Files => new(AssemblyPaths, NavigateTo);
}

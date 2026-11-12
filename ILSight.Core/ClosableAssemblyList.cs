namespace ILSight.Core;

/// <summary>The assembly list itself, which takes assemblies off it: whoever makes one holds it, and hands out the
/// list (<see cref="List"/>), which reads it and opens assemblies onto it but closes none. A holder that keeps a rule
/// across the list and something else (an analysis is of an assembly on the list) closes through this, so that no one
/// handed the list closes around it.</summary>
public sealed class ClosableAssemblyList
{
    /// <summary>A new, empty list whose assemblies resolve their references among <paramref name="runtime"/>'s
    /// too.</summary>
    public ClosableAssemblyList(InstalledRuntime runtime)
    {
        List = new AssemblyList { Runtime = runtime };
    }

    /// <summary>The list, to read and to open assemblies onto.</summary>
    public AssemblyList List { get; }

    /// <summary>Takes the item holding <paramref name="assembly"/> off the list (the assembly, or the package it was
    /// read out of), as <see cref="AssemblyList.Close"/> does; one no longer listed is passed over.</summary>
    public void Close(LoadedAssembly assembly)
    {
        List.Close(assembly);
    }

    /// <summary>Takes every assembly off the list, as <see cref="AssemblyList.CloseAll"/> does.</summary>
    public void CloseAll()
    {
        List.CloseAll();
    }
}

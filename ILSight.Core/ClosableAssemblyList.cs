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
    /// read out of), as <see cref="AssemblyList.Close"/> does; one no longer listed is passed over. Returns the modules
    /// it took off, for the I/O side to release.</summary>
    public RemovedModules Close(LoadedAssembly assembly)
    {
        return List.Close(assembly);
    }

    /// <summary>Lists <paramref name="fresh"/>, read again for the listed <paramref name="item"/>, in its place, as
    /// <see cref="AssemblyList.TryReplace"/> does: reads nothing. False when the item is no longer listed.
    /// <paramref name="removed"/> is what nothing lists after: the item replaced, or the fresh one, not listed.</summary>
    public bool TryReplace(LoadedAssembly item, FreshItem fresh, out RemovedModules removed)
    {
        return List.TryReplace(item, fresh, out removed);
    }

    /// <summary>Records <paramref name="paths"/>, asked for, as on their way (<see cref="AssemblyList.Arriving"/>)
    /// until an assembly is listed for each, they stop arriving or the list is cleared.</summary>
    public void BeginArriving(IReadOnlyList<AssemblyPath> paths)
    {
        List.BeginArriving(paths);
    }

    /// <summary>Takes <paramref name="paths"/>, whose asking stopped, off those on their way, each once; returns those
    /// that were still on their way, which never landed.</summary>
    public List<AssemblyPath> StopArriving(IReadOnlyList<AssemblyPath> paths)
    {
        return List.StopArriving(paths);
    }

    /// <summary>Takes every assembly off the list, as <see cref="AssemblyList.CloseAll"/> does. Returns the modules it
    /// took off, for the I/O side to release.</summary>
    public RemovedModules CloseAll()
    {
        return List.CloseAll();
    }
}

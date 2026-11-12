using ILSight.Core;

namespace ILSight.ComparisonPicking;

/// <summary>The "compare with" dialog as its widgets bind it: the filter typed; and <see cref="Choice"/>, the
/// assembly to compare and those offered to compare it with, which keeps its own rules.</summary>
public sealed class ComparisonPicker
{
    public string Filter = "";

    /// <summary>The assembly to compare, those it can be compared with and the one selected.</summary>
    public ComparisonChoice Choice { get; } = new();

    /// <summary>Opens the dialog to pick what to compare <paramref name="compared"/> with, its filter empty.</summary>
    public void Open(LoadedModule compared)
    {
        Filter = "";
        Choice.Open(compared);
    }

    /// <summary>Takes in the filter typed and the assemblies open; see <see cref="ComparisonChoice.Update"/>.</summary>
    public void Update(AssemblyList assemblies)
    {
        Choice.Update(assemblies, Filter);
    }
}

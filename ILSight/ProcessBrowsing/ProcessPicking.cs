using System.Diagnostics;
using ILSight.Core;
using ILSight.Core.Processes;

namespace ILSight.ProcessBrowsing;

/// <summary>What the "open from running process" dialog knows of the processes: which a filter matches, and
/// what is known of a module listing whatever came of it.</summary>
public static class ProcessPicking
{
    /// <summary>Whether <paramref name="filter"/> is in the process's name, id or entry assembly's name.</summary>
    public static bool IsMatch(RunningProcess process, string filter)
    {
        if (filter.Length == 0)
        {
            return true;
        }

        if (process.Name.Contains(filter, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var idText = process.Id.ToString();
        if (idText.Contains(filter, StringComparison.Ordinal))
        {
            return true;
        }

        if (process.Info == null || process.Info.EntryAssemblyName is null)
        {
            return false;
        }

        return process.Info.EntryAssemblyName.Contains(filter, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The process <paramref name="scan"/> is of, or null when none is selected.</summary>
    public static RunningProcess? FindProcess(ModuleScan scan)
    {
        return scan switch
        {
            ModuleScan.None => null,
            ModuleScan.Loading loading => loading.Process,
            ModuleScan.Loaded loaded => loaded.Process,
            ModuleScan.Unreachable unreachable => unreachable.Process,
            ModuleScan.Unreadable unreadable => unreadable.Process,
            _ => throw new UnreachableException(),
        };
    }

    /// <summary>The entry assembly found for the process <paramref name="scan"/> is of, or null.</summary>
    public static AssemblyPath? FindEntryAssembly(ModuleScan scan)
    {
        return scan switch
        {
            ModuleScan.None or ModuleScan.Loading => null,
            ModuleScan.Loaded loaded => loaded.EntryAssembly,
            ModuleScan.Unreachable unreachable => unreachable.EntryAssembly,
            ModuleScan.Unreadable unreadable => unreadable.EntryAssembly,
            _ => throw new UnreachableException(),
        };
    }

    /// <summary>The modules listed, or none while they aren't.</summary>
    public static IReadOnlyList<RunningModule> GetModules(ModuleScan scan)
    {
        if (scan is ModuleScan.Loaded loaded)
        {
            return loaded.Modules;
        }

        return [];
    }
}

using System.Diagnostics;
using ZGF.Gui.Immediate.Widgets;
using ILSight.Core;
using ILSight.Core.Processes;

namespace ILSight;

/// <summary>What the "open from running process" dialog does: looking for the processes, filtering them,
/// listing the selected one's modules, and what Open opens.</summary>
public static class ProcessPicking
{
    /// <summary>Opens the dialog and looks for the processes again, as they come and go.</summary>
    public static void OpenPicker(ProcessPicker picker)
    {
        picker.IsOpen = true;
        picker.FocusFilter = true;
        Refresh(picker);
    }

    /// <summary>Looks for the processes again, keeping those shown until the new list comes.</summary>
    public static void Refresh(ProcessPicker picker)
    {
        Background.Start(picker.Work, nameof(ScanAsync), ScanAsync(picker), picker.ReportError);
    }

    private static async Task ScanAsync(ProcessPicker picker)
    {
        var request = ++picker.ScanRequest;
        if (picker.Scan is not ProcessScan.Scanned)
        {
            picker.Scan = new ProcessScan.Scanning();
        }

        var find = picker.FindProcesses;
        var outcome = await Background.Wait(Task.Run(() => find(CancellationToken.None)));
        if (request != picker.ScanRequest)
        {
            return;
        }

        picker.AppliedFilter = null;
        switch (outcome)
        {
            case Outcome<IReadOnlyList<RunningProcess>>.Ok ok:
                picker.Scan = new ProcessScan.Scanned(ok.Value);
                break;
            case Outcome<IReadOnlyList<RunningProcess>>.Failed failed:
                // Listing reports a process it can't reach as a row, so a failure here is a bug.
                picker.ReportError(new AppProblem.Unexpected(failed.Error));
                picker.Scan = new ProcessScan.Failed(failed.Error.Message);
                break;
            default:
                throw new UnreachableException();
        }
    }

    /// <summary>Stops what the dialog started and forgets the process picked, once it closed.</summary>
    public static void ClosePicker(ProcessPicker picker)
    {
        picker.IsOpen = false;
        picker.ScanRequest++;
        StopModules(picker);
        picker.Modules = new ModuleScan.None();
        picker.ProcessSelection.Clear();
    }

    /// <summary>True once after <see cref="OpenPicker"/>: the filter box should take focus.</summary>
    public static bool TakeFilterFocus(ProcessPicker picker)
    {
        var focus = picker.FocusFilter;
        picker.FocusFilter = false;
        return focus;
    }

    /// <summary>Re-filters when the processes or the filter changed, and lists the modules of the process
    /// selected when that changed.</summary>
    public static void UpdatePicker(ProcessPicker picker)
    {
        ApplyFilter(picker);
        FollowSelection(picker);
    }

    private static void ApplyFilter(ProcessPicker picker)
    {
        if (picker.Scan is not ProcessScan.Scanned scanned)
        {
            return;
        }

        if (picker.Filter == picker.AppliedFilter)
        {
            return;
        }

        picker.AppliedFilter = picker.Filter;
        var selected = FindSelectedProcess(picker);
        picker.Filtered.Clear();
        var filter = picker.Filter.Trim();
        foreach (var process in scanned.Processes)
        {
            if (IsMatch(process, filter))
            {
                picker.Filtered.Add(process);
            }
        }
        var index = selected == null ? -1 : picker.Filtered.FindIndex(p => p.Id == selected.Id);
        picker.ProcessSelection.Select(index, picker.Filtered.Count, reveal: false);
    }

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

    /// <summary>The process selected among those shown, or null.</summary>
    public static RunningProcess? FindSelectedProcess(ProcessPicker picker)
    {
        var index = picker.ProcessSelection.Index;
        if (index < 0 || index >= picker.Filtered.Count)
        {
            return null;
        }

        return picker.Filtered[index];
    }

    /// <summary>Lists the modules of the process selected, once it is another than the one shown; the
    /// listing of the one before is cancelled, and its result dropped.</summary>
    private static void FollowSelection(ProcessPicker picker)
    {
        var selected = FindSelectedProcess(picker);
        var shown = FindProcess(picker.Modules);
        if (selected?.Id == shown?.Id)
        {
            return;
        }

        StopModules(picker);
        if (selected == null)
        {
            picker.Modules = new ModuleScan.None();
            return;
        }
        picker.Modules = new ModuleScan.Loading(selected);
        Background.Start(picker.Work, nameof(ScanModulesAsync), ScanModulesAsync(picker, selected, picker.ModuleRequest, picker.ModuleWork.Token), picker.ReportError);
    }

    private static void StopModules(ProcessPicker picker)
    {
        picker.ModuleRequest++;
        picker.ModuleWork.Cancel();
        picker.ModuleWork.Dispose();
        picker.ModuleWork = new CancellationTokenSource();
        picker.ModuleSelection.Clear();
    }

    private static async Task ScanModulesAsync(ProcessPicker picker, RunningProcess process, int request, CancellationToken cancellationToken)
    {
        var find = picker.FindModules;
        var outcome = await Background.Wait(Task.Run(() => ListModulesAsync(find, process, cancellationToken)));
        if (request != picker.ModuleRequest)
        {
            return;
        }

        switch (outcome)
        {
            case Outcome<ModuleScan>.Ok ok:
                picker.Modules = ok.Value;
                SelectEntryModule(picker, ok.Value);
                break;
            case Outcome<ModuleScan>.Failed failed:
                // A process that can't be asked is a result, so a failure here is a bug.
                picker.ReportError(new AppProblem.Unexpected(failed.Error));
                picker.Modules = new ModuleScan.Unreachable(process, failed.Error.Message, null);
                break;
            default:
                throw new UnreachableException();
        }
    }

    /// <summary>The modules of <paramref name="process"/> and its entry assembly, read off the UI thread, as
    /// finding the entry assembly looks for files.</summary>
    private static async Task<ModuleScan> ListModulesAsync(Func<ProcessId, CancellationToken, Task<ModuleListing>> find, RunningProcess process,
        CancellationToken cancellationToken)
    {
        var listing = await find(process.Id, cancellationToken).ConfigureAwait(false);
        return listing switch
        {
            ModuleListing.Listed listed => new ModuleScan.Loaded(process, listed.Modules, RunningProcesses.FindEntryAssembly(process, listed.Modules)),
            ModuleListing.Unreachable unreachable => new ModuleScan.Unreachable(process, unreachable.Detail, RunningProcesses.FindEntryAssembly(process, [])),
            ModuleListing.Unreadable unreadable => new ModuleScan.Unreadable(process, unreadable.Detail, RunningProcesses.FindEntryAssembly(process, [])),
            _ => throw new UnreachableException(),
        };
    }

    /// <summary>Picks out the entry assembly's module, so that Open opens it unless another is picked.</summary>
    private static void SelectEntryModule(ProcessPicker picker, ModuleScan scan)
    {
        if (scan is not ModuleScan.Loaded loaded || loaded.EntryAssembly == null)
        {
            return;
        }

        foreach (var module in loaded.Modules)
        {
            if (module is RunningModule.OnDisk onDisk && onDisk.Path.Equals(loaded.EntryAssembly))
            {
                picker.ModuleSelection.Select(module, SelectionChange.Replace, loaded.Modules);
                return;
            }
        }
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

    /// <summary>Whether Open has anything to open, worked out without building the list.</summary>
    public static bool CanOpen(ProcessPicker picker)
    {
        if (picker.ModuleSelection.Count == 0)
        {
            return FindEntryAssembly(picker.Modules) != null;
        }

        foreach (var module in GetModules(picker.Modules))
        {
            if (module is RunningModule.OnDisk && picker.ModuleSelection.Contains(module))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>What Open opens: the modules picked out that have a file, in the order shown; with none picked
    /// out, the entry assembly, which may be no module, as a single-file app's executable isn't.</summary>
    public static List<AssemblyPath> ListToOpen(ProcessPicker picker)
    {
        if (picker.ModuleSelection.Count == 0)
        {
            var entry = FindEntryAssembly(picker.Modules);
            return entry == null ? [] : [entry];
        }
        var paths = new List<AssemblyPath>();
        foreach (var module in picker.ModuleSelection.InOrder(GetModules(picker.Modules)))
        {
            if (module is RunningModule.OnDisk onDisk)
            {
                paths.Add(onDisk.Path);
            }
        }
        return paths;
    }
}

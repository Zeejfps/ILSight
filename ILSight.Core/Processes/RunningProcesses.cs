// Adapted from ILSpy's Processes/ProcessExplorer, DiagnosticsPortScanner and RunningDotNetProcess
// (MIT License, Copyright (c) 2026 Christoph Wille): see LICENSE-ILSpy.txt for the terms.

using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace ILSight.Core.Processes;

/// <summary>
/// The running .NET processes and the modules loaded in them. A CoreCLR runtime (.NET Core 3.0 on) opens a
/// diagnostics endpoint, as <c>dotnet-trace ps</c> finds them: a named pipe <c>dotnet-diagnostic-{pid}</c>
/// on Windows, a Unix domain socket <c>dotnet-diagnostic-{pid}-*-socket</c> in the temp folder elsewhere.
/// Through it the runtime says what it is, and lists what it has loaded, which no module list of the
/// operating system can see all of. Only this user's processes can be reached, and none started with
/// <c>DOTNET_EnableDiagnostics=0</c>; .NET Framework processes, which have no endpoint, aren't found.
/// </summary>
/// <remarks>The seam for <see cref="DiagnosticsIpc"/> and <see cref="NettraceRundown"/>: what they throw for a
/// process that can't be reached becomes a result here, and anything else, a bug, goes on.</remarks>
public static class RunningProcesses
{
    /// <summary>The ids of the processes that have a diagnostics endpoint, ascending. A folder or pipe list
    /// that can't be read for now gives what was read of it.</summary>
    public static IReadOnlyList<ProcessId> FindProcessIds()
    {
        var ids = new HashSet<int>();
        try
        {
            if (OperatingSystem.IsWindows())
            {
                ScanPipes(ids);
            }
            else
            {
                ScanSockets(ids);
            }
        }
        catch (IOException)
        {
            // The transport's listing failed partway: what was found is still worth listing.
            return ListFound(ids);
        }
        catch (UnauthorizedAccessException)
        {
            return ListFound(ids);
        }

        return ListFound(ids);
    }

    private static IReadOnlyList<ProcessId> ListFound(HashSet<int> ids)
    {
        return ids.Order().Select(static id => new ProcessId(id)).ToList();
    }

    /// <summary>The running .NET processes, sorted by name and then id. Their runtimes are asked
    /// together, so that one that doesn't answer holds up the list for its budget once, not for every
    /// process after it.</summary>
    public static async Task<IReadOnlyList<RunningProcess>> FindProcessesAsync(CancellationToken cancellationToken)
    {
        var queries = FindProcessIds().Select(id => DescribeAsync(id, DiagnosticsIpc.CommandBudget, cancellationToken));
        var processes = await Task.WhenAll(queries).ConfigureAwait(false);
        var found = new List<RunningProcess>();
        foreach (var process in processes)
        {
            if (process is not null)
            {
                found.Add(process);
            }
        }
        return found
            .OrderBy(static p => p.Name, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(static p => p.Id.Value)
            .ToList();
    }

    /// <summary>The process <paramref name="id"/> as its runtime describes it; or null when it has exited.</summary>
    internal static async Task<RunningProcess?> DescribeAsync(ProcessId id, TimeSpan budget, CancellationToken cancellationToken)
    {
        var name = FindProcessName(id);
        if (name is null)
        {
            return null; // exited since the scan
        }

        var endpoint = FindEndpoint(id);
        if (endpoint is null)
        {
            return null; // its socket went with it
        }

        // An endpoint that is there but doesn't answer (a suspended runtime, a stale transport, or one shutting
        // down) is still worth listing, without what only it could say.
        return await AskUnlessUnreachableAsync(
            async ct => new RunningProcess
            {
                Id = id,
                Name = name,
                Info = await DiagnosticsIpc.QueryProcessInfoAsync(endpoint, budget, ct).ConfigureAwait(false),
            },
            _ => new RunningProcess { Id = id, Name = name, Info = null }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The modules process <paramref name="id"/> has loaded, as its runtime lists them as of now.</summary>
    public static Task<ModuleListing> GetModulesAsync(ProcessId id, CancellationToken cancellationToken)
    {
        return GetModulesAsync(id, DiagnosticsIpc.RundownBudget, cancellationToken);
    }

    internal static async Task<ModuleListing> GetModulesAsync(ProcessId id, TimeSpan budget, CancellationToken cancellationToken)
    {
        var endpoint = FindEndpoint(id);
        if (endpoint is null)
        {
            return new ModuleListing.Unreachable($"Process {id} has no diagnostics endpoint.");
        }

        return await AskUnlessUnreachableAsync(
            async ct => ReadModules(await DiagnosticsIpc.CollectRundownAsync(endpoint, budget, ct).ConfigureAwait(false)),
            static e => new ModuleListing.Unreachable(e.Message), cancellationToken).ConfigureAwait(false);
    }

    private static ModuleListing ReadModules(MemoryStream trace)
    {
        using (trace)
        {
            return NettraceRundown.Read(trace).Match<ModuleListing>(
                static read => new ModuleListing.Listed(ResolveModules(read.Modules, IsFile)),
                static corrupt => new ModuleListing.Unreadable(corrupt.Detail));
        }
    }

    /// <summary>The modules a rundown reported, each once, with a file where one of its paths names a file
    /// that is there, as <paramref name="exists"/> tells of an absolute path, sorted by name.</summary>
    internal static IReadOnlyList<RunningModule> ResolveModules(IReadOnlyList<RundownModule> modules, Func<string, bool> exists)
    {
        var result = new List<RunningModule>();
        var seenPaths = new HashSet<AssemblyPath>();
        var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var module in modules)
        {
            var path = FindExistingFile(module.IlPath, exists) ?? FindExistingFile(module.NativePath, exists);
            if (path is not null)
            {
                if (seenPaths.Add(path))
                {
                    result.Add(new RunningModule.OnDisk { Name = path.FileName, Path = path });
                }

                continue;
            }

            var name = module.AssemblyName ?? FindNonBlank(module.IlPath) ?? FindNonBlank(module.NativePath) ?? "(dynamic module)";
            if (seenNames.Add(name))
            {
                result.Add(new RunningModule.InMemory { Name = name });
            }
        }

        return result.OrderBy(static m => m.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>
    /// The file of the entry assembly of <paramref name="process"/>, or null when it can't be told: the module
    /// named as the runtime names its entry assembly, else what its command line runs. That is the DLL
    /// <c>dotnet</c> is given, the DLL beside an app host, or else the executable itself, as a single-file
    /// app is, which opens as a bundle. A relative path on the command line is relative to where the process
    /// started, which isn't known, so it is passed over. Looks on disk for the files the command line names.
    /// </summary>
    public static AssemblyPath? FindEntryAssembly(RunningProcess process, IReadOnlyList<RunningModule> modules)
    {
        return FindEntryAssembly(process, modules, IsFile);
    }

    /// <summary><see cref="FindEntryAssembly(RunningProcess, IReadOnlyList{RunningModule})"/>, with
    /// <paramref name="exists"/> telling whether a file is at an absolute path.</summary>
    internal static AssemblyPath? FindEntryAssembly(RunningProcess process, IReadOnlyList<RunningModule> modules, Func<string, bool> exists)
    {
        if (process.Info is null)
        {
            return null;
        }

        var entryName = process.Info.EntryAssemblyName;
        if (entryName is not null)
        {
            foreach (var module in modules)
            {
                if (module is not RunningModule.OnDisk onDisk)
                {
                    continue;
                }

                var name = Path.GetFileNameWithoutExtension(onDisk.Name);
                if (string.Equals(name, entryName, StringComparison.OrdinalIgnoreCase))
                {
                    return onDisk.Path;
                }
            }
        }
        return FindEntryAssembly(process.Info.CommandLine, exists);
    }

    private static AssemblyPath? FindEntryAssembly(string? commandLine, Func<string, bool> exists)
    {
        var tokens = SplitCommandLine(commandLine);
        if (tokens.Count == 0)
        {
            return null;
        }

        var executable = tokens[0];

        if (IsDll(executable))
        {
            return FindExistingFile(executable, exists);
        }

        // dotnet's own options come before the app, as in "dotnet exec --depsfile App.deps.json App.dll",
        // and the app's arguments after it, so the first DLL named is the app.
        var firstDll = tokens.Skip(1).FirstOrDefault(IsDll);
        if (firstDll is not null)
        {
            var dll = FindExistingFile(firstDll, exists);
            if (dll is not null)
            {
                return dll;
            }
        }

        return FindExistingFile(Path.ChangeExtension(executable, ".dll"), exists) ?? FindExistingFile(executable, exists);
    }

    private static bool IsDll(string path)
    {
        return path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The words of a command line, split at white space outside double quotes, the quotes dropped.</summary>
    internal static IReadOnlyList<string> SplitCommandLine(string? commandLine)
    {
        var tokens = new List<string>();
        if (string.IsNullOrWhiteSpace(commandLine))
        {
            return tokens;
        }

        var token = new StringBuilder();
        var quoted = false;
        foreach (var c in commandLine)
        {
            if (c == '"')
            {
                quoted = !quoted;
            }
            else if (char.IsWhiteSpace(c) && !quoted)
            {
                if (token.Length > 0)
                {
                    tokens.Add(token.ToString());
                    token.Clear();
                }
            }
            else
            {
                token.Append(c);
            }
        }
        if (token.Length > 0)
        {
            tokens.Add(token.ToString());
        }

        return tokens;
    }

    private static AssemblyPath? FindExistingFile(string text, Func<string, bool> exists)
    {
        if (text.Length == 0)
        {
            return null;
        }

        var path = AssemblyPath.Parse(text);
        if (path is null || !exists(path.Text))
        {
            return null;
        }

        return path;
    }

    // File.Exists is false too for a file this user can't see.
    private static bool IsFile(string path)
    {
        return File.Exists(path);
    }

    private static string? FindNonBlank(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    /// <summary>What <paramref name="ask"/> answers, or what <paramref name="unreachable"/> makes of why the process
    /// didn't answer, rather than that this code is wrong. Runtimes are asked together, so this tells losing one row
    /// from losing the list. A Unix socket whose runtime is gone refuses the connection with a
    /// <see cref="System.Net.Sockets.SocketException"/>, a <see cref="Win32Exception"/> and no <see cref="IOException"/>;
    /// a named pipe with no server behind it times out.</summary>
    internal static async Task<T> AskUnlessUnreachableAsync<T>(Func<CancellationToken, Task<T>> ask, Func<Exception, T> unreachable,
        CancellationToken ct)
    {
        try
        {
            return await ask(ct).ConfigureAwait(false);
        }
        catch (IOException e)
        {
            return unreachable(e);
        }
        catch (Win32Exception e)
        {
            return unreachable(e);
        }
        catch (TimeoutException e)
        {
            return unreachable(e);
        }
        catch (UnauthorizedAccessException e)
        {
            return unreachable(e);
        }
    }

    private static string? FindProcessName(ProcessId id)
    {
        try
        {
            using var process = Process.GetProcessById(id.Value);
            return process.ProcessName;
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private static bool IsAlive(int id)
    {
        try
        {
            using var process = Process.GetProcessById(id);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
        catch (Win32Exception)
        {
            return false;
        }
    }

    /// <summary>Where process <paramref name="id"/>'s runtime listens, or null when it doesn't.</summary>
    internal static DiagnosticsEndpoint? FindEndpoint(ProcessId id)
    {
        if (OperatingSystem.IsWindows())
        {
            return new DiagnosticsEndpoint.NamedPipe(DiagnosticsIpc.TransportPrefix + id.Value);
        }

        var socket = FindSocketPath(id);
        if (socket is null)
        {
            return null;
        }

        return new DiagnosticsEndpoint.UnixSocket(socket);
    }

    private static void ScanPipes(HashSet<int> ids)
    {
        // The pipe file system lists like a folder. Listing it all and filtering beats a search pattern there, as the
        // official diagnostics client does.
        foreach (var path in Directory.GetFiles(@"\\.\pipe\"))
        {
            var name = Path.GetFileName(path);
            if (!name.StartsWith(DiagnosticsIpc.TransportPrefix, StringComparison.Ordinal))
            {
                continue;
            }

            // A pipe's name is just dotnet-diagnostic-{pid}.
            var digits = name.AsSpan(DiagnosticsIpc.TransportPrefix.Length);
            if (int.TryParse(digits, out var id))
            {
                ids.Add(id);
            }
        }
    }

    private static void ScanSockets(HashSet<int> ids)
    {
        // A runtime puts its socket in TMPDIR, else /tmp, which is what Path.GetTempPath gives.
        foreach (var path in Directory.EnumerateFiles(Path.GetTempPath(), DiagnosticsIpc.TransportPrefix + "*"))
        {
            var id = ParseSocketProcessId(Path.GetFileName(path));
            // The socket of a process that exited can linger, so a name alone doesn't prove a process is running.
            if (id is not null && IsAlive(id.Value))
            {
                ids.Add(id.Value);
            }
        }
    }

    // When a socket left by an earlier process with the same id is there too, the newest is the live runtime's.
    private static string? FindSocketPath(ProcessId id)
    {
        try
        {
            string? best = null;
            var bestTime = DateTime.MinValue;
            foreach (var path in Directory.EnumerateFiles(Path.GetTempPath(), DiagnosticsIpc.TransportPrefix + id.Value + "-*"))
            {
                var processId = ParseSocketProcessId(Path.GetFileName(path));
                if (processId != id.Value)
                {
                    continue;
                }

                var writeTime = File.GetLastWriteTimeUtc(path);
                if (best is null || writeTime > bestTime)
                {
                    best = path;
                    bestTime = writeTime;
                }
            }
            return best;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>The process id in a socket's name, <c>dotnet-diagnostic-{pid}-{disambiguation}-socket</c>;
    /// null for a name of another shape.</summary>
    internal static int? ParseSocketProcessId(string fileName)
    {
        if (!fileName.StartsWith(DiagnosticsIpc.TransportPrefix, StringComparison.Ordinal) || !fileName.EndsWith("-socket", StringComparison.Ordinal))
        {
            return null;
        }

        var rest = fileName.AsSpan(DiagnosticsIpc.TransportPrefix.Length);
        var dash = rest.IndexOf('-');
        if (dash <= 0 || !int.TryParse(rest[..dash], out var id))
        {
            return null;
        }

        return id;
    }
}

using System.Net.Sockets;
using System.Runtime.Loader;
using System.Text;
using ILSight.Core;
using ILSight.Core.Processes;

namespace ILSight.Tests;

/// <summary>Finding running .NET processes and what they have loaded, tried on this test process, whose
/// runtime has a diagnostics endpoint like any other.</summary>
public sealed class RunningProcessTests
{
    private static readonly ProcessId Self = new(Environment.ProcessId);

    private static string CoreAssemblyPath => typeof(AssemblyList).Assembly.Location;

    [Fact]
    public void A_request_has_the_documented_header()
    {
        var message = DiagnosticsIpc.EncodeRequest(0x04, 0x02, [0xAA, 0xBB]);

        Assert.Equal("DOTNET_IPC_V1\0"u8.ToArray(), message[..14]);
        Assert.Equal(22, BitConverter.ToUInt16(message, 14));
        Assert.Equal([0x04, 0x02, 0, 0, 0xAA, 0xBB], message[16..]);
    }

    [Fact]
    public void A_string_reads_back_as_written_and_one_of_no_length_reads_as_null()
    {
        using var buffer = new MemoryStream();
        using (var writer = new BinaryWriter(buffer, Encoding.Unicode, leaveOpen: true))
        {
            DiagnosticsIpc.WriteString(writer, "héllo");
            DiagnosticsIpc.WriteString(writer, null);
        }
        buffer.Position = 0;
        using var reader = new BinaryReader(buffer);

        Assert.Equal("héllo", DiagnosticsIpc.ReadString(reader));
        Assert.Null(DiagnosticsIpc.ReadString(reader));
    }

    [Theory]
    [InlineData("dotnet-diagnostic-1234-5678-socket", 1234)]
    [InlineData("dotnet-diagnostic-x-5678-socket", null)]
    [InlineData("dotnet-diagnostic-1234-5678", null)]
    [InlineData("other-1234-5678-socket", null)]
    public void A_socket_name_gives_its_process_id(string name, int? expected)
    {
        Assert.Equal(expected, RunningProcesses.ParseSocketProcessId(name));
    }

    [Fact]
    public void This_process_is_found_by_its_endpoint()
    {
        Assert.Contains(Self, RunningProcesses.FindProcessIds());
    }

    [Fact]
    public async Task This_process_is_listed_with_what_its_runtime_says_of_it()
    {
        var processes = await RunningProcesses.FindProcessesAsync(CancellationToken.None);

        var self = Assert.Single(processes, p => p.Id == Self);
        Assert.NotNull(self.Info);
        Assert.NotNull(self.Info.EntryAssemblyName);
        Assert.StartsWith($"{Environment.Version.Major}.", self.Info.RuntimeVersion);
    }

    [Fact]
    public async Task This_process_lists_the_assemblies_it_loaded_from_disk()
    {
        var listing = await RunningProcesses.GetModulesAsync(Self, CancellationToken.None);

        var modules = Assert.IsType<ModuleListing.Listed>(listing).Modules;
        var core = Assert.IsType<RunningModule.OnDisk>(Assert.Single(modules, m => m.Name == "ILSight.Core.dll"));
        Assert.Equal(AssemblyPath.Of(CoreAssemblyPath), core.Path);
        Assert.Contains(modules, m => m.Name == "System.Private.CoreLib.dll");
    }

    [Fact]
    public async Task An_assembly_loaded_from_bytes_is_listed_with_no_file()
    {
        var context = new AssemblyLoadContext("from-bytes", isCollectible: true);
        try
        {
            using (var bytes = new MemoryStream(await File.ReadAllBytesAsync(Trees.SampleAppPath)))
            {
                context.LoadFromStream(bytes);
            }

            var listing = await RunningProcesses.GetModulesAsync(Self, CancellationToken.None);

            var modules = Assert.IsType<ModuleListing.Listed>(listing).Modules;
            Assert.Contains(new RunningModule.InMemory("SampleApp"), modules);
        }
        finally
        {
            context.Unload();
        }
    }

    [Fact]
    public async Task A_rundown_reads_whole_and_not_cut_short_or_from_noise()
    {
        var endpoint = RunningProcesses.FindEndpoint(Self);
        Assert.NotNull(endpoint);
        using var trace = await DiagnosticsIpc.CollectRundownAsync(endpoint, DiagnosticsIpc.RundownBudget, CancellationToken.None);
        var bytes = trace.ToArray();

        var whole = NettraceRundown.Read(new MemoryStream(bytes));
        var cut = NettraceRundown.Read(new MemoryStream(bytes[..(bytes.Length / 2)]));
        var noise = NettraceRundown.Read(new MemoryStream(Encoding.ASCII.GetBytes("Nettrace and then nothing it expects")));
        var empty = NettraceRundown.Read(new MemoryStream());

        Assert.NotEmpty(Assert.IsType<RundownRead.Read>(whole).Modules);
        Assert.IsType<RundownRead.Corrupt>(cut);
        Assert.IsType<RundownRead.Corrupt>(noise);
        Assert.IsType<RundownRead.Corrupt>(empty);
    }

    [Fact]
    public void A_block_with_no_event_header_is_stepped_over_by_its_size()
    {
        // A sequence point block begins with a timestamp, whose first bytes, read as an event block's
        // header, would claim a header far longer than the block.
        var trace = WriteTrace(block: "SPBlock", content: [0xAC, 0x59, 0x9F, 0xAC, 0x06, 0xD1, 0x04, 0x00, 0x01, 0x00, 0x00, 0x00]);

        var read = NettraceRundown.Read(new MemoryStream(trace));

        Assert.Empty(Assert.IsType<RundownRead.Read>(read).Modules);
    }

    [Fact]
    public void A_block_that_claims_more_than_the_stream_holds_is_corrupt()
    {
        var trace = WriteTrace(block: "SPBlock", content: [0, 0, 0, 0], claimedSize: 4096);

        Assert.IsType<RundownRead.Corrupt>(NettraceRundown.Read(new MemoryStream(trace)));
    }

    [Fact]
    public void A_module_reported_twice_is_listed_once_and_one_with_no_file_by_its_assembly()
    {
        var core = AppFile("ILSight.Core.dll");
        var gone = "/nowhere/Gone.dll";

        var modules = RunningProcesses.ResolveModules(
        [
            new RundownModule(core, "", "ILSight.Core"),
            new RundownModule("", core, "ILSight.Core"),
            new RundownModule("", "", "Generated"),
            new RundownModule("", "", "Generated"),
            new RundownModule(gone, "", null),
            new RundownModule("", "", null),
        ], FilesAt(core));

        Assert.Equal(
        [
            new RunningModule.InMemory("(dynamic module)"),
            new RunningModule.InMemory(gone),
            new RunningModule.InMemory("Generated"),
            new RunningModule.OnDisk("ILSight.Core.dll", AssemblyPath.Of(core)),
        ], modules);
    }

    [Fact]
    public void The_entry_assembly_is_the_module_the_runtime_names()
    {
        var modules = new RunningModule[]
        {
            new RunningModule.InMemory("ILSight.Core"),
            new RunningModule.OnDisk("ILSight.Core.dll", AssemblyPath.Of(CoreAssemblyPath)),
        };

        Assert.Equal(AssemblyPath.Of(CoreAssemblyPath), RunningProcesses.FindEntryAssembly(CreateProcess(entryName: "ILSight.Core", commandLine: null), modules));
        Assert.Null(RunningProcesses.FindEntryAssembly(CreateProcess(entryName: "Elsewhere", commandLine: null), modules));
        Assert.Null(RunningProcesses.FindEntryAssembly(new RunningProcess(Self, "x", null), modules));
    }

    [Fact]
    public void The_entry_assembly_is_taken_from_the_command_line_when_no_module_is_named()
    {
        var dll = AppFile("App.dll");
        var host = AppFile("App");
        var bundle = AppFile("Single");
        var spaced = AppFile("With Space.dll");
        var exists = FilesAt(dll, host, bundle, spaced);

        Assert.Equal(dll, Find($"/usr/bin/dotnet exec {dll} --flag", exists));
        Assert.Equal(dll, Find(dll, exists));
        Assert.Equal(dll, Find(host, exists));
        Assert.Equal(bundle, Find(bundle, exists));
        Assert.Equal(spaced, Find($"dotnet \"{spaced}\"", exists));
        Assert.Null(Find("dotnet App.dll", exists));
        Assert.Null(Find(AppFile("Missing.dll"), exists));
        Assert.Null(Find(""));
    }

    [Fact]
    public void A_dll_named_on_the_command_line_but_not_there_gives_way_to_the_one_beside_the_host()
    {
        var host = AppFile("App");
        var dll = AppFile("App.dll");

        Assert.Equal(dll, Find($"{host} {AppFile("Plugin.dll")}", FilesAt(host, dll)));
        Assert.Equal(host, Find($"{host} {AppFile("Plugin.dll")}", FilesAt(host)));
    }

    [Fact]
    public void The_entry_assembly_beside_an_app_host_is_found_on_disk()
    {
        using var temp = new TempDir();
        var dll = temp.Write("App.dll", "");
        var host = temp.Write("App", "");

        Assert.Equal(dll, RunningProcesses.FindEntryAssembly(CreateProcess(entryName: null, $"\"{host}\" --flag"), [])?.Text);
        Assert.Null(RunningProcesses.FindEntryAssembly(CreateProcess(entryName: null, $"\"{temp.PathOf("Missing.dll")}\""), []));
    }

    [Fact]
    public void A_command_line_splits_at_spaces_outside_quotes()
    {
        Assert.Equal(["a", "b c", "d"], RunningProcesses.SplitCommandLine("  a \"b c\"\td  "));
        Assert.Empty(RunningProcesses.SplitCommandLine(null));
    }

    [Fact]
    public async Task An_endpoint_that_never_answers_runs_out_of_time()
    {
        using var temp = new TempDir();
        await using var endpoint = await SilentEndpoint.StartAsync(temp);

        await Assert.ThrowsAsync<TimeoutException>(() =>
            DiagnosticsIpc.QueryProcessInfoAsync(endpoint.Endpoint, TimeSpan.FromMilliseconds(200), CancellationToken.None));
    }

    [Fact]
    public async Task The_caller_cancelling_is_not_taken_for_a_timeout()
    {
        using var temp = new TempDir();
        await using var endpoint = await SilentEndpoint.StartAsync(temp);
        using var cancel = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            DiagnosticsIpc.QueryProcessInfoAsync(endpoint.Endpoint, TimeSpan.FromMinutes(1), cancel.Token));
    }

    [Fact]
    public void A_refused_connection_counts_as_unreachable_and_a_bug_does_not()
    {
        Assert.True(RunningProcesses.IsUnreachable(new SocketException((int)SocketError.ConnectionRefused)));
        Assert.True(RunningProcesses.IsUnreachable(new TimeoutException()));
        Assert.True(RunningProcesses.IsUnreachable(new EndOfStreamException()));
        Assert.False(RunningProcesses.IsUnreachable(new InvalidOperationException()));
        Assert.False(RunningProcesses.IsUnreachable(new OperationCanceledException()));
    }

    /// <summary>A nettrace stream holding a trace object and one block of <paramref name="block"/>'s type.</summary>
    private static byte[] WriteTrace(string block, byte[] content, int? claimedSize = null)
    {
        using var buffer = new MemoryStream();
        using (var writer = new BinaryWriter(buffer, Encoding.ASCII, leaveOpen: true))
        {
            writer.Write("Nettrace"u8);
            WriteAscii(writer, "!FastSerialization.1");
            BeginObject(writer, "Trace", version: 4);
            writer.Write(new byte[8 * sizeof(short) + 2 * sizeof(long) + 4 * sizeof(int)]);
            writer.Write((byte)6);
            BeginObject(writer, block, version: 2);
            writer.Write(claimedSize ?? content.Length);
            while (buffer.Position % 4 != 0)
            {
                writer.Write((byte)0);
            }

            writer.Write(content);
            writer.Write((byte)6);
            writer.Write((byte)1);
        }
        return buffer.ToArray();
    }

    private static void BeginObject(BinaryWriter writer, string type, int version)
    {
        writer.Write((byte)4);
        writer.Write((byte)5);
        writer.Write((byte)1);
        writer.Write(version);
        writer.Write(0);
        WriteAscii(writer, type);
        writer.Write((byte)6);
    }

    private static void WriteAscii(BinaryWriter writer, string text)
    {
        writer.Write(text.Length);
        writer.Write(Encoding.ASCII.GetBytes(text));
    }

    private static RunningProcess CreateProcess(string? entryName, string? commandLine)
    {
        return new RunningProcess(Self, "x", new ProcessInfo
        {
            CommandLine = commandLine,
            Architecture = null,
            EntryAssemblyName = entryName,
            RuntimeVersion = null,
        });
    }

    private static string? Find(string commandLine, Func<string, bool>? exists = null)
    {
        return RunningProcesses.FindEntryAssembly(CreateProcess(entryName: null, commandLine), [], exists ?? FilesAt())?.Text;
    }

    /// <summary>An absolute path to <paramref name="name"/> in a folder that need not exist.</summary>
    private static string AppFile(string name)
    {
        return Path.GetFullPath(Path.Combine(Path.GetTempPath(), "ILSightApps", name));
    }

    /// <summary>A file check that finds exactly <paramref name="paths"/>.</summary>
    private static Func<string, bool> FilesAt(params string[] paths)
    {
        var files = new HashSet<string>(paths, StringComparer.Ordinal);
        return files.Contains;
    }
}

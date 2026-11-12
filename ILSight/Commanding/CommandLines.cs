using ZGF.Gui.Immediate;
using ILSight.Core;

namespace ILSight.Commanding;

/// <summary>Reads and writes command lines. All that is known of the options and arguments the app takes
/// is here.</summary>
public static class CommandLines
{
    private const string SettingsOption = "--settings=";
    private const string OpenGlFlag = "--gl";
    private const string NewInstanceFlag = "--newinstance";
    private const string LanguageOption = "--language=";
    private const string ListOption = "--list=";
    private const string ClearListFlag = "--clear-list";
    private const string NavigateToOption = "--navigateto=";
    private const string FlagPrefix = "--";

    public static CommandLine Parse(string[] args)
    {
        var reader = new Reader();
        foreach (var arg in args)
        {
            Read(reader, arg);
        }

        return Finish(reader);
    }

    /// <summary>The command line that asks for <paramref name="arguments"/> again: the language, the list, what
    /// to show and the files, made absolute from this process's folder; and the arguments ignored, to
    /// tell of.</summary>
    public static List<string> ListArguments(RunArguments arguments)
    {
        var args = new List<string>();
        if (arguments.Language is not null)
        {
            args.Add(LanguageOption + arguments.Language.Value);
        }

        if (arguments.AssemblyList is not null)
        {
            args.Add(ListOption + arguments.AssemblyList);
        }

        if (arguments.ClearList)
        {
            args.Add(ClearListFlag);
        }

        if (arguments.Files.NavigateTo is not null)
        {
            args.Add(NavigateToOption + arguments.Files.NavigateTo);
        }

        foreach (var path in arguments.Files.Paths)
        {
            args.Add(path.Text);
        }

        args.AddRange(arguments.Ignored);
        return args;
    }

    private static CommandLine Finish(Reader reader)
    {
        var launch = new LaunchConfig(reader.SettingsPath, reader.RenderBackend, reader.NewInstance);
        var files = new FilesToOpen(reader.AssemblyPaths, reader.NavigateTo);
        var arguments = new RunArguments(reader.Language, reader.AssemblyList, reader.ClearList, files, reader.Ignored);
        return new CommandLine(launch, arguments);
    }

    private static void Read(Reader reader, string arg)
    {
        if (arg.StartsWith(SettingsOption, StringComparison.Ordinal))
        {
            ReadSettingsPath(reader, arg);
        }
        else if (arg == OpenGlFlag)
        {
            reader.RenderBackend = RenderBackendKind.OpenGl;
        }
        else if (arg == NewInstanceFlag)
        {
            reader.NewInstance = true;
        }
        else if (arg.StartsWith(LanguageOption, StringComparison.Ordinal))
        {
            ReadLanguage(reader, arg);
        }
        else if (arg.StartsWith(ListOption, StringComparison.Ordinal))
        {
            ReadAssemblyList(reader, arg);
        }
        else if (arg == ClearListFlag)
        {
            reader.ClearList = true;
        }
        else if (arg.StartsWith(NavigateToOption, StringComparison.Ordinal))
        {
            ReadNavigateTo(reader, arg);
        }
        else if (arg.StartsWith(FlagPrefix, StringComparison.Ordinal))
        {
            reader.Ignored.Add(arg);
        }
        else
        {
            AddAssemblyPath(reader, arg);
        }
    }

    /// <summary>Takes <paramref name="arg"/>'s value as the settings file, made absolute from this process's
    /// folder, so that finding the running app, reading and writing all use the same path.</summary>
    private static void ReadSettingsPath(Reader reader, string arg)
    {
        var name = ReadName(arg.Substring(SettingsOption.Length));
        if (name is null)
        {
            reader.Ignored.Add(arg);
            return;
        }
        var path = FullFilePath.Parse(Path.Combine(Environment.CurrentDirectory, name));
        if (path == null)
        {
            reader.Ignored.Add(arg);
            return;
        }
        reader.SettingsPath = path;
    }

    private static void ReadLanguage(Reader reader, string arg)
    {
        var language = ParseLanguage(arg.Substring(LanguageOption.Length));
        if (language is null)
        {
            reader.Ignored.Add(arg);
            return;
        }
        reader.Language = language;
    }

    private static void ReadAssemblyList(Reader reader, string arg)
    {
        var name = ReadName(arg.Substring(ListOption.Length));
        if (name is null)
        {
            reader.Ignored.Add(arg);
            return;
        }
        reader.AssemblyList = name;
    }

    private static void ReadNavigateTo(Reader reader, string arg)
    {
        var id = ReadName(arg.Substring(NavigateToOption.Length));
        if (id is null)
        {
            reader.Ignored.Add(arg);
            return;
        }
        reader.NavigateTo = id;
    }

    /// <summary>Takes <paramref name="arg"/> as a file, made absolute from this process's folder.</summary>
    private static void AddAssemblyPath(Reader reader, string arg)
    {
        var path = AssemblyPath.Parse(Path.Combine(Environment.CurrentDirectory, arg));
        if (path == null)
        {
            reader.Ignored.Add(arg);
            return;
        }
        reader.AssemblyPaths.Add(path);
    }

    /// <summary>The language named <paramref name="name"/>, in any case; null for one the app does not have.</summary>
    private static DecompilationLanguage? ParseLanguage(string name)
    {
        var upper = name.ToUpperInvariant();
        return upper switch
        {
            "C#" => DecompilationLanguage.CSharp,
            "CSHARP" => DecompilationLanguage.CSharp,
            "IL" => DecompilationLanguage.IL,
            "IL WITH C#" => DecompilationLanguage.ILWithCSharp,
            "ILWITHCSHARP" => DecompilationLanguage.ILWithCSharp,
            _ => null,
        };
    }

    /// <summary>The name without the spaces around it; null for a blank one.</summary>
    private static string? ReadName(string name)
    {
        var trimmed = name.Trim();
        if (trimmed.Length == 0)
        {
            return null;
        }

        return trimmed;
    }

    /// <summary>What has been read of the arguments so far.</summary>
    private sealed class Reader
    {
        public readonly List<AssemblyPath> AssemblyPaths = new();
        public readonly List<string> Ignored = new();
        public FullFilePath SettingsPath = AppSettings.DefaultPath;
        public RenderBackendKind RenderBackend = RenderBackendKind.Auto;
        public bool NewInstance;
        public DecompilationLanguage? Language;
        public string? AssemblyList;
        public bool ClearList;
        public string? NavigateTo;
    }
}

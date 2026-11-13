using ZGF.Gui.Immediate;
using ILSight.Core;
using ILSight.AppSettingArea;

namespace ILSight.CommandArea;

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
        var launch = new LaunchConfig
        {
            SettingsPath = reader.SettingsPath,
            RenderBackend = reader.RenderBackend,
            NewInstance = reader.NewInstance,
        };
        var files = new FilesToOpen { Paths = reader.AssemblyPaths, NavigateTo = reader.NavigateTo };
        var arguments = new RunArguments
        {
            Language = reader.Language,
            AssemblyList = reader.AssemblyList,
            ClearList = reader.ClearList,
            Files = files,
            Ignored = reader.Ignored,
        };
        return new CommandLine { Launch = launch, Arguments = arguments };
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

    private static void ReadSettingsPath(Reader reader, string arg)
    {
        var name = ReadName(arg.Substring(SettingsOption.Length));
        if (name is null)
        {
            reader.Ignored.Add(arg);
            return;
        }
        // Made absolute from this process's folder so that finding the running app, reading and writing all use the
        // same path.
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

    private static string? ReadName(string name)
    {
        var trimmed = name.Trim();
        if (trimmed.Length == 0)
        {
            return null;
        }

        return trimmed;
    }

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

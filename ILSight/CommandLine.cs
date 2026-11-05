using ZGF.Gui.Immediate;
using ILSight.Core;

namespace ILSight;

/// <summary>The arguments a run was started with, read: how it starts, and what it asks of the app. All
/// that is known of the options and arguments the app takes is here.</summary>
public sealed record CommandLine(LaunchConfig Launch, ExternalCommand Command)
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
            reader.Read(arg);
        return reader.ToCommandLine();
    }

    /// <summary>The arguments that ask for <paramref name="command"/> again: the language, the list, what
    /// to show and the files, made absolute from this process's folder; and the arguments ignored, to
    /// tell of.</summary>
    public static List<string> ListArguments(ExternalCommand command)
    {
        var args = new List<string>();
        if (command.Language != null)
            args.Add(LanguageOption + command.Language.Value);
        if (command.AssemblyList != null)
            args.Add(ListOption + command.AssemblyList);
        if (command.ClearList)
            args.Add(ClearListFlag);
        if (command.NavigateTo != null)
            args.Add(NavigateToOption + command.NavigateTo);
        foreach (var path in command.AssemblyPaths)
            args.Add(path.Text);
        args.AddRange(command.Ignored);
        return args;
    }

    /// <summary>What has been read of the arguments so far.</summary>
    private sealed class Reader
    {
        private readonly List<AssemblyPath> _assemblyPaths = new();
        private readonly List<string> _ignored = new();
        private string _settingsPath = AppSettings.DefaultPath;
        private RenderBackendKind _renderBackend = RenderBackendKind.Auto;
        private bool _newInstance;
        private DecompilationLanguage? _language;
        private string? _assemblyList;
        private bool _clearList;
        private string? _navigateTo;

        public CommandLine ToCommandLine()
        {
            var launch = new LaunchConfig(_settingsPath, _renderBackend, _newInstance);
            var command = new ExternalCommand(_language, _assemblyList, _clearList, _navigateTo, _assemblyPaths, _ignored);
            return new CommandLine(launch, command);
        }

        public void Read(string arg)
        {
            if (arg.StartsWith(SettingsOption, StringComparison.Ordinal))
                ReadSettingsPath(arg);
            else if (arg == OpenGlFlag)
                _renderBackend = RenderBackendKind.OpenGl;
            else if (arg == NewInstanceFlag)
                _newInstance = true;
            else if (arg.StartsWith(LanguageOption, StringComparison.Ordinal))
                ReadLanguage(arg);
            else if (arg.StartsWith(ListOption, StringComparison.Ordinal))
                ReadAssemblyList(arg);
            else if (arg == ClearListFlag)
                _clearList = true;
            else if (arg.StartsWith(NavigateToOption, StringComparison.Ordinal))
                ReadNavigateTo(arg);
            else if (arg.StartsWith(FlagPrefix, StringComparison.Ordinal))
                _ignored.Add(arg);
            else
                AddAssemblyPath(arg);
        }

        private void ReadSettingsPath(string arg)
        {
            var path = ReadName(arg.Substring(SettingsOption.Length));
            if (path == null)
            {
                _ignored.Add(arg);
                return;
            }
            _settingsPath = path;
        }

        private void ReadLanguage(string arg)
        {
            var language = ParseLanguage(arg.Substring(LanguageOption.Length));
            if (language == null)
            {
                _ignored.Add(arg);
                return;
            }
            _language = language;
        }

        private void ReadAssemblyList(string arg)
        {
            var name = ReadName(arg.Substring(ListOption.Length));
            if (name == null)
            {
                _ignored.Add(arg);
                return;
            }
            _assemblyList = name;
        }

        private void ReadNavigateTo(string arg)
        {
            var id = ReadName(arg.Substring(NavigateToOption.Length));
            if (id == null)
            {
                _ignored.Add(arg);
                return;
            }
            _navigateTo = id;
        }

        /// <summary>Takes <paramref name="arg"/> as a file, made absolute from this process's folder.</summary>
        private void AddAssemblyPath(string arg)
        {
            var path = AssemblyPath.Parse(Path.Combine(Environment.CurrentDirectory, arg));
            if (path == null)
            {
                _ignored.Add(arg);
                return;
            }
            _assemblyPaths.Add(path);
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
                return null;
            return trimmed;
        }
    }
}

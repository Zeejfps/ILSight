using System.Reflection;
using ZGF.Gui.Immediate;
using ZGF.Gui.Immediate.Widgets;
using ILSight.Core;
using ILSight.CodeViewArea;
using ILSight.ExportArea;
using ILSight.BookmarkArea;
using ILSight.ProcessArea;
using ILSight.FrameworkArea;
using ILSight.AssemblyListArea;
using ILSight.MetadataGridArea;
using ILSight.NuGetArea;
using ILSight.SearchArea;
using ILSight.MainArea;
using ILSight.AssemblyArea;
using ILSight.MetadataArea;
using ILSight.SettingArea;

namespace ILSight.App.Tests;

/// <summary>The app state never reaches the screen over it, through data or callbacks.</summary>
public sealed class ScreenIsolationTests : IDisposable
{
    private const BindingFlags InstanceFields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    private static readonly Assembly[] WalkedAssemblies =
    [
        typeof(AppState).Assembly, typeof(LoadedAssembly).Assembly, typeof(DockLayout).Assembly, typeof(Ui).Assembly, typeof(object).Assembly,
    ];

    private static readonly Type[] ScreenTypes =
    [
        typeof(UiState), typeof(TabView), typeof(PictureView), typeof(BrowseTree), typeof(AnalyzerRows), typeof(BookmarksPanel),
        typeof(ErrorDialog), typeof(AssemblyListsDialog), typeof(FrameworkPicker), typeof(NuGetPicker), typeof(ProcessPicker), typeof(AssemblyCloser), typeof(Search), typeof(ExportSetup), typeof(AssemblyPicker),
        typeof(MetadataFilters), typeof(MainLayout), typeof(CodeViewState), typeof(MetadataGridState), typeof(TableColumns), typeof(FoldedGroups),
        typeof(TreeViewState<>),
    ];

    private readonly TempDir _temp = new();

    public void Dispose()
    {
        _temp.Dispose();
    }

    [Fact]
    public async Task The_app_state_reaches_no_object_of_the_screen_over_it()
    {
        var d = AppStateDriver.Opened(_temp.Settings());
        d.Show(await d.FixtureAsync("Greeter"));
        d.Show(await d.FixtureAsync("Farewell"), inNewTab: true);
        d.Ui.Panels.Open(DockView.Bookmarks);
        d.Analyze(await d.FixtureAsync("Greeter"));
        ScreenLayout.ScopeSearch(d.Ui.Search, d.Ui.Panels, await d.NodeAsync(AppStateDriver.FixturesNamespace));
        d.Ui.BookmarksPanel.Columns.Restore([1f, 1f, 4f, 1f]);
        d.Settle();
        var write = d.AppState.SettingsSaver.Save(d.AppState);
        Assert.IsType<SettingsWriteResult.Written>(write);

        var reached = FindScreenObject(d.AppState, FindLayouts(d.Ui.Panels));

        Assert.Null(reached);
    }

    [Fact]
    public void A_walk_from_a_timer_the_app_holds_reaches_no_other_timers_target_through_the_processs_timer_queue()
    {
        var screen = new TableColumns(new TableColumn());
        using var others = new Timer(static s => GC.KeepAlive(s), screen, TimeSpan.FromHours(1), Timeout.InfiniteTimeSpan);
        using var held = new Timer(static _ => { }, null, TimeSpan.FromHours(1), Timeout.InfiniteTimeSpan);

        var reached = FindScreenObject(Tuple.Create(held), []);

        Assert.Null(reached);
    }

    // The layouts the screen's panels hold and hand the dock widget, found as any reference is: they are of the
    // framework's type, which the app state may hold copies of, so the screen's own are told by identity.
    private static List<DockLayout> FindLayouts(Panels panels)
    {
        var layouts = new List<DockLayout>();
        var seen = new HashSet<object>(ReferenceEqualityComparer.Instance);
        var queue = new Queue<(object Value, string Path)>();
        queue.Enqueue((panels, nameof(Panels)));
        while (queue.TryDequeue(out var next))
        {
            if (next.Value is MainLayout layout)
            {
                layouts.Add(layout.Dock);
            }

            var runtimeType = next.Value.GetType();
            if (!runtimeType.IsValueType && !seen.Add(next.Value))
            {
                continue;
            }

            foreach (var reference in ListReferences(next.Value, next.Path))
            {
                queue.Enqueue(reference);
            }
        }
        Assert.NotEmpty(layouts);
        return layouts;
    }

    private static string? FindScreenObject(object root, List<DockLayout> layouts)
    {
        var seen = new HashSet<object>(ReferenceEqualityComparer.Instance);
        var queue = new Queue<(object Value, string Path)>();
        queue.Enqueue((root, root.GetType().Name));
        while (queue.TryDequeue(out var next))
        {
            if (IsOfScreen(next.Value, layouts))
            {
                return next.Path;
            }

            var runtimeType = next.Value.GetType();
            if (!runtimeType.IsValueType && !seen.Add(next.Value))
            {
                continue;
            }

            foreach (var reference in ListReferences(next.Value, next.Path))
            {
                queue.Enqueue(reference);
            }
        }
        return null;
    }

    private static bool IsOfScreen(object value, List<DockLayout> layouts)
    {
        if (layouts.Exists(l => ReferenceEquals(l, value)))
        {
            return true;
        }

        var type = value.GetType();
        if (type.IsGenericType)
        {
            type = type.GetGenericTypeDefinition();
        }

        return ScreenTypes.Contains(type);
    }

    private static IEnumerable<(object Value, string Path)> ListReferences(object value, string path)
    {
        if (IsOpaque(value))
        {
            yield break;
        }

        if (value is Array array)
        {
            foreach (var element in ListElements(array, path))
            {
                yield return element;
            }

            yield break;
        }
        var assembly = value.GetType().Assembly;
        if (!WalkedAssemblies.Contains(assembly))
        {
            yield break;
        }

        for (var type = value.GetType(); type is not null; type = type.BaseType)
        {
            foreach (var field in type.GetFields(InstanceFields))
            {
                if (field.FieldType.IsPrimitive || field.FieldType.IsPointer)
                {
                    continue;
                }

                var held = field.GetValue(value);
                if (held is not null && !IsProcessWide(value, held))
                {
                    yield return (held, $"{path}.{field.Name}");
                }
            }
        }
    }

    // A link the runtime keeps from what the app holds into what the whole process shares, which isn't the app's state:
    // a timer's queue, and its neighbours in that queue, the timers of everything else running (another test's screen
    // among them, while its timer is due). Followed, a walk from the user themes' quiet timer reached whatever another
    // test had waiting at that moment, so the test failed only now and then.
    private static bool IsProcessWide(object holder, object held)
    {
        if (IsRuntimeType(held, TimerQueueType))
        {
            return true;
        }

        return IsRuntimeType(holder, TimerQueueTimerType) && IsRuntimeType(held, TimerQueueTimerType);
    }

    private const string TimerQueueType = "System.Threading.TimerQueue";
    private const string TimerQueueTimerType = "System.Threading.TimerQueueTimer";

    private static bool IsRuntimeType(object value, string fullName)
    {
        var type = value.GetType();
        return type.Assembly == typeof(object).Assembly && type.FullName == fullName;
    }

    private static IEnumerable<(object Value, string Path)> ListElements(Array array, string path)
    {
        var element = array.GetType().GetElementType() ?? throw new InvalidOperationException($"{array.GetType()} is an array with no element type.");
        if (element.IsPrimitive || element.IsEnum || element == typeof(string))
        {
            yield break;
        }

        for (var i = 0; i < array.Length; i++)
        {
            var held = array.GetValue(i);
            if (held is not null)
            {
                yield return (held, $"{path}[{i}]");
            }
        }
    }

    private static bool IsOpaque(object value)
    {
        if (value is string)
        {
            return true;
        }

        if (value is MemberInfo)
        {
            return true;
        }

        if (value is Assembly)
        {
            return true;
        }

        if (value is Module)
        {
            return true;
        }

        return value is Thread;
    }
}

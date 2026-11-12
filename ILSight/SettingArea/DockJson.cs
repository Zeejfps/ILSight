using System.Diagnostics;
using ZGF.Gui.Immediate.Widgets;

namespace ILSight.SettingArea;

/// <summary>Turns a dock layout into what a settings file holds, and back.</summary>
internal static class DockJson
{
    public static DockLayoutJson Write(DockLayout layout)
    {
        return new DockLayoutJson
        {
            Root = Write(layout.Root),
            Windows = layout.Windows.Select(static DockWindowJson? (w) => Write(w)).ToList(),
            NextId = layout.NextId,
        };
    }

    /// <summary>The layout <paramref name="json"/> holds, unless it is missing a part or repeats an id.
    /// A <see cref="DockLayout.NextId"/> at or below an id the layout uses is raised above it, as the
    /// dock space numbers new parts from it.</summary>
    public static DockLayoutRead ToLayout(DockLayoutJson json)
    {
        var layout = Parse(json);
        if (layout is null)
        {
            return new DockLayoutRead.PartsMissing();
        }

        if (layout.RepeatsAnId())
        {
            return new DockLayoutRead.IdsRepeated();
        }

        layout.NextId = Math.Max(layout.NextId, CalculateHighestId(layout) + 1);
        return new DockLayoutRead.Parsed { Layout = layout };
    }

    /// <summary>The highest id of a node or window of <paramref name="layout"/>; 0 when none has one.</summary>
    private static int CalculateHighestId(DockLayout layout)
    {
        var highest = CalculateHighestId(layout.Root);
        foreach (var window in layout.Windows)
        {
            highest = Math.Max(highest, Math.Max(window.Id, CalculateHighestId(window.Root)));
        }
        return highest;
    }

    private static int CalculateHighestId(DockNode node)
    {
        if (node is not DockSplit split)
        {
            return node.Id;
        }

        return Math.Max(node.Id, Math.Max(CalculateHighestId(split.First), CalculateHighestId(split.Second)));
    }

    /// <summary>The layout, or null when it is missing a part.</summary>
    private static DockLayout? Parse(DockLayoutJson json)
    {
        var root = Parse(json.Root);
        if (root is null || json.Windows is null)
        {
            return null;
        }

        var windows = new List<DockWindow>(json.Windows.Count);
        foreach (var window in json.Windows)
        {
            if (window is null)
            {
                return null;
            }

            var parsed = Parse(window);
            if (parsed is null)
            {
                return null;
            }

            windows.Add(parsed);
        }
        return new DockLayout { Root = root, Windows = windows, NextId = json.NextId };
    }

    private static DockNodeJson Write(DockNode node)
    {
        return node switch
        {
            DockSplit split => Write(split),
            DockGroup group => Write(group),
            _ => throw new UnreachableException(),
        };
    }

    /// <summary>The node, or null when it is held as null or is missing a part.</summary>
    private static DockNode? Parse(DockNodeJson? json)
    {
        if (json is null)
        {
            return null;
        }

        return json switch
        {
            DockSplitJson split => Parse(split),
            DockGroupJson group => Parse(group),
            _ => throw new UnreachableException(),
        };
    }

    private static DockSplitJson Write(DockSplit split)
    {
        return new DockSplitJson
        {
            Id = split.Id,
            Direction = split.Direction,
            Ratio = split.Ratio,
            First = Write(split.First),
            Second = Write(split.Second),
        };
    }

    private static DockNode? Parse(DockSplitJson json)
    {
        var first = Parse(json.First);
        if (first is null)
        {
            return null;
        }

        var second = Parse(json.Second);
        if (second is null)
        {
            return null;
        }

        return new DockSplit { Id = json.Id, Direction = json.Direction, Ratio = json.Ratio, First = first, Second = second };
    }

    private static DockGroupJson Write(DockGroup group)
    {
        return new DockGroupJson { Id = group.Id, Panels = new List<string?>(group.Panels), Active = group.Active };
    }

    private static DockNode? Parse(DockGroupJson json)
    {
        if (json.Panels is null)
        {
            return null;
        }

        var panels = json.Panels.OfType<string>().ToList();
        if (panels.Count != json.Panels.Count)
        {
            return null;
        }

        return new DockGroup { Id = json.Id, Panels = panels, Active = json.Active };
    }

    private static DockWindowJson Write(DockWindow window)
    {
        return new DockWindowJson
        {
            Id = window.Id,
            Root = Write(window.Root),
            X = window.X,
            Y = window.Y,
            Width = window.Width,
            Height = window.Height,
        };
    }

    private static DockWindow? Parse(DockWindowJson json)
    {
        var root = Parse(json.Root);
        if (root is null)
        {
            return null;
        }

        return new DockWindow { Id = json.Id, Root = root, X = json.X, Y = json.Y, Width = json.Width, Height = json.Height };
    }
}

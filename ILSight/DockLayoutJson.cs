using System.Diagnostics;
using System.Text.Json.Serialization;
using ZGF.Gui.Immediate;
using ZGF.Gui.Immediate.Widgets;

namespace ILSight;

/// <summary>A dock layout as a settings file can hold it. A part the file leaves out is the part a new
/// layout starts with; a part it holds as null, or a panel it names as null, is missing, and a layout
/// missing a part is none. The parts have setters, not init: the source-generated reader gives an init
/// property the file leaves out its type's default, not the one it starts with here.</summary>
internal sealed class DockLayoutJson
{
    public DockNodeJson? Root { get; set; } = new DockGroupJson();
    public List<DockWindowJson?>? Windows { get; set; } = new();
    public int NextId { get; set; } = 1;
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(DockSplitJson), "split")]
[JsonDerivedType(typeof(DockGroupJson), "group")]
internal abstract class DockNodeJson
{
    public int Id { get; set; }
}

internal sealed class DockSplitJson : DockNodeJson
{
    public Direction Direction { get; set; }
    public float Ratio { get; set; } = 0.5f;
    public DockNodeJson? First { get; set; } = new DockGroupJson();
    public DockNodeJson? Second { get; set; } = new DockGroupJson();
}

internal sealed class DockGroupJson : DockNodeJson
{
    public List<string?>? Panels { get; set; } = new();
    public string? Active { get; set; }
}

internal sealed class DockWindowJson
{
    public int Id { get; set; }
    public DockNodeJson? Root { get; set; } = new DockGroupJson();
    public float X { get; set; }
    public float Y { get; set; }
    public float Width { get; set; }
    public float Height { get; set; }
}

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

    /// <summary>The layout, or null when it is missing a part.</summary>
    public static DockLayout? Parse(DockLayoutJson json)
    {
        var root = Parse(json.Root);
        if (root == null || json.Windows == null)
        {
            return null;
        }

        var windows = new List<DockWindow>(json.Windows.Count);
        foreach (var window in json.Windows)
        {
            if (window == null)
            {
                return null;
            }

            var parsed = Parse(window);
            if (parsed == null)
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
        if (json == null)
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
        if (first == null)
        {
            return null;
        }

        var second = Parse(json.Second);
        if (second == null)
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
        if (json.Panels == null)
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
        if (root == null)
        {
            return null;
        }

        return new DockWindow { Id = json.Id, Root = root, X = json.X, Y = json.Y, Width = json.Width, Height = json.Height };
    }
}

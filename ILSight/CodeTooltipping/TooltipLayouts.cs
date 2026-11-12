using System.Diagnostics;
using ZGF.Gui;
using ILSight.Core;
using ILSight.Localization;

namespace ILSight.CodeTooltipping;

/// <summary>How a code tooltip's declaration and documentation are set out as rich text.</summary>
internal static class TooltipLayouts
{
    private const char KindMark = '\u0001';
    private const char NameMark = '\u0002';
    private const string Bullet = "•";
    private const string TermSeparator = " – ";

    public static TooltipLayout Build(TooltipStyles styles, Strings t, CodeTooltip tooltip)
    {
        var signature = tooltip.Signature;
        var line = signature.Line;
        var starts = signature.ParameterStarts;
        return new TooltipLayout
        {
            Declaration = CodeRuns(styles, line, 0, line.Text.Length),
            DeclarationHead = starts.Count == 0 ? [] : CodeRuns(styles, line, 0, starts[0]),
            Parameters = BuildParameters(styles, signature),
            Container = ContainerRuns(styles, t, tooltip.Container),
            Constraints = BuildConstraints(styles, signature),
            Groups = BuildGroups(styles, t, tooltip.Sections),
        };
    }

    /// <summary>The declaration cut where each parameter starts: each parameter with its comma,
    /// the last with what follows it.</summary>
    private static List<IReadOnlyList<RichTextRun>> BuildParameters(TooltipStyles styles, CodeSignature signature)
    {
        var line = signature.Line;
        var starts = signature.ParameterStarts;
        var parameters = new List<IReadOnlyList<RichTextRun>>();
        for (var i = 0; i < starts.Count; i++)
        {
            var end = i + 1 < starts.Count ? starts[i + 1] : line.Text.Length;
            parameters.Add(CodeRuns(styles, line, starts[i], end));
        }

        return parameters;
    }

    private static List<IReadOnlyList<RichTextRun>> BuildConstraints(TooltipStyles styles, CodeSignature signature)
    {
        var constraints = new List<IReadOnlyList<RichTextRun>>();
        foreach (var line in signature.Constraints)
        {
            var start = LeadingSpaces(line.Text);
            constraints.Add(CodeRuns(styles, line, start, line.Text.Length));
        }

        return constraints;
    }

    private static List<TooltipGroup> BuildGroups(TooltipStyles styles, Strings t, IReadOnlyList<DocSection> sections)
    {
        var groups = new List<TooltipGroup>();
        foreach (var section in sections)
        {
            var group = GroupFor(groups, styles, t, section.Kind);
            group.Entries.Add(EntryOf(styles, section));
        }

        return groups;
    }

    private static int LeadingSpaces(string text)
    {
        var start = 0;
        while (start < text.Length && text[start] == ' ')
        {
            start++;
        }

        return start;
    }

    /// <summary>The line's text from <paramref name="from"/> to <paramref name="to"/> as runs in
    /// its spans' colors, without the spaces it ends with.</summary>
    private static IReadOnlyList<RichTextRun> CodeRuns(TooltipStyles styles, DocumentLine line, int from, int to)
    {
        var end = TrimmedEnd(line.Text, from, to);
        var runs = new List<RichTextRun>();
        foreach (var span in line.Spans)
        {
            var start = Math.Max(span.Start, from);
            var stop = Math.Min(span.End, end);
            if (stop <= start)
            {
                continue;
            }

            var text = line.Text.Substring(start, stop - start);
            runs.Add(new RichTextRun(text, styles.Code(span.Kind), Link: LinkOf(span.Reference)));
        }
        return runs;
    }

    private static int TrimmedEnd(string text, int from, int to)
    {
        var end = to;
        while (end > from && text[end - 1] == ' ')
        {
            end--;
        }

        return end;
    }

    /// <summary>"in class Namespace.Type" or "in namespace Namespace", worded as the language words
    /// it, the keyword and name colored as code; nothing for the global namespace.</summary>
    private static IReadOnlyList<RichTextRun> ContainerRuns(TooltipStyles styles, Strings t, CodeContainer? container)
    {
        if (container is null)
        {
            return [];
        }

        var pattern = t.CodeTooltipContainer(KindMark, NameMark);
        var nameKind = container.IsNamespace ? SpanKind.Namespace : SpanKind.Type;
        var runs = new List<RichTextRun>();
        var start = 0;
        for (var i = 0; i < pattern.Length; i++)
        {
            if (pattern[i] != KindMark && pattern[i] != NameMark)
            {
                continue;
            }

            AddContainerText(runs, styles, pattern.Substring(start, i - start));
            if (pattern[i] == KindMark)
            {
                runs.Add(new RichTextRun(container.Keyword, styles.Code(SpanKind.Keyword)));
            }
            else
            {
                runs.Add(new RichTextRun(container.Name, styles.Code(nameKind)));
            }

            start = i + 1;
        }
        AddContainerText(runs, styles, pattern.Substring(start));
        return runs;
    }

    private static void AddContainerText(List<RichTextRun> runs, TooltipStyles styles, string text)
    {
        if (text.Length > 0)
        {
            runs.Add(new RichTextRun(text, styles.Container));
        }
    }

    /// <summary>The group a section of <paramref name="kind"/> joins: the last one when it is of the
    /// same kind, else a new one under the kind's heading.</summary>
    private static TooltipGroup GroupFor(List<TooltipGroup> groups, TooltipStyles styles, Strings t, DocSectionKind kind)
    {
        if (groups.Count > 0 && groups[^1].Kind == kind)
        {
            return groups[^1];
        }

        var heading = HeadingRuns(styles, t, kind);
        var group = new TooltipGroup { Kind = kind, Heading = heading, Shape = ShapeOf(kind) };
        groups.Add(group);
        return group;
    }

    private static IReadOnlyList<RichTextRun>? HeadingRuns(TooltipStyles styles, Strings t, DocSectionKind kind)
    {
        var heading = HeadingOf(t, kind);
        if (heading is null)
        {
            return null;
        }

        return [new RichTextRun(heading, styles.Heading)];
    }

    private static string? HeadingOf(Strings t, DocSectionKind kind)
    {
        return kind switch
        {
            DocSectionKind.TypeParameter => t.CodeTooltipTypeParameters,
            DocSectionKind.Parameter => t.CodeTooltipParameters,
            DocSectionKind.Returns => t.CodeTooltipReturns,
            DocSectionKind.Value => t.CodeTooltipValue,
            DocSectionKind.Exception => t.CodeTooltipExceptions,
            DocSectionKind.Remarks => t.CodeTooltipRemarks,
            DocSectionKind.Example => t.CodeTooltipExample,
            _ => null,
        };
    }

    private static EntryShape ShapeOf(DocSectionKind kind)
    {
        return kind switch
        {
            DocSectionKind.TypeParameter => EntryShape.Column,
            DocSectionKind.Parameter => EntryShape.Column,
            DocSectionKind.Exception => EntryShape.Stacked,
            _ => EntryShape.Blocks,
        };
    }

    private static TooltipEntry EntryOf(TooltipStyles styles, DocSection section)
    {
        var label = LabelRuns(styles, section);
        var blocks = new List<TooltipBlock>();
        foreach (var block in section.Blocks)
        {
            blocks.Add(BlockOf(styles, block));
        }

        return new TooltipEntry { Label = label, Blocks = blocks };
    }

    /// <summary>A parameter or type parameter by its name in code, an exception by its type.</summary>
    private static IReadOnlyList<RichTextRun>? LabelRuns(TooltipStyles styles, DocSection section)
    {
        if (section.Label is null)
        {
            return null;
        }

        var label = section.Label.Value;
        return section.Kind switch
        {
            DocSectionKind.TypeParameter => [new RichTextRun(label.Text, styles.Code(SpanKind.Type))],
            DocSectionKind.Parameter => [new RichTextRun(label.Text, styles.Code(SpanKind.Parameter))],
            _ => [Run(styles, label)],
        };
    }

    private static TooltipBlock BlockOf(TooltipStyles styles, DocBlock block)
    {
        return block.Match<TooltipBlock>(
            paragraph: paragraph => new TextBlock(Runs(styles, paragraph.Runs)),
            code: code => new CodeBlock([new RichTextRun(code.Text, styles.CodeBlock)]),
            list: list => ListOf(styles, list));
    }

    private static ListBlock ListOf(TooltipStyles styles, DocBlock.List list)
    {
        var isTable = list.Kind == DocListKind.Table;
        var rows = new List<ListRow>();
        if (list.Header is not null)
        {
            rows.Add(HeaderRow(styles, list.Header, isTable));
        }

        for (var i = 0; i < list.Items.Count; i++)
        {
            rows.Add(ItemRow(styles, list, i));
        }

        return new ListBlock { IsTable = isTable, Rows = rows };
    }

    /// <summary>A table's heading row, its term over the terms and description over the
    /// descriptions; another list's leading item, without a marker.</summary>
    private static ListRow HeaderRow(TooltipStyles styles, DocListItem header, bool isTable)
    {
        if (!isTable)
        {
            return new ListRow { Lead = [], Content = TermAndDescription(styles, header) };
        }

        var term = TextOf(header.Term);
        var description = TextOf(header.Description);
        return new ListRow { Lead = [new RichTextRun(term, styles.Heading)], Content = [new RichTextRun(description, styles.Heading)] };
    }

    private static ListRow ItemRow(TooltipStyles styles, DocBlock.List list, int index)
    {
        var item = list.Items[index];
        if (list.Kind == DocListKind.Table)
        {
            return new ListRow { Lead = Runs(styles, item.Term), Content = Runs(styles, item.Description) };
        }

        var marker = list.Kind == DocListKind.Number ? $"{index + 1}." : Bullet;
        return new ListRow { Lead = [new RichTextRun(marker, styles.Plain)], Content = TermAndDescription(styles, item) };
    }

    /// <summary>The item's term in bold and its description after a dash, or the description alone.</summary>
    private static IReadOnlyList<RichTextRun> TermAndDescription(TooltipStyles styles, DocListItem item)
    {
        var description = Runs(styles, item.Description);
        if (item.Term.Count == 0)
        {
            return description;
        }

        var runs = new List<RichTextRun> { new RichTextRun(TextOf(item.Term), styles.Bold), new RichTextRun(TermSeparator, styles.Plain) };
        runs.AddRange(description);
        return runs;
    }

    private static string TextOf(IReadOnlyList<DocRun> runs)
    {
        return string.Concat(runs.Select(static r => r.Text));
    }

    private static List<RichTextRun> Runs(TooltipStyles styles, IReadOnlyList<DocRun> runs)
    {
        var converted = new List<RichTextRun>();
        foreach (var run in runs)
        {
            converted.Add(Run(styles, run));
        }

        return converted;
    }

    /// <summary>What a name links to: the type or member a reference points at; none for a local
    /// or an assembly.</summary>
    private static CodeReference.Entity? LinkOf(CodeReference? reference)
    {
        return reference as CodeReference.Entity;
    }

    private static RichTextRun Run(TooltipStyles styles, DocRun run)
    {
        return run.Style switch
        {
            DocStyle.Bold => new RichTextRun(run.Text, styles.Bold),
            DocStyle.Italic => new RichTextRun(run.Text, styles.Italic),
            DocStyle.Code => new RichTextRun(run.Text, styles.CodeBlock, IsCode: true),
            DocStyle.Reference => new RichTextRun(run.Text, styles.Reference(GetSpanKind(run.Reference)), Link: LinkOf(run.Reference)),
            _ => new RichTextRun(run.Text, styles.Plain),
        };
    }

    private static SpanKind GetSpanKind(CodeReference? reference)
    {
        if (reference is not CodeReference.Entity entity)
        {
            return SpanKind.Type;
        }

        return entity.Kind switch
        {
            EntityKind.Type => SpanKind.Type,
            EntityKind.Method => SpanKind.Method,
            EntityKind.Field => SpanKind.Field,
            EntityKind.Property => SpanKind.Property,
            EntityKind.Event => SpanKind.Event,
            _ => throw new UnreachableException(),
        };
    }
}

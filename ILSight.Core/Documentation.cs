using System.Diagnostics;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp.OutputVisitor;
using ICSharpCode.Decompiler.CSharp.Syntax;
using ICSharpCode.Decompiler.Documentation;
using ICSharpCode.Decompiler.Output;
using ICSharpCode.Decompiler.TypeSystem;

namespace ILSight.Core;

/// <summary>An entity's XML documentation read into sections of styled runs: the summary, each
/// type parameter, parameter and exception, what it returns, and its remarks and example.
/// <c>inheritdoc</c> is followed to the documentation it names or inherits. And the <see cref="CodeTooltip"/>
/// that shows it, with the declaration of the entity.</summary>
internal static class Documentation
{
    private const ConversionFlags SignatureFlags =
        ConversionFlags.All & ~(ConversionFlags.ShowBody | ConversionFlags.PlaceReturnTypeAfterParameterList |
                                ConversionFlags.UseFullyQualifiedEntityNames | ConversionFlags.UseFullyQualifiedTypeNames);

    private const string NamespaceKeyword = "namespace";

    private static readonly CSharpAmbience TypeNames = new() { ConversionFlags = ConversionFlags.ShowTypeParameterList };
    private static readonly CSharpAmbience MemberNames = new() { ConversionFlags = ConversionFlags.ShowTypeParameterList | ConversionFlags.ShowDeclaringType };

    public static IReadOnlyList<DocSection> Read(IEntity entity)
    {
        var root = Parse(Find(entity));
        if (root is null)
        {
            return [];
        }

        var documentation = new XmlDocumentationElement(root, entity, cref => FindCref(cref, entity));
        return ReadSections(documentation);
    }

    private static string? Find(IEntity entity)
    {
        var file = entity.ParentModule?.MetadataFile;
        if (file is null)
        {
            return null;
        }

        try
        {
            return XmlDocLoader.LoadDocumentation(file)?.GetDocumentation(entity);
        }
        catch (XmlException)
        {
            return null;
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

    private static XElement? Parse(string? xml)
    {
        if (string.IsNullOrWhiteSpace(xml))
        {
            return null;
        }

        try
        {
            return XElement.Parse("<doc>" + xml + "</doc>", LoadOptions.PreserveWhitespace);
        }
        catch (XmlException)
        {
            return null;
        }
    }

    private static IEntity? FindCref(string cref, IEntity entity)
    {
        var module = entity.ParentModule;
        if (module is null)
        {
            return null;
        }

        return IdStringProvider.FindEntity(cref, new SimpleTypeResolveContext(module));
    }

    /// <summary>The sections in <see cref="DocSectionKind"/> order; text outside any section is
    /// the summary when there is none.</summary>
    private static List<DocSection> ReadSections(XmlDocumentationElement documentation)
    {
        var sections = new List<DocSection>();
        var loose = new DocText();
        foreach (var child in documentation.Children)
        {
            var section = ReadSection(child);
            if (section is not null)
            {
                sections.Add(section);
                continue;
            }

            var kind = KindOf(child);
            if (kind is null)
            {
                AddInline(loose, child);
            }
        }
        if (!loose.IsEmpty && !sections.Any(IsSummary))
        {
            sections.Add(new DocSection.Summary { Blocks = Documentation.FinishBlocks(loose) });
        }

        return sections
            .Where(HasContent)
            .OrderBy(s => s.Kind)
            .ToList();
    }

    private static DocSectionKind? KindOf(XmlDocumentationElement element)
    {
        return element.Name switch
        {
            "summary" => DocSectionKind.Summary,
            "typeparam" => DocSectionKind.TypeParameter,
            "param" => DocSectionKind.Parameter,
            "returns" => DocSectionKind.Returns,
            "value" => DocSectionKind.Value,
            "exception" => DocSectionKind.Exception,
            "remarks" => DocSectionKind.Remarks,
            "example" => DocSectionKind.Example,
            _ => null,
        };
    }

    private static DocSection? ReadSection(XmlDocumentationElement element)
    {
        var kind = KindOf(element);
        if (kind is null)
        {
            return null;
        }

        var text = new DocText();
        AddChildren(text, element);
        var blocks = Documentation.FinishBlocks(text);
        return kind.Value switch
        {
            DocSectionKind.Summary => new DocSection.Summary { Blocks = blocks },
            DocSectionKind.TypeParameter => new DocSection.TypeParameter { Blocks = blocks, Name = NameOf(element) },
            DocSectionKind.Parameter => new DocSection.Parameter { Blocks = blocks, Name = NameOf(element) },
            DocSectionKind.Returns => new DocSection.Returns { Blocks = blocks },
            DocSectionKind.Value => new DocSection.Value { Blocks = blocks },
            DocSectionKind.Exception => new DocSection.Exception { Blocks = blocks, Name = ExceptionLabel(element) },
            DocSectionKind.Remarks => new DocSection.Remarks { Blocks = blocks },
            DocSectionKind.Example => new DocSection.Example { Blocks = blocks },
            _ => throw new UnreachableException($"No documentation section of kind {kind.Value}."),
        };
    }

    /// <summary>The parameter or type parameter a <c>param</c> or <c>typeparam</c> element names, as code.</summary>
    private static DocRun? NameOf(XmlDocumentationElement element)
    {
        var name = element.GetAttribute("name");
        if (name is null)
        {
            return null;
        }

        return new DocRun { Text = name, Style = DocStyle.Code };
    }

    /// <summary>The exception an <c>exception</c> element is about, by its short name: the text
    /// inside the element describes when it is thrown.</summary>
    private static DocRun? ExceptionLabel(XmlDocumentationElement element)
    {
        var cref = element.GetAttribute("cref");
        if (cref is null)
        {
            return null;
        }

        var name = ShortName(element.ReferencedEntity, cref);
        return CrefRun(element, name);
    }

    private static bool IsSummary(DocSection section)
    {
        return section.Kind == DocSectionKind.Summary;
    }

    private static bool HasContent(DocSection section)
    {
        return section.Blocks.Count > 0 || section.Label is not null;
    }

    private static void AddChildren(DocText text, XmlDocumentationElement element)
    {
        foreach (var child in element.Children)
        {
            AddInline(text, child);
        }
    }

    private static void AddInline(DocText text, XmlDocumentationElement element)
    {
        if (element.IsTextNode)
        {
            Documentation.AddRun(text, element.TextContent, DocStyle.Plain);
            return;
        }
        switch (element.Name)
        {
            case "para":
                Documentation.BreakParagraph(text);
                AddChildren(text, element);
                Documentation.BreakParagraph(text);
                break;
            case "br":
                Documentation.BreakParagraph(text);
                break;
            case "c":
                Documentation.AddRun(text, element.TextContent, DocStyle.Code);
                break;
            case "code":
                Documentation.AddCode(text, element.TextContent);
                break;
            case "b" or "strong":
                Documentation.AddRun(text, element.TextContent, DocStyle.Bold);
                break;
            case "i" or "em":
                Documentation.AddRun(text, element.TextContent, DocStyle.Italic);
                break;
            case "paramref" or "typeparamref":
                Documentation.AddRun(text, AttributeText(element, "name"), DocStyle.Code);
                break;
            case "see" or "seealso":
                AddSee(text, element);
                break;
            case "list":
                AddList(text, element);
                break;
            default:
                AddChildren(text, element);
                break;
        }
    }

    /// <summary>A <c>see</c>: a keyword, a type or member by its short name, or a link's text.</summary>
    private static void AddSee(DocText text, XmlDocumentationElement element)
    {
        var langword = element.GetAttribute("langword");
        if (langword is not null)
        {
            Documentation.AddRun(text, langword, DocStyle.Code);
            return;
        }
        var cref = CrefRun(element, element.TextContent);
        if (cref is not null)
        {
            Documentation.AddRun(text, cref.Value.Text, cref.Value.Style, cref.Value.Reference);
            return;
        }
        var shown = element.TextContent;
        if (shown.Length == 0)
        {
            shown = AttributeText(element, "href");
        }

        Documentation.AddRun(text, shown, DocStyle.Plain);
    }

    /// <summary>The value of <paramref name="element"/>'s attribute <paramref name="name"/>, empty without one.</summary>
    private static string AttributeText(XmlDocumentationElement element, string name)
    {
        var value = element.GetAttribute(name);
        if (value is null)
        {
            return "";
        }

        return value;
    }

    /// <summary>The type or member <paramref name="element"/>'s <c>cref</c> names, shown as
    /// <paramref name="shown"/> or else by its short name; null without a <c>cref</c>.</summary>
    private static DocRun? CrefRun(XmlDocumentationElement element, string shown)
    {
        var cref = element.GetAttribute("cref");
        if (cref is null)
        {
            return null;
        }

        var entity = element.ReferencedEntity;
        var name = shown;
        if (name.Length == 0)
        {
            name = ShortName(entity, cref);
        }

        var reference = entity is not null ? CodeReferences.Find(entity) : null;
        return new DocRun { Text = name, Style = DocStyle.Reference, Reference = reference };
    }

    private static string ShortName(IEntity? entity, string cref)
    {
        if (entity is ITypeDefinition type)
        {
            return TypeNames.ConvertSymbol(type);
        }

        if (entity is not null)
        {
            return MemberNames.ConvertSymbol(entity);
        }

        return ShortNameOf(cref);
    }

    /// <summary>An id string such as <c>M:System.String.Concat(System.String)</c> as
    /// <c>String.Concat</c>: a type by its name, a member with its type's.</summary>
    private static string ShortNameOf(string cref)
    {
        var isType = cref.StartsWith("T:", StringComparison.Ordinal);
        var name = StripPrefix(cref);
        var parameters = name.IndexOf('(');
        if (parameters >= 0)
        {
            name = name.Substring(0, parameters);
        }

        var parts = name.Split('.');
        var kept = isType ? 1 : 2;
        var shown = parts.Skip(Math.Max(0, parts.Length - kept));
        return StripArity(string.Join('.', shown));
    }

    private static string StripPrefix(string cref)
    {
        if (cref.Length > 2 && cref[1] == ':')
        {
            return cref.Substring(2);
        }

        return cref;
    }

    private static string StripArity(string name)
    {
        var tick = name.IndexOf('`');
        if (tick < 0)
        {
            return name;
        }

        return name.Substring(0, tick);
    }

    private static void AddList(DocText text, XmlDocumentationElement list)
    {
        DocListItem? header = null;
        var items = new List<DocListItem>();
        foreach (var child in list.Children)
        {
            if (child.Name == "listheader")
            {
                header = ReadListItem(child);
            }
            else if (child.Name == "item")
            {
                items.Add(ReadListItem(child));
            }
        }
        Documentation.AddList(text, new DocBlock.List { Kind = ListKindOf(list), Header = header, Items = items });
    }

    private static DocListKind ListKindOf(XmlDocumentationElement list)
    {
        return list.GetAttribute("type") switch
        {
            "number" => DocListKind.Number,
            "table" => DocListKind.Table,
            _ => DocListKind.Bullet,
        };
    }

    /// <summary>An item's <c>term</c> and <c>description</c>; text outside them describes it too.</summary>
    private static DocListItem ReadListItem(XmlDocumentationElement item)
    {
        var term = new DocText();
        var description = new DocText();
        foreach (var part in item.Children)
        {
            if (part.Name == "term")
            {
                AddChildren(term, part);
            }
            else if (part.Name == "description")
            {
                AddChildren(description, part);
            }
            else
            {
                AddInline(description, part);
            }
        }
        return new DocListItem
        {
            Term = Documentation.FinishInline(term),
            Description = Documentation.FinishInline(description),
        };
    }

    /// <summary>What hovering <paramref name="entity"/> of <paramref name="assembly"/>, whose type system is
    /// <paramref name="typeSystem"/> as read, explains.</summary>
    public static CodeTooltip CreateTooltip(IEntity entity, LoadedModule assembly, DecompilerTypeSystem typeSystem)
    {
        var signature = WriteSignature(entity, assembly, typeSystem);
        var sections = Read(entity);
        return new CodeTooltip { Signature = signature, Container = FindContainer(entity), Sections = sections };
    }

    private static CodeSignature WriteSignature(IEntity entity, LoadedModule assembly, DecompilerTypeSystem typeSystem)
    {
        var settings = Settings.Create();
        var output = new DocumentBuilder { Language = DecompilationLanguage.CSharp, Assembly = assembly, TypeSystem = typeSystem };
        TokenWriter writer = new TextTokenWriter(output, settings);
        writer = TokenWriter.InsertRequiredSpaces(writer);
        writer = new HighlightingTokenWriter(writer, output);
        var parameters = new ParameterStarts(writer, output);
        var ambience = new CSharpAmbience { ConversionFlags = SignatureFlags };
        ambience.ConvertSymbol(entity, parameters, settings.CSharpFormattingOptions);
        var line = DocumentWriting.BuildDocument(output, entity.Name).Lines[0];
        var constraints = WriteConstraints(entity, assembly, typeSystem, settings);
        return new CodeSignature { Line = line, ParameterStarts = parameters.Starts, Constraints = constraints };
    }

    /// <summary>The <c>where</c> clauses of the entity's own type parameters, highlighted as code,
    /// one a line; none when it has no constraints.</summary>
    private static IReadOnlyList<DocumentLine> WriteConstraints(IEntity entity, LoadedModule assembly, DecompilerTypeSystem typeSystem, DecompilerSettings settings)
    {
        var builder = new TypeSystemAstBuilder
        {
            ShowTypeParameterConstraints = true,
            AlwaysUseShortTypeNames = true,
            AddResolveResultAnnotations = true,
        };
        var declaration = builder.ConvertEntity(entity);
        var constraints = declaration.Children.OfType<Constraint>().ToList();
        if (constraints.Count == 0)
        {
            return [];
        }

        var output = new DocumentBuilder { Language = DecompilationLanguage.CSharp, Assembly = assembly, TypeSystem = typeSystem };
        TokenWriter writer = new TextTokenWriter(output, settings);
        writer = TokenWriter.InsertRequiredSpaces(writer);
        writer = new HighlightingTokenWriter(writer, output);
        foreach (var constraint in constraints)
        {
            constraint.AcceptVisitor(new CSharpOutputVisitor(writer, settings.CSharpFormattingOptions));
            output.WriteLine();
        }
        return ListConstraintLines(DocumentWriting.BuildDocument(output, entity.Name).Lines);
    }

    /// <summary>The written clauses without the blank lines between them.</summary>
    private static List<DocumentLine> ListConstraintLines(IReadOnlyList<DocumentLine> lines)
    {
        return lines
            .Where(static l => !string.IsNullOrWhiteSpace(l.Text))
            .ToList();
    }

    private static CodeContainer? FindContainer(IEntity entity)
    {
        var declaring = entity.DeclaringTypeDefinition;
        if (declaring is not null)
        {
            return new CodeContainer
            {
                Keyword = GetKeyword(declaring),
                Name = EntityText.FullName(declaring),
                IsNamespace = false,
            };
        }

        if (entity.Namespace.Length == 0)
        {
            return null;
        }

        return new CodeContainer { Keyword = NamespaceKeyword, Name = entity.Namespace, IsNamespace = true };
    }

    private static string GetKeyword(ITypeDefinition type)
    {
        return type.Kind switch
        {
            TypeKind.Interface => "interface",
            TypeKind.Enum => "enum",
            TypeKind.Delegate => "delegate",
            TypeKind.Struct => type.IsRecord ? "record struct" : "struct",
            _ => type.IsRecord ? "record" : "class",
        };
    }

    private static void AddRun(DocText doc, string text, DocStyle style, CodeReference? reference = null)
    {
        var collapsed = CollapseWhitespace(text);
        if (EndsWithSpace(doc) && collapsed.StartsWith(' '))
        {
            collapsed = collapsed.Substring(1);
        }

        if (collapsed.Length == 0)
        {
            return;
        }

        doc.Runs.Add(new DocRun { Text = collapsed, Style = style, Reference = reference });
    }

    /// <summary>Adds <paramref name="code"/> as a block of its own, lines kept and their common
    /// indentation removed.</summary>
    private static void AddCode(DocText doc, string code)
    {
        BreakParagraph(doc);
        var lines = Dedent(code);
        if (lines.Length == 0)
        {
            return;
        }

        doc.Blocks.Add(new DocBlock.Code(lines));
    }

    private static void AddList(DocText doc, DocBlock.List list)
    {
        BreakParagraph(doc);
        if (list.Items.Count > 0 || list.Header is not null)
        {
            doc.Blocks.Add(list);
        }
    }

    /// <summary>Ends the open paragraph.</summary>
    private static void BreakParagraph(DocText doc)
    {
        TrimParagraphEnds(doc);
        if (doc.Runs.Count > 0)
        {
            doc.Blocks.Add(new DocBlock.Paragraph(doc.Runs));
        }

        doc.Runs = new List<DocRun>();
    }

    private static IReadOnlyList<DocBlock> FinishBlocks(DocText doc)
    {
        BreakParagraph(doc);
        return doc.Blocks;
    }

    /// <summary>The text read as one run of words, its paragraphs joined by spaces, as a list item
    /// shows it; code blocks and lists inside it are left out.</summary>
    private static IReadOnlyList<DocRun> FinishInline(DocText doc)
    {
        var joined = new List<DocRun>();
        foreach (var block in FinishBlocks(doc))
        {
            if (block is not DocBlock.Paragraph paragraph)
            {
                continue;
            }

            if (joined.Count > 0)
            {
                joined.Add(new DocRun { Text = " ", Style = DocStyle.Plain });
            }

            joined.AddRange(paragraph.Runs);
        }
        return joined;
    }

    private static bool EndsWithSpace(DocText doc)
    {
        if (doc.Runs.Count == 0)
        {
            return true;
        }

        return doc.Runs[^1].Text.EndsWith(' ');
    }

    private static void TrimParagraphEnds(DocText doc)
    {
        if (doc.Runs.Count == 0)
        {
            return;
        }

        var last = doc.Runs[^1];
        doc.Runs[^1] = last with { Text = last.Text.TrimEnd() };
        doc.Runs.RemoveAll(static r => r.Text.Length == 0);
    }

    private static string CollapseWhitespace(string text)
    {
        var collapsed = new StringBuilder(text.Length);
        var inSpace = false;
        foreach (var c in text)
        {
            if (char.IsWhiteSpace(c))
            {
                inSpace = true;
                continue;
            }
            if (inSpace)
            {
                collapsed.Append(' ');
            }

            inSpace = false;
            collapsed.Append(c);
        }
        if (inSpace)
        {
            collapsed.Append(' ');
        }

        return collapsed.ToString();
    }

    /// <summary>The code's lines without the blank ones around them or the indentation they
    /// share. Its first line arrives with its indentation trimmed, so only the lines after it say
    /// how deep the code sits.</summary>
    private static string Dedent(string code)
    {
        var lines = code.Replace("\r\n", "\n").Split('\n').ToList();
        while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[0]))
        {
            lines.RemoveAt(0);
        }

        while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[^1]))
        {
            lines.RemoveAt(lines.Count - 1);
        }

        var indent = CommonIndent(lines.Skip(1));
        var dedented = lines
            .Select(l => RemoveIndent(l, indent))
            .Select(l => l.TrimEnd());
        return string.Join('\n', dedented);
    }

    private static int CommonIndent(IEnumerable<string> lines)
    {
        var indent = int.MaxValue;
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var leading = line.Length - line.TrimStart().Length;
            indent = Math.Min(indent, leading);
        }
        if (indent == int.MaxValue)
        {
            return 0;
        }

        return indent;
    }

    private static string RemoveIndent(string line, int indent)
    {
        var leading = line.Length - line.TrimStart().Length;
        if (leading < indent)
        {
            return line.TrimStart();
        }

        return line.Substring(indent);
    }

    /// <summary>
    /// Documentation blocks as they are read: runs join the open paragraph with their whitespace
    /// collapsed, as XML documentation is rendered, and a paragraph is kept only when it has text.
    /// Code blocks and lists stand on their own between paragraphs.
    /// </summary>
    private sealed class DocText
    {
        public readonly List<DocBlock> Blocks = new();
        internal List<DocRun> Runs = new();

        public bool IsEmpty => Blocks.Count == 0 && Runs.Count == 0;
    }
}

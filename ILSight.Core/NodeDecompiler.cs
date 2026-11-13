using System.Diagnostics;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Text;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.CSharp.OutputVisitor;
using ICSharpCode.Decompiler.CSharp.ProjectDecompiler;
using ICSharpCode.Decompiler.CSharp.Syntax;
using ICSharpCode.Decompiler.DebugInfo;
using ICSharpCode.Decompiler.Disassembler;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.Output;
using ICSharpCode.Decompiler.TypeSystem;
using ICSharpCode.Decompiler.Util;
using ILSight.Core.Resources;

namespace ILSight.Core;

internal static class NodeDecompiler
{
    private const int MaxResourceBytes = 64 * 1024;
    private const string UsedIndent = "    ";
    private const string ReferenceAssemblyAttribute = "System.Runtime.CompilerServices.ReferenceAssemblyAttribute";
    private const string ReferenceAssemblyWarning =
        "This assembly is marked as 'reference assembly', which means that it only contains metadata and no executable code.";

    /// <summary>Writes the document <paramref name="request"/> asks for, after <paramref name="note"/>
    /// as a comment when given, once the read of its module's type system ended. Throws
    /// <see cref="OperationCanceledException"/> when cancelled; a module the library could not read, for code that
    /// reads it, and whatever the decompiler throws while it writes become a document describing it.</summary>
    public static async Task<DecompiledDocument> DecompileAsync(ModuleReading reading, DocumentRequest request, DecompilationLanguage language,
        DecompilerOptions options, CancellationToken cancellationToken, string? note = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (request is DocumentRequest.Failure failure)
        {
            return DescribeLoadError(failure, language);
        }

        var module = ModuleOf(request);
        if (module is null)
        {
            return Decompile(reading, request, null, language, options, cancellationToken, note);
        }

        var read = await LoadedAssemblies.ReadTypeSystemAsync(reading, module, cancellationToken);
        return read.Match(
            r => Decompile(reading, request, r.TypeSystem, language, options, cancellationToken, note),
            u => DecompileUnreadable(reading, request, u.Thrown.SourceException, language, options, cancellationToken, note),
            static c => throw new InvalidOperationException($"'{c.Module.FilePath}' was decompiled after its release: the unit holds a reading of the node's module, which the request's is."));
    }

    /// <summary>The document of <paramref name="request"/>, whose module the library could not read: the failure, for
    /// code that reads it; else what it writes without the module's type system.</summary>
    private static DecompiledDocument DecompileUnreadable(ModuleReading reading, DocumentRequest request, Exception why, DecompilationLanguage language, DecompilerOptions options,
        CancellationToken cancellationToken, string? note)
    {
        if (IsReadThroughTypeSystem(request))
        {
            return DescribeFailure(request, language, why);
        }

        return Decompile(reading, request, null, language, options, cancellationToken, note);
    }

    /// <summary>Whether writing <paramref name="request"/> reads its module's type system: a module's preview, a type's
    /// or member's code; not a summary, resource or BAML.</summary>
    internal static bool IsReadThroughTypeSystem(DocumentRequest request)
    {
        return request is DocumentRequest.ModulePreview or DocumentRequest.Type or DocumentRequest.Member;
    }

    /// <summary>Writes the document <paramref name="request"/> asks for with <paramref name="typeSystem"/>, its module's
    /// as read, after <paramref name="note"/> as a comment when given.</summary>
    private static DecompiledDocument Decompile(ModuleReading reading, DocumentRequest request, DecompilerTypeSystem? typeSystem, DecompilationLanguage language,
        DecompilerOptions options, CancellationToken cancellationToken, string? note)
    {
        var builder = new DocumentBuilder { Language = language, Assembly = ModuleOf(request), TypeSystem = typeSystem };
        if (note is not null)
        {
            WriteComment(builder, note);
        }

        var settings = Settings.Create(options);
        var thrown = TryWrite(reading, request, builder, language, settings, cancellationToken);
        if (thrown is not null)
        {
            return DescribeFailure(request, language, thrown);
        }

        cancellationToken.ThrowIfCancellationRequested();
        return builder.BuildDocument(request.Title);
    }

    /// <summary>Has the decompiler write what <paramref name="request"/> asks for into <paramref name="builder"/>;
    /// what it threw when it failed on the module, else null. Cancellation asked for goes through.</summary>
    private static Exception? TryWrite(ModuleReading reading, DocumentRequest request, DocumentBuilder builder, DecompilationLanguage language,
        DecompilerSettings settings, CancellationToken cancellationToken)
    {
        try
        {
            Write(reading, request, builder, language, settings, cancellationToken);
            return null;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception e)
        {
            return e;
        }
    }

    /// <summary>Writes the document <paramref name="request"/> asks for with <paramref name="typeSystem"/>, its module's
    /// as read, as <see cref="DecompileAsync"/> does, but throws what the decompiler throws.</summary>
    private static DecompiledDocument DecompileOrThrow(ModuleReading reading, DocumentRequest request, DecompilerTypeSystem? typeSystem, DecompilationLanguage language,
        DecompilerOptions options, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (request is DocumentRequest.Failure failure)
        {
            return DescribeLoadError(failure, language);
        }

        var builder = new DocumentBuilder { Language = language, Assembly = ModuleOf(request), TypeSystem = typeSystem };
        Write(reading, request, builder, language, Settings.Create(options), cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return builder.BuildDocument(request.Title);
    }

    private static void Write(ModuleReading reading, DocumentRequest request, DocumentBuilder builder, DecompilationLanguage language, DecompilerSettings settings,
        CancellationToken cancellationToken)
    {
        if (language == DecompilationLanguage.CSharp)
        {
            WriteCSharp(reading, request, builder, settings, cancellationToken);
        }
        else
        {
            WriteIL(reading, request, builder, language, settings, cancellationToken);
        }
    }

    /// <summary>The document saying the decompiler failed on what <paramref name="request"/> asks for, and
    /// how.</summary>
    private static DecompiledDocument DescribeFailure(DocumentRequest request, DecompilationLanguage language, Exception thrown)
    {
        var error = new DocumentBuilder { Language = language };
        error.Write($"// Error decompiling {request.FullName}", SpanKind.Comment);
        error.WriteLine();
        error.Write(thrown.ToString(), SpanKind.Comment);
        return error.BuildFailure(request.Title, thrown);
    }

    /// <summary>Why the file <paramref name="failure"/> names could not be read.</summary>
    private static DecompiledDocument DescribeLoadError(DocumentRequest.Failure failure, DecompilationLanguage language)
    {
        var error = failure.Assembly.Error;
        var builder = new DocumentBuilder { Language = language };
        WriteComment(builder, error.Message);
        if (error.InnerException is not null)
        {
            builder.Write(error.InnerException.ToString(), SpanKind.Comment);
        }

        return builder.BuildFailure(failure.Title, error);
    }

    /// <summary>The module the document of <paramref name="request"/> is read from; null for a package's.</summary>
    internal static LoadedModule? ModuleOf(DocumentRequest request)
    {
        return request.Match<LoadedModule?>(
            static preview => preview.Module,
            static type => type.Definition.Module,
            static member => member.Definition.Module,
            static summary => summary.Module,
            static resource => resource.Module,
            static baml => baml.Module,
            static _ => null);
    }

    /// <summary>Writes the code <paramref name="export"/> names to <paramref name="stream"/>. Throws when decompiling
    /// fails.</summary>
    internal static void WriteCode(ModuleReading reading, CodeExport export, DecompilerTypeSystem? typeSystem, DecompilationLanguage language,
        DecompilerOptions options, Stream stream, CancellationToken cancellationToken)
    {
        using var writer = new StreamWriter(stream);
        export.Switch((reading, typeSystem, language, options, writer, cancellationToken),
            static (s, whole) => WriteWholeCode(s.reading, whole, RequireTypeSystem(s.typeSystem), s.language, s.options, s.writer, s.cancellationToken),
            static (s, ns) => WriteWholeCode(s.reading, ns, RequireTypeSystem(s.typeSystem), s.language, s.options, s.writer, s.cancellationToken),
            static (s, document) =>
            {
                var written = DecompileOrThrow(s.reading, document.Request, s.typeSystem, s.language, s.options, s.cancellationToken);
                s.writer.Write(Documents.GetText(written));
            });
    }

    private static void WriteWholeCode(ModuleReading reading, CodeExport export, DecompilerTypeSystem typeSystem, DecompilationLanguage language, DecompilerOptions options,
        TextWriter writer, CancellationToken cancellationToken)
    {
        var settings = Settings.Create(options);
        var output = new PlainTextOutput(writer) { IndentationString = settings.CSharpFormattingOptions.IndentationString };
        if (language == DecompilationLanguage.CSharp)
        {
            WriteWholeCSharp(reading, export, typeSystem, output, settings, cancellationToken);
        }
        else
        {
            WriteWholeIL(reading, export, typeSystem, output, language, settings, cancellationToken);
        }
    }

    private static void WriteWholeCSharp(ModuleReading reading, CodeExport export, DecompilerTypeSystem typeSystem, ITextOutput output, DecompilerSettings settings,
        CancellationToken cancellationToken)
    {
        var syntaxTree = DecompileWhole(reading, export, typeSystem, settings, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        syntaxTree.AcceptVisitor(new InsertParenthesesVisitor { InsertParenthesesForReadability = true });
        var writer = new TextTokenWriter(output, settings);
        syntaxTree.AcceptVisitor(new CSharpOutputVisitor(writer, settings.CSharpFormattingOptions));
    }

    /// <summary>The syntax tree of the whole module or the namespace's types <paramref name="export"/> names.</summary>
    private static SyntaxTree DecompileWhole(ModuleReading reading, CodeExport export, DecompilerTypeSystem typeSystem, DecompilerSettings settings,
        CancellationToken cancellationToken)
    {
        if (export is CodeExport.WholeModule whole)
        {
            return CreateCSharpDecompiler(reading, whole.Module, typeSystem, settings, cancellationToken).DecompileWholeModuleAsSingleFile();
        }

        if (export is CodeExport.Namespace ns)
        {
            return CreateCSharpDecompiler(reading, ns.Module, typeSystem, settings, cancellationToken).DecompileTypes(TypeHandles(ns));
        }

        throw new UnreachableException();
    }

    private static void WriteWholeIL(ModuleReading reading, CodeExport export, DecompilerTypeSystem typeSystem, ITextOutput output, DecompilationLanguage language,
        DecompilerSettings settings, CancellationToken cancellationToken)
    {
        if (export is CodeExport.WholeModule whole)
        {
            var file = LoadedAssemblies.GetFile(whole.Module);
            var disassembler = CreateDisassembler(reading, whole.Module, typeSystem, output, language, settings, cancellationToken);
            disassembler.WriteAssemblyReferences(file.Metadata);
            if (file.Metadata.IsAssembly)
            {
                disassembler.WriteAssemblyHeader(file);
            }

            output.WriteLine();
            disassembler.WriteModuleHeader(file);
            output.WriteLine();
            disassembler.WriteModuleContents(file);
            return;
        }

        if (export is CodeExport.Namespace ns)
        {
            CreateDisassembler(reading, ns.Module, typeSystem, output, language, settings, cancellationToken)
                .DisassembleNamespace(ns.Name, LoadedAssemblies.GetFile(ns.Module), TypeHandles(ns));
            return;
        }

        throw new UnreachableException();
    }

    private static List<TypeDefinitionHandle> TypeHandles(CodeExport.Namespace ns)
    {
        return ns.Types
            .Select(static t => DecompilerFacts.GetTypeHandle(t))
            .ToList();
    }

    private static void WriteCSharp(ModuleReading reading, DocumentRequest request, DocumentBuilder output, DecompilerSettings settings,
        CancellationToken cancellationToken)
    {
        request.Switch((reading, output, settings, cancellationToken),
            static (s, preview) =>
            {
                WriteAssemblySummary(preview.Module, s.output);
                WriteSyntaxTree(s.reading, preview.Module, s.output, s.settings, d => d.DecompileModuleAndAssemblyAttributes(), s.cancellationToken);
            },
            static (s, type) =>
            {
                var definition = type.Definition;
                WriteHeader(definition.Module, s.output, NodeDefinitions.GetSymbol(definition), ConversionFlags.UseFullyQualifiedTypeNames | ConversionFlags.UseFullyQualifiedEntityNames);
                WriteSyntaxTree(s.reading, definition.Module, s.output, s.settings, d => d.Decompile(definition.Handle), s.cancellationToken);
            },
            static (s, member) => WriteMember(s.reading, member, s.output, s.settings, s.cancellationToken),
            static (s, summary) => WriteSummary(summary.Info, s.output, s.cancellationToken),
            static (s, resource) => WriteResource(resource, s.output),
            static (s, baml) => WriteXaml(s.reading, baml, s.output, s.cancellationToken),
            static (_, _) => throw new UnreachableException());
    }

    private static void WriteMember(ModuleReading reading, DocumentRequest.Member request, DocumentBuilder output, DecompilerSettings settings, CancellationToken cancellationToken)
    {
        var module = request.Definition.Module;
        var member = NodeDefinitions.GetSymbol(request.Definition);
        var declaring = member.DeclaringTypeDefinition;
        if (declaring is null)
        {
            WriteSyntaxTree(reading, module, output, settings, d => d.Decompile(request.Definition.Handle), cancellationToken);
            return;
        }
        WriteHeader(module, output, member.DeclaringType, ConversionFlags.UseFullyQualifiedEntityNames | ConversionFlags.UseFullyQualifiedTypeNames);
        WriteSyntaxTree(reading, module, output, settings, d => DecompileMember(d, member, declaring), cancellationToken);
    }

    private static void WriteIL(ModuleReading reading, DocumentRequest request, DocumentBuilder output, DecompilationLanguage language,
        DecompilerSettings settings, CancellationToken cancellationToken)
    {
        request.Switch((reading, output, language, settings, cancellationToken),
            static (s, preview) =>
            {
                var file = LoadedAssemblies.GetFile(preview.Module);
                var disassembler = CreateDisassembler(s.reading, preview.Module, RequireTypeSystem(s.output.TypeSystem), s.output, s.language, s.settings, s.cancellationToken);
                WriteAssemblySummary(preview.Module, s.output);
                s.output.WriteLine();
                disassembler.WriteAssemblyReferences(file.Metadata);
                if (file.Metadata.IsAssembly)
                {
                    disassembler.WriteAssemblyHeader(file);
                }

                s.output.WriteLine();
                disassembler.WriteModuleHeader(file);
            },
            static (s, type) => CreateDisassembler(s.reading, type.Definition.Module, RequireTypeSystem(s.output.TypeSystem), s.output, s.language, s.settings, s.cancellationToken)
                .DisassembleType(LoadedAssemblies.GetFile(type.Definition.Module), type.Definition.Handle),
            static (s, member) =>
            {
                var memberDisassembler = CreateDisassembler(s.reading, member.Definition.Module, RequireTypeSystem(s.output.TypeSystem), s.output, s.language, s.settings,
                    s.cancellationToken);
                DisassembleMember(memberDisassembler, LoadedAssemblies.GetFile(member.Definition.Module), member.Definition.Handle);
            },
            static (s, summary) => WriteSummary(summary.Info, s.output, s.cancellationToken),
            static (s, resource) => WriteResource(resource, s.output),
            static (s, baml) => WriteXaml(s.reading, baml, s.output, s.cancellationToken),
            static (_, _) => throw new UnreachableException());
    }

    private static void DisassembleMember(ReflectionDisassembler disassembler, MetadataFile file, EntityHandle handle)
    {
        switch (handle.Kind)
        {
            case HandleKind.MethodDefinition:
                disassembler.DisassembleMethod(file, (MethodDefinitionHandle)handle);
                break;
            case HandleKind.FieldDefinition:
                disassembler.DisassembleField(file, (FieldDefinitionHandle)handle);
                break;
            case HandleKind.PropertyDefinition:
                disassembler.DisassembleProperty(file, (PropertyDefinitionHandle)handle);
                break;
            case HandleKind.EventDefinition:
                disassembler.DisassembleEvent(file, (EventDefinitionHandle)handle);
                break;
            default:
                throw new UnreachableException();
        }
    }

    private static ReflectionDisassembler CreateDisassembler(ModuleReading reading, LoadedModule assembly, DecompilerTypeSystem typeSystem,
        ITextOutput output, DecompilationLanguage language, DecompilerSettings settings, CancellationToken cancellationToken)
    {
        var body = CreateMethodBodyDisassembler(reading, assembly, typeSystem, output, language, settings, cancellationToken);
        return new ReflectionDisassembler(output, body, cancellationToken)
        {
            DetectControlStructure = true,
            ExpandMemberDefinitions = true,
            AssemblyResolver = LoadedAssemblies.CreateResolver(reading, assembly),
            DebugInfo = DebugSymbolsFor(assembly, settings),
        };
    }

    /// <summary>What disassembles each method body: with the C# above each statement for
    /// <see cref="DecompilationLanguage.ILWithCSharp"/>, else as plain IL.</summary>
    private static MethodBodyDisassembler CreateMethodBodyDisassembler(ModuleReading reading, LoadedModule assembly, DecompilerTypeSystem typeSystem,
        ITextOutput output, DecompilationLanguage language, DecompilerSettings settings, CancellationToken cancellationToken)
    {
        if (language == DecompilationLanguage.ILWithCSharp)
        {
            return new MethodBodyWithCSharp(output, reading, assembly, typeSystem, settings, cancellationToken);
        }

        return new MethodBodyDisassembler(output, cancellationToken);
    }

    /// <summary>The comments ILSpy opens a type or member with: a warning for a reference assembly,
    /// the assembly's full name, and the type.</summary>
    private static void WriteHeader(LoadedModule assembly, DocumentBuilder output, IType type, ConversionFlags flags)
    {
        var attributes = RequireTypeSystem(output.TypeSystem).MainModule.GetAssemblyAttributes();
        if (IsReferenceAssembly(attributes))
        {
            WriteComment(output, ReferenceAssemblyWarning);
        }

        WriteComment(output, assembly.FullName);
        var ambience = new CSharpAmbience { ConversionFlags = ConversionFlags.ShowTypeParameterList | ConversionFlags.PlaceReturnTypeAfterParameterList | flags };
        WriteComment(output, type is ITypeDefinition definition ? ambience.ConvertSymbol(definition) : ambience.ConvertType(type));
    }

    /// <summary>Whether <paramref name="attributes"/>, an assembly's, mark it a reference assembly.</summary>
    private static bool IsReferenceAssembly(IEnumerable<IAttribute> attributes)
    {
        return attributes.Any(static a => a.AttributeType.FullName == ReferenceAssemblyAttribute);
    }

    /// <summary>Decompiles <paramref name="member"/> as ILSpy shows it on its own: a constructor of a
    /// class or a field that is not constant with the members whose initializers it shares.</summary>
    private static SyntaxTree DecompileMember(CSharpDecompiler decompiler, IMember member, ITypeDefinition declaring)
    {
        var module = decompiler.TypeSystem.MainModule;
        member = DecompilerFacts.ResolveMember(module, member.MetadataToken);
        declaring = module.GetDefinition(DecompilerFacts.GetTypeHandle(declaring));
        switch (member)
        {
            case IMethod { IsConstructor: true } ctor when ctor.DeclaringType.IsReferenceType != false:
                decompiler.AstTransforms.Add(new ConstructorSelection { Kept = ctor });
                return decompiler.Decompile(MemberSelection.FieldsAndConstructors(declaring, ctor.IsStatic));
            case IField { IsConst: false } field:
                decompiler.AstTransforms.Add(new FieldSelection { Kept = field });
                return decompiler.Decompile(MemberSelection.FieldsAndConstructors(declaring, field.IsStatic));
            default:
                return decompiler.Decompile(member.MetadataToken);
        }
    }

    private static void WriteSyntaxTree(ModuleReading reading, LoadedModule assembly, DocumentBuilder output, DecompilerSettings settings,
        Func<CSharpDecompiler, SyntaxTree> decompile, CancellationToken cancellationToken)
    {
        var decompiler = CreateCSharpDecompiler(reading, assembly, RequireTypeSystem(output.TypeSystem), settings, cancellationToken);
        var syntaxTree = decompile(decompiler);
        cancellationToken.ThrowIfCancellationRequested();
        output.IndentAs(settings.CSharpFormattingOptions);
        var writer = CreateHighlightingWriter(output, settings);
        WriteSyntaxTree(syntaxTree, writer, settings);
    }

    /// <summary>Writes <paramref name="syntaxTree"/> to <paramref name="output"/> as the C# view shows
    /// it, recording in the tree where each node landed, for <see cref="CSharpDecompiler.CreateSequencePoints"/>.</summary>
    internal static void WriteLocatedSyntaxTree(SyntaxTree syntaxTree, DocumentBuilder output, DecompilerSettings settings)
    {
        output.IndentAs(settings.CSharpFormattingOptions);
        var highlighting = CreateHighlightingWriter(output, settings);
        var located = new LocatingTokenWriter(highlighting, output);
        var writer = TokenWriter.WrapInWriterThatSetsLocationsInAST(located);
        WriteSyntaxTree(syntaxTree, writer, settings);
    }

    private static TokenWriter CreateHighlightingWriter(DocumentBuilder output, DecompilerSettings settings)
    {
        var writer = new TextTokenWriter(output, settings);
        return new HighlightingTokenWriter(writer, output);
    }

    private static void WriteSyntaxTree(SyntaxTree syntaxTree, TokenWriter writer, DecompilerSettings settings)
    {
        syntaxTree.AcceptVisitor(new InsertParenthesesVisitor { InsertParenthesesForReadability = true });
        syntaxTree.AcceptVisitor(new CSharpOutputVisitor(writer, settings.CSharpFormattingOptions));
    }

    /// <summary>The C# decompiler of <paramref name="assembly"/> with <paramref name="settings"/>, over
    /// <paramref name="typeSystem"/>, the assembly's own as read, or the one the settings choose, read in the unit of
    /// <paramref name="reading"/>.</summary>
    internal static CSharpDecompiler CreateCSharpDecompiler(ModuleReading reading, LoadedModule assembly, DecompilerTypeSystem typeSystem,
        DecompilerSettings settings, CancellationToken cancellationToken)
    {
        return new CSharpDecompiler(LoadedAssemblies.ChooseTypeSystemFor(reading, assembly, typeSystem, settings), settings)
        {
            CancellationToken = cancellationToken,
            DebugInfoProvider = DebugSymbolsFor(assembly, settings),
        };
    }

    /// <summary><paramref name="typeSystem"/>, which the unit writing awaited and handed down before writing code that
    /// reads it; throws when it did not, a bug.</summary>
    internal static DecompilerTypeSystem RequireTypeSystem(DecompilerTypeSystem? typeSystem)
    {
        return typeSystem ?? throw new InvalidOperationException("Code that reads a module's type system was written without the read its unit awaited.");
    }

    internal static IDebugInfoProvider? DebugSymbolsFor(LoadedModule assembly, DecompilerSettings settings)
    {
        if (!settings.UseDebugSymbols)
        {
            return null;
        }

        return LoadedAssemblies.EnsureDebugSymbols(assembly);
    }

    private static void WriteAssemblySummary(LoadedModule assembly, DocumentBuilder output)
    {
        var file = LoadedAssemblies.GetFile(assembly);
        WriteComment(output, assembly.FilePath.Text);
        WriteComment(output, assembly.FullName);
        var corHeader = file.CorHeader;
        if (file.Metadata.IsAssembly && corHeader is not null)
        {
            var entryPoint = corHeader.EntryPointTokenOrRelativeVirtualAddress;
            if (entryPoint != 0 && (corHeader.Flags & CorFlags.NativeEntryPoint) == 0)
            {
                WriteEntryPoint(assembly, MetadataTokens.EntityHandle(entryPoint), output);
            }
        }
        var pe = LoadedAssemblies.FindPEFile(assembly);
        if (pe is not null)
        {
            WriteComment(output, $"Architecture: {ArchitectureOf(pe.Reader.PEHeaders)}");
        }

        if (assembly.TargetFramework is not null)
        {
            WriteComment(output, $"Target framework: {assembly.TargetFramework.Text}");
        }

        output.WriteLine();
    }

    private static void WriteEntryPoint(LoadedModule assembly, EntityHandle handle, DocumentBuilder output)
    {
        if (handle.Kind != HandleKind.MethodDefinition)
        {
            return;
        }

        var method = RequireTypeSystem(output.TypeSystem).MainModule.ResolveEntity(handle);
        if (method is null)
        {
            return;
        }

        var reference = CodeReferences.Find(method);
        if (reference is null)
        {
            return;
        }

        output.Write("// Entry point: ", SpanKind.Comment);
        output.Write(EntityText.FullName(method), SpanKind.Method, reference);
        output.WriteLine();
    }

    private static string ArchitectureOf(PEHeaders headers)
    {
        var machine = headers.CoffHeader.Machine;
        var flags = headers.CorHeader?.Flags ?? 0;
        if (machine != Machine.I386 || (flags & CorFlags.ILOnly) == 0)
        {
            return machine.ToString();
        }

        if ((flags & CorFlags.Requires32Bit) == 0)
        {
            return "AnyCPU (64-bit preferred)";
        }

        return (flags & CorFlags.Prefers32Bit) != 0 ? "AnyCPU (32-bit preferred)" : "x86";
    }

    private static void WriteSummary(SummaryInfo summary, DocumentBuilder output, CancellationToken cancellationToken)
    {
        summary.Switch((output, cancellationToken),
            static (s, comments) =>
            {
                foreach (var line in comments.Lines)
                {
                    s.cancellationToken.ThrowIfCancellationRequested();
                    if (line.Length == 0)
                    {
                        s.output.WriteLine();
                    }
                    else
                    {
                        WriteComment(s.output, line);
                    }
                }
            },
            static (s, ns) =>
            {
                s.output.Write("namespace", SpanKind.Keyword);
                s.output.Write(" ");
                s.output.Write(ns.Name, SpanKind.Namespace);
                s.output.WriteLine();
                s.output.WriteLine();
                foreach (var entry in ns.Entries)
                {
                    s.cancellationToken.ThrowIfCancellationRequested();
                    WriteNamespaceEntry(s.output, entry);
                }
            },
            static (s, forwarded) =>
            {
                WriteComment(s.output, ForwardedToText(forwarded.Destination));
                s.output.Write(forwarded.FullName, SpanKind.Type, forwarded.Target);
                s.output.WriteLine();
            },
            static (s, references) =>
            {
                foreach (var assembly in references.Assemblies)
                {
                    WriteReferenceLine(s.output, assembly);
                }

                foreach (var module in references.Modules)
                {
                    WriteComment(s.output, module);
                }
            },
            static (s, module) =>
            {
                WriteComment(s.output, module.Name);
                WriteComment(s.output, DescribeModule(module.ContainsMetadata));
                WriteImports(s.output, module.Imports);
            },
            static (s, import) =>
            {
                WriteLink(s.output, import.Method, SpanKind.Method);
                WriteComment(s.output, $"Calls {import.EntryPoint} in {import.ModuleName}");
            },
            static (s, reference) =>
            {
                WriteComment(s.output, reference.Reference.FullName);
                var path = LoadedAssemblies.GetResolver(reference.Module).FindAssemblyFile(reference.Reference);
                WriteComment(s.output, path is null ? "Not found" : $"Resolves to {path}");
            },
            static (s, used) => WriteUsed(s.output, used.Entries, "", s.cancellationToken),
            static (s, types) =>
            {
                foreach (var type in types.Entries)
                {
                    WriteLink(s.output, type, SpanKind.Type);
                }
            },
            static (s, error) =>
            {
                foreach (var line in error.Exception.ToString().Split('\n'))
                {
                    WriteComment(s.output, line.TrimEnd('\r'));
                }
            });
    }

    private static void WriteNamespaceEntry(DocumentBuilder output, NamespaceEntry entry)
    {
        entry.Switch(output,
            static (o, declared) => WriteTypeDeclaration(o, declared),
            static (o, forwarded) => WriteForwardedType(o, forwarded.Line));
    }

    /// <summary>A line for each type and member <paramref name="entries"/> lists as used from a reference,
    /// going to where it is defined, with what is used of a type indented under it.</summary>
    private static void WriteUsed(DocumentBuilder output, IReadOnlyList<UsedEntry> entries, string indent, CancellationToken cancellationToken)
    {
        foreach (var entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            output.Write(indent);
            entry.Switch((output, indent, cancellationToken),
                static (s, type) =>
                {
                    s.output.Write(type.FullName, SpanKind.Type, type.Target);
                    s.output.WriteLine();
                    WriteUsed(s.output, type.Used, s.indent + UsedIndent, s.cancellationToken);
                },
                static (s, member) =>
                {
                    s.output.Write(member.Label, member.Kind, member.Target);
                    s.output.WriteLine();
                },
                static (s, forwarded) => WriteForwardedType(s.output, forwarded.Line));
        }
    }

    /// <summary>A line naming the forwarded type, which goes to where it is defined, and where it is forwarded to.</summary>
    private static void WriteForwardedType(DocumentBuilder output, ForwardedLine line)
    {
        output.Write(line.Label, SpanKind.Type, line.Target);
        output.Write(" // " + ForwardedToText(line.Destination), SpanKind.Comment);
        output.WriteLine();
    }

    private static string ForwardedToText(string? destination)
    {
        return $"Forwarded to {destination}";
    }

    private static void WriteTypeDeclaration(DocumentBuilder output, NamespaceEntry.Declared type)
    {
        var declaration = EntityText.Declaration(type.Type);
        var label = type.Label;
        var nameAt = declaration.LastIndexOf(label, StringComparison.Ordinal);
        if (nameAt < 0)
        {
            output.Write(declaration, SpanKind.Plain, type.Reference);
        }
        else
        {
            output.Write(declaration[..nameAt], SpanKind.Keyword);
            output.Write(label, SpanKind.Type, type.Reference);
            output.Write(declaration[(nameAt + label.Length)..], SpanKind.Plain);
        }
        output.WriteLine();
    }

    /// <summary>WPF's compiled XAML as the XAML it was compiled from, colored as markup.</summary>
    private static void WriteXaml(ModuleReading reading, DocumentRequest.Baml baml, DocumentBuilder output, CancellationToken cancellationToken)
    {
        var xaml = Baml.DecompileToText(reading, baml.Module, baml.Data, cancellationToken);
        ResourceText.Write(output, xaml, ResourceSyntax.Xml);
        output.WriteLine();
    }

    /// <summary>A text resource as its text, colored as XML or JSON when it is that, as ILSpy shows
    /// it, and indented when written on one line; anything else as a description and a hex dump of
    /// its first bytes.</summary>
    private static void WriteResource(DocumentRequest.Resource resource, DocumentBuilder output)
    {
        var data = resource.Read();
        var detected = ResourceText.Detect(resource.Title, data);
        if (detected is not null)
        {
            var (text, syntax) = detected.Value;
            var shown = ResourceText.IndentOneLine(text, syntax);
            ResourceText.Write(output, shown, syntax);
            output.WriteLine();
            return;
        }
        WriteComment(output, resource.Description);
        output.WriteLine();
        WriteHexDump(data.AsSpan(0, Math.Min(data.Length, MaxResourceBytes)), output);
        if (data.Length > MaxResourceBytes)
        {
            WriteComment(output, $"Truncated after {MaxResourceBytes} bytes.");
        }
    }

    private static void WriteHexDump(ReadOnlySpan<byte> bytes, DocumentBuilder output)
    {
        var line = new StringBuilder();
        for (var offset = 0; offset < bytes.Length; offset += 16)
        {
            var row = bytes.Slice(offset, Math.Min(16, bytes.Length - offset));
            output.Write(offset.ToString("X8"), SpanKind.ILLabel);
            output.Write("  ");
            line.Clear();
            for (var i = 0; i < 16; i++)
            {
                line.Append(i < row.Length ? row[i].ToString("X2") + " " : "   ");
            }

            output.Write(line.ToString(), SpanKind.Number);
            line.Clear();
            foreach (var b in row)
            {
                line.Append(b is >= 0x20 and < 0x7F ? (char)b : '.');
            }

            output.Write(" " + line, SpanKind.String);
            output.WriteLine();
        }
    }

    /// <summary>An assembly reference's full name, which goes to the assembly, after a comment's marker.</summary>
    private static void WriteReferenceLine(DocumentBuilder output, SummaryLink reference)
    {
        output.Write("// ", SpanKind.Comment);
        output.Write(reference.Text, SpanKind.Plain, reference.Target);
        output.WriteLine();
    }

    private static string DescribeModule(bool containsMetadata)
    {
        if (containsMetadata)
        {
            return "A module of this assembly, holding metadata";
        }

        return "A native library, or a module holding no metadata";
    }

    /// <summary>The methods the assembly imports from a module, after a blank line, a line each.</summary>
    private static void WriteImports(DocumentBuilder output, IReadOnlyList<SummaryLink> imports)
    {
        if (imports.Count == 0)
        {
            return;
        }

        output.WriteLine();
        foreach (var import in imports)
        {
            WriteLink(output, import, SpanKind.Method);
        }
    }

    private static void WriteLink(DocumentBuilder output, SummaryLink link, SpanKind kind)
    {
        output.Write(link.Text, kind, link.Target);
        output.WriteLine();
    }

    private static void WriteComment(DocumentBuilder output, string text)
    {
        output.Write($"// {text}", SpanKind.Comment);
        output.WriteLine();
    }

    /// <summary>Passes C# on to the writers it decorates, telling the decompiler where in
    /// <see cref="DocumentBuilder"/> each token lands, so that it can record where each node of the syntax
    /// tree was written.</summary>
    private sealed class LocatingTokenWriter(TokenWriter decorated, DocumentBuilder builder) : DecoratingTokenWriter(decorated), ILocatable
    {
        public TextLocation Location => builder.Location;

        public int Length => 0;
    }
}

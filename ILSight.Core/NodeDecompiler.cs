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
using ILSight.Core.ResourceArea;

namespace ILSight.Core;

internal static class NodeDecompiler
{
    private const int MaxResourceBytes = 64 * 1024;
    private const string UsedIndent = "    ";
    private const string ReferenceAssemblyAttribute = "System.Runtime.CompilerServices.ReferenceAssemblyAttribute";

    /// <summary>Writes the document <paramref name="request"/> asks for, after <paramref name="note"/>
    /// on a line of its own when given, once the read of its module's type system ended, the app's notes beside the code in
    /// <paramref name="notes"/>' words. Throws
    /// <see cref="OperationCanceledException"/> when cancelled; a module the library could not read, for code that
    /// reads it, and whatever the decompiler throws while it writes become a document describing it.</summary>
    public static async Task<DecompiledDocument> DecompileAsync(ModuleReadLease reading, DocumentRequest request, DecompilationLanguage language,
        DecompilerOptions options, IDocumentNotes notes, CancellationToken cancellationToken, DocumentNote? note = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (request is DocumentRequest.Failure failure)
        {
            return DescribeLoadError(failure, language, notes);
        }

        var module = ModuleOf(request);
        if (module is null)
        {
            return Decompile(reading, request, null, null, language, options, notes, cancellationToken, note);
        }

        var read = await LoadedAssemblies.ReadTypeSystemAsync(reading, module, cancellationToken);
        if (read is not TypeSystemRead.Read own || !WritesCSharp(request, language))
        {
            return read switch
            {
                TypeSystemRead.Read r => Decompile(reading, request, r.TypeSystem, null, language, options, notes, cancellationToken, note),
                TypeSystemRead.Unreadable u => DecompileUnreadable(reading, request, u.Thrown.SourceException, language, options, notes, cancellationToken, note),
                TypeSystemRead.Closed c => throw ReadAfterRelease(c),
            };
        }

        // The type system the decompiler reads with these settings, a shared read of its own, awaited and matched
        // before the write's read starts: what the library failed on reading it is the node's failure, said as a
        // decompiler's would be; a bug of its resolver's has gone on from the await.
        var chosen = await LoadedAssemblies.ReadChosenTypeSystemAsync(reading, module, Settings.Create(options), cancellationToken);
        return chosen switch
        {
            TypeSystemRead.Read r => Decompile(reading, request, own.TypeSystem, r.TypeSystem, language, options, notes, cancellationToken, note),
            TypeSystemRead.Unreadable u => DescribeFailure(request, language, notes, u.Thrown.SourceException),
            TypeSystemRead.Closed c => throw ReadAfterRelease(c),
        };
    }

    private static InvalidOperationException ReadAfterRelease(TypeSystemRead.Closed closed)
    {
        return new InvalidOperationException(
            $"'{closed.Module.FilePath}' was decompiled after its release: the unit holds a reading of the node's module, which the request's is.");
    }

    /// <summary>Whether writing <paramref name="request"/> in <paramref name="language"/> runs the C# decompiler: code read
    /// through the type system, as C# or as IL with the C# of each method above it.</summary>
    internal static bool WritesCSharp(DocumentRequest request, DecompilationLanguage language)
    {
        return IsReadThroughTypeSystem(request) && language != DecompilationLanguage.IL;
    }

    /// <summary>Whether writing <paramref name="export"/> in <paramref name="language"/> runs the C# decompiler.</summary>
    internal static bool WritesCSharp(CodeExport export, DecompilationLanguage language)
    {
        if (language == DecompilationLanguage.IL)
        {
            return false;
        }

        return export is not CodeExport.Document document || IsReadThroughTypeSystem(document.Request);
    }

    private static DecompiledDocument DecompileUnreadable(ModuleReadLease reading, DocumentRequest request, Exception why, DecompilationLanguage language, DecompilerOptions options,
        IDocumentNotes notes, CancellationToken cancellationToken, DocumentNote? note)
    {
        if (IsReadThroughTypeSystem(request))
        {
            return DescribeFailure(request, language, notes, why);
        }

        return Decompile(reading, request, null, null, language, options, notes, cancellationToken, note);
    }

    /// <summary>Whether writing <paramref name="request"/> reads its module's type system: a module's preview, a type's
    /// or member's code; not a summary, resource or BAML.</summary>
    internal static bool IsReadThroughTypeSystem(DocumentRequest request)
    {
        return request is DocumentRequest.ModulePreview or DocumentRequest.Type or DocumentRequest.Member;
    }

    private static DecompiledDocument Decompile(ModuleReadLease reading, DocumentRequest request, DecompilerTypeSystem? typeSystem,
        DecompilerTypeSystem? chosenTypeSystem, DecompilationLanguage language, DecompilerOptions options, IDocumentNotes notes, CancellationToken cancellationToken,
        DocumentNote? note)
    {
        var builder = new DocumentBuilder { Language = language, Assembly = ModuleOf(request), TypeSystem = typeSystem, ChosenTypeSystem = chosenTypeSystem };
        if (note is not null)
        {
            WriteNote(builder, note, notes);
        }

        WriteUnreadReferences(builder, request, typeSystem, notes);

        var settings = Settings.Create(options);
        var thrown = TryWrite(reading, request, builder, language, settings, notes, cancellationToken);
        if (thrown is not null)
        {
            return DescribeFailure(request, language, notes, thrown);
        }

        cancellationToken.ThrowIfCancellationRequested();
        return builder.BuildDocument(request.Title);
    }

    private static Exception? TryWrite(ModuleReadLease reading, DocumentRequest request, DocumentBuilder builder, DecompilationLanguage language,
        DecompilerSettings settings, IDocumentNotes notes, CancellationToken cancellationToken)
    {
        try
        {
            Write(reading, request, builder, language, settings, notes, cancellationToken);
            return null;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        // The app's code the decompiler called back (the builder, its writers, a method's C#, a transform, the resolver)
        // threw: a bug, which goes on to the edge rather than reading as the decompiler's failure on the node (CD4).
        catch (Exception e) when (!builder.Failed)
        {
            return e;
        }
    }

    private static DecompiledDocument DecompileOrThrow(ModuleReadLease reading, DocumentRequest request, DecompilerTypeSystem? typeSystem,
        DecompilerTypeSystem? chosenTypeSystem, DecompilationLanguage language, DecompilerOptions options, IDocumentNotes notes, CallbackFailure callbacks,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (request is DocumentRequest.Failure failure)
        {
            return DescribeLoadError(failure, language, notes);
        }

        var builder = new DocumentBuilder
        {
            Language = language, Assembly = ModuleOf(request), TypeSystem = typeSystem, ChosenTypeSystem = chosenTypeSystem, Callbacks = callbacks,
        };
        WriteUnreadReferences(builder, request, typeSystem, notes);
        Write(reading, request, builder, language, Settings.Create(options), notes, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return builder.BuildDocument(request.Title);
    }

    private static void Write(ModuleReadLease reading, DocumentRequest request, DocumentBuilder builder, DecompilationLanguage language, DecompilerSettings settings,
        IDocumentNotes notes, CancellationToken cancellationToken)
    {
        if (language == DecompilationLanguage.CSharp)
        {
            WriteCSharp(reading, request, builder, settings, notes, cancellationToken);
        }
        else
        {
            WriteIL(reading, request, builder, language, settings, notes, cancellationToken);
        }
    }

    // The failure said by the cause's message, as app notes; the exception (its type and stack trace) stays on the
    // document, for the report that carries it as technical details.
    private static DecompiledDocument DescribeFailure(DocumentRequest request, DecompilationLanguage language, IDocumentNotes notes, Exception thrown)
    {
        var error = new DocumentBuilder { Language = language };
        WriteNote(error, new DocumentNote.DecompileFailed(request.FullName), notes);
        WriteNote(error, new DocumentNote.FailureCause(DocumentNote.OneLine(thrown.Message)), notes);
        return error.BuildFailure(request.Title, thrown);
    }

    // A file that could not be read as an assembly, said from its cause, as an app note; the exception stays on the
    // document.
    private static DecompiledDocument DescribeLoadError(DocumentRequest.Failure failure, DecompilationLanguage language, IDocumentNotes notes)
    {
        var error = failure.Assembly.Error;
        var builder = new DocumentBuilder { Language = language };
        WriteNote(builder, new DocumentNote.AssemblyUnreadable { File = error.FilePath.FileName, Error = error }, notes);
        return builder.BuildFailure(failure.Title, error);
    }

    // Code read through a type system that read a reference by name only, not found or its file unreadable, says so first.
    private static void WriteUnreadReferences(DocumentBuilder builder, DocumentRequest request, DecompilerTypeSystem? typeSystem, IDocumentNotes notes)
    {
        var module = builder.Assembly;
        if (module is null || typeSystem is null || !IsReadThroughTypeSystem(request))
        {
            return;
        }

        builder.WriteUnreadReferences(LoadedAssemblies.ListUnreadReferences(module), LoadedAssemblies.ListMissingReferences(module), notes);
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

    /// <summary>Writes the code <paramref name="export"/> names to <paramref name="stream"/>, the app's notes beside a
    /// document's code in <paramref name="notes"/>' words. Throws when decompiling fails.</summary>
    internal static void WriteCode(ModuleReadLease reading, CodeExport export, DecompilerTypeSystem? typeSystem, DecompilerTypeSystem? chosenTypeSystem,
        DecompilationLanguage language, DecompilerOptions options, IDocumentNotes notes, CallbackFailure callbacks, Stream stream, CancellationToken cancellationToken)
    {
        using var writer = new StreamWriter(stream);
        export.Switch((reading, typeSystem, chosenTypeSystem, language, options, notes, callbacks, writer, cancellationToken),
            static (s, whole) => WriteWholeCode(s.reading, whole, RequireTypeSystem(s.typeSystem), s.chosenTypeSystem, s.language, s.options, s.callbacks, s.writer,
                s.cancellationToken),
            static (s, ns) => WriteWholeCode(s.reading, ns, RequireTypeSystem(s.typeSystem), s.chosenTypeSystem, s.language, s.options, s.callbacks, s.writer,
                s.cancellationToken),
            static (s, document) =>
            {
                var written = DecompileOrThrow(s.reading, document.Request, s.typeSystem, s.chosenTypeSystem, s.language, s.options, s.notes, s.callbacks,
                    s.cancellationToken);
                s.writer.Write(Documents.GetText(written));
            });
    }

    private static void WriteWholeCode(ModuleReadLease reading, CodeExport export, DecompilerTypeSystem typeSystem, DecompilerTypeSystem? chosenTypeSystem,
        DecompilationLanguage language, DecompilerOptions options, CallbackFailure callbacks, TextWriter writer, CancellationToken cancellationToken)
    {
        var settings = Settings.Create(options);
        var output = new PlainTextOutput(writer) { IndentationString = settings.CSharpFormattingOptions.IndentationString };
        if (language == DecompilationLanguage.CSharp)
        {
            WriteWholeCSharp(export, RequireTypeSystem(chosenTypeSystem), output, settings, callbacks, cancellationToken);
        }
        else
        {
            WriteWholeIL(reading, export, typeSystem, chosenTypeSystem, output, language, settings, callbacks, cancellationToken);
        }
    }

    private static void WriteWholeCSharp(CodeExport export, DecompilerTypeSystem chosenTypeSystem, ITextOutput output, DecompilerSettings settings,
        CallbackFailure callbacks, CancellationToken cancellationToken)
    {
        var syntaxTree = DecompileWhole(export, chosenTypeSystem, settings, callbacks, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        syntaxTree.AcceptVisitor(new InsertParenthesesVisitor { InsertParenthesesForReadability = true });
        var writer = new TextTokenWriter(output, settings);
        syntaxTree.AcceptVisitor(new CSharpOutputVisitor(writer, settings.CSharpFormattingOptions));
    }

    private static SyntaxTree DecompileWhole(CodeExport export, DecompilerTypeSystem chosenTypeSystem, DecompilerSettings settings,
        CallbackFailure callbacks, CancellationToken cancellationToken)
    {
        if (export is CodeExport.WholeModule whole)
        {
            return CreateCSharpDecompiler(whole.Module, chosenTypeSystem, settings, callbacks, cancellationToken).DecompileWholeModuleAsSingleFile();
        }

        if (export is CodeExport.Namespace ns)
        {
            return CreateCSharpDecompiler(ns.Module, chosenTypeSystem, settings, callbacks, cancellationToken).DecompileTypes(TypeHandles(ns));
        }

        throw new UnreachableException();
    }

    private static void WriteWholeIL(ModuleReadLease reading, CodeExport export, DecompilerTypeSystem typeSystem, DecompilerTypeSystem? chosenTypeSystem,
        ITextOutput output, DecompilationLanguage language, DecompilerSettings settings, CallbackFailure callbacks, CancellationToken cancellationToken)
    {
        if (export is CodeExport.WholeModule whole)
        {
            var file = LoadedAssemblies.GetFile(whole.Module);
            var disassembler = CreateDisassembler(reading, whole.Module, typeSystem, chosenTypeSystem, output, callbacks, language, settings, cancellationToken);
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
            CreateDisassembler(reading, ns.Module, typeSystem, chosenTypeSystem, output, callbacks, language, settings, cancellationToken)
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

    private static void WriteCSharp(ModuleReadLease reading, DocumentRequest request, DocumentBuilder output, DecompilerSettings settings,
        IDocumentNotes notes, CancellationToken cancellationToken)
    {
        request.Switch((reading, output, settings, notes, cancellationToken),
            static (s, preview) =>
            {
                WriteAssemblySummary(preview.Module, s.output, s.notes);
                WriteSyntaxTree(s.reading, preview.Module, s.output, s.settings, d => d.DecompileModuleAndAssemblyAttributes(), s.cancellationToken);
            },
            static (s, type) =>
            {
                var definition = type.Definition;
                WriteHeader(definition.Module, s.output, s.notes, NodeDefinitions.GetSymbol(definition), ConversionFlags.UseFullyQualifiedTypeNames | ConversionFlags.UseFullyQualifiedEntityNames);
                WriteSyntaxTree(s.reading, definition.Module, s.output, s.settings, d => d.Decompile(definition.Handle), s.cancellationToken);
            },
            static (s, member) => WriteMember(s.reading, member, s.output, s.settings, s.notes, s.cancellationToken),
            static (s, summary) => WriteSummary(summary.Info, s.output, s.notes, s.cancellationToken),
            static (s, resource) => WriteResource(resource, s.output, s.notes),
            static (s, baml) => WriteXaml(s.reading, baml, s.output, s.cancellationToken),
            static (_, _) => throw new UnreachableException());
    }

    private static void WriteMember(ModuleReadLease reading, DocumentRequest.Member request, DocumentBuilder output, DecompilerSettings settings, IDocumentNotes notes,
        CancellationToken cancellationToken)
    {
        var module = request.Definition.Module;
        var member = NodeDefinitions.GetSymbol(request.Definition);
        var declaring = member.DeclaringTypeDefinition;
        if (declaring is null)
        {
            WriteSyntaxTree(reading, module, output, settings, d => d.Decompile(request.Definition.Reference.Handle), cancellationToken);
            return;
        }
        WriteHeader(module, output, notes, member.DeclaringType, ConversionFlags.UseFullyQualifiedEntityNames | ConversionFlags.UseFullyQualifiedTypeNames);
        WriteSyntaxTree(reading, module, output, settings, d => DecompileMember(d, member, declaring, output.Callbacks), cancellationToken);
    }

    private static void WriteIL(ModuleReadLease reading, DocumentRequest request, DocumentBuilder output, DecompilationLanguage language,
        DecompilerSettings settings, IDocumentNotes notes, CancellationToken cancellationToken)
    {
        request.Switch((reading, output, language, settings, notes, cancellationToken),
            static (s, preview) =>
            {
                var file = LoadedAssemblies.GetFile(preview.Module);
                var disassembler = CreateDisassembler(s.reading, preview.Module, RequireTypeSystem(s.output.TypeSystem), s.output.ChosenTypeSystem, s.output, s.output.Callbacks, s.language, s.settings, s.cancellationToken);
                WriteAssemblySummary(preview.Module, s.output, s.notes);
                s.output.WriteLine();
                disassembler.WriteAssemblyReferences(file.Metadata);
                if (file.Metadata.IsAssembly)
                {
                    disassembler.WriteAssemblyHeader(file);
                }

                s.output.WriteLine();
                disassembler.WriteModuleHeader(file);
            },
            static (s, type) => CreateDisassembler(s.reading, type.Definition.Module, RequireTypeSystem(s.output.TypeSystem), s.output.ChosenTypeSystem, s.output, s.output.Callbacks, s.language, s.settings, s.cancellationToken)
                .DisassembleType(LoadedAssemblies.GetFile(type.Definition.Module), type.Definition.Handle),
            static (s, member) =>
            {
                var memberDisassembler = CreateDisassembler(s.reading, member.Definition.Module, RequireTypeSystem(s.output.TypeSystem), s.output.ChosenTypeSystem, s.output, s.output.Callbacks, s.language, s.settings,
                    s.cancellationToken);
                DisassembleMember(memberDisassembler, LoadedAssemblies.GetFile(member.Definition.Module), member.Definition.Reference.Handle);
            },
            static (s, summary) => WriteSummary(summary.Info, s.output, s.notes, s.cancellationToken),
            static (s, resource) => WriteResource(resource, s.output, s.notes),
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

    private static ReflectionDisassembler CreateDisassembler(ModuleReadLease reading, LoadedModule assembly, DecompilerTypeSystem typeSystem,
        DecompilerTypeSystem? chosenTypeSystem, ITextOutput output, CallbackFailure callbacks, DecompilationLanguage language, DecompilerSettings settings,
        CancellationToken cancellationToken)
    {
        var body = CreateMethodBodyDisassembler(assembly, typeSystem, chosenTypeSystem, output, callbacks, language, settings, cancellationToken);
        return new ReflectionDisassembler(output, body, cancellationToken)
        {
            DetectControlStructure = true,
            ExpandMemberDefinitions = true,
            AssemblyResolver = LoadedAssemblies.CreateResolver(reading, assembly, callbacks),
            DebugInfo = DebugSymbolsFor(assembly, settings, callbacks),
        };
    }

    private static MethodBodyDisassembler CreateMethodBodyDisassembler(LoadedModule assembly, DecompilerTypeSystem typeSystem,
        DecompilerTypeSystem? chosenTypeSystem, ITextOutput output, CallbackFailure callbacks, DecompilationLanguage language, DecompilerSettings settings,
        CancellationToken cancellationToken)
    {
        if (language == DecompilationLanguage.ILWithCSharp)
        {
            return new MethodBodyWithCSharp(output, callbacks, assembly, typeSystem, RequireTypeSystem(chosenTypeSystem), settings, cancellationToken);
        }

        return new MethodBodyDisassembler(output, cancellationToken);
    }

    // ILSpy opens a type or member with these comments.
    private static void WriteHeader(LoadedModule assembly, DocumentBuilder output, IDocumentNotes notes, IType type, ConversionFlags flags)
    {
        var attributes = RequireTypeSystem(output.TypeSystem).MainModule.GetAssemblyAttributes();
        if (IsReferenceAssembly(attributes))
        {
            WriteNote(output, new DocumentNote.ReferenceAssembly(), notes);
        }

        WriteComment(output, assembly.FullName);
        var ambience = new CSharpAmbience { ConversionFlags = ConversionFlags.ShowTypeParameterList | ConversionFlags.PlaceReturnTypeAfterParameterList | flags };
        WriteComment(output, type is ITypeDefinition definition ? ambience.ConvertSymbol(definition) : ambience.ConvertType(type));
    }

    private static bool IsReferenceAssembly(IEnumerable<IAttribute> attributes)
    {
        return attributes.Any(static a => a.AttributeType.FullName == ReferenceAssemblyAttribute);
    }

    // As ILSpy shows a member on its own: a constructor of a class, or a field that is not constant, with the members
    // whose initializers it shares.
    private static SyntaxTree DecompileMember(CSharpDecompiler decompiler, IMember member, ITypeDefinition declaring, CallbackFailure callbacks)
    {
        var module = decompiler.TypeSystem.MainModule;
        member = DecompilerFacts.ResolveMember(module, member.MetadataToken);
        declaring = module.GetDefinition(DecompilerFacts.GetTypeHandle(declaring));
        switch (member)
        {
            case IMethod { IsConstructor: true } ctor when ctor.DeclaringType.IsReferenceType != false:
                decompiler.AstTransforms.Add(new ConstructorSelection { Kept = ctor, Callbacks = callbacks });
                return decompiler.Decompile(MemberSelection.FieldsAndConstructors(declaring, ctor.IsStatic));
            case IField { IsConst: false } field:
                decompiler.AstTransforms.Add(new FieldSelection { Kept = field, Callbacks = callbacks });
                return decompiler.Decompile(MemberSelection.FieldsAndConstructors(declaring, field.IsStatic));
            default:
                return decompiler.Decompile(member.MetadataToken);
        }
    }

    private static void WriteSyntaxTree(ModuleReadLease reading, LoadedModule assembly, DocumentBuilder output, DecompilerSettings settings,
        Func<CSharpDecompiler, SyntaxTree> decompile, CancellationToken cancellationToken)
    {
        var decompiler = CreateCSharpDecompiler(assembly, RequireTypeSystem(output.ChosenTypeSystem), settings, output.Callbacks, cancellationToken);
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
    /// <paramref name="chosenTypeSystem"/>, the type system those settings read, which the unit awaited before its read
    /// began (<see cref="LoadedAssemblies.ReadChosenTypeSystemAsync(ModuleReadLease, LoadedModule, DecompilerSettings, CancellationToken)"/>).</summary>
    internal static CSharpDecompiler CreateCSharpDecompiler(LoadedModule assembly, DecompilerTypeSystem chosenTypeSystem, DecompilerSettings settings,
        CallbackFailure callbacks, CancellationToken cancellationToken)
    {
        return new CSharpDecompiler(chosenTypeSystem, settings)
        {
            CancellationToken = cancellationToken,
            DebugInfoProvider = DebugSymbolsFor(assembly, settings, callbacks),
        };
    }

    /// <summary><paramref name="typeSystem"/>, which the unit writing awaited and handed down before writing code that
    /// reads it; throws when it did not, a bug.</summary>
    internal static DecompilerTypeSystem RequireTypeSystem(DecompilerTypeSystem? typeSystem)
    {
        return typeSystem ?? throw new InvalidOperationException("Code that reads a module's type system was written without the read its unit awaited.");
    }

    /// <summary>What the decompiler reads of <paramref name="assembly"/>'s PDB during one read: a reader made for that
    /// read, its own work counted into <paramref name="callbacks"/>, the read's (CJ11); null without symbols or when the
    /// settings leave them out.</summary>
    internal static IDebugInfoProvider? DebugSymbolsFor(LoadedModule assembly, DecompilerSettings settings, CallbackFailure callbacks)
    {
        if (!settings.UseDebugSymbols)
        {
            return null;
        }

        var symbols = LoadedAssemblies.EnsureDebugSymbols(assembly);
        return symbols is null ? null : new DebugSymbolsReader(symbols, callbacks);
    }

    private static void WriteAssemblySummary(LoadedModule assembly, DocumentBuilder output, IDocumentNotes notes)
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
                WriteEntryPoint(MetadataTokens.EntityHandle(entryPoint), output, notes);
            }
        }
        var pe = LoadedAssemblies.FindPEFile(assembly);
        if (pe is not null)
        {
            WriteNote(output, new DocumentNote.ArchitectureOf(ArchitectureOf(pe.Reader.PEHeaders)), notes);
        }

        if (assembly.TargetFramework is not null)
        {
            WriteNote(output, new DocumentNote.TargetFramework(assembly.TargetFramework.Text), notes);
        }

        output.WriteLine();
    }

    // The note's words around the method, which is written as a link where the language's template puts it
    // (DocumentNote.EntryPoint).
    private static void WriteEntryPoint(EntityHandle handle, DocumentBuilder output, IDocumentNotes notes)
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

        WriteNote(output, new DocumentNote.EntryPoint { Method = EntityText.FullName(method), Target = reference }, notes);
    }

    // The platform as the build tools name it, and the bitness an AnyCPU module prefers, as ILSpy reads them.
    internal static ModuleArchitecture ArchitectureOf(PEHeaders headers)
    {
        var machine = headers.CoffHeader.Machine;
        var flags = headers.CorHeader?.Flags ?? 0;
        if (machine != Machine.I386 || (flags & CorFlags.ILOnly) == 0)
        {
            return new ModuleArchitecture { Platform = machine.ToString(), PreferredBits = null };
        }

        if ((flags & CorFlags.Requires32Bit) == 0)
        {
            return new ModuleArchitecture { Platform = AnyCpu, PreferredBits = 64 };
        }

        return (flags & CorFlags.Prefers32Bit) != 0
            ? new ModuleArchitecture { Platform = AnyCpu, PreferredBits = 32 }
            : new ModuleArchitecture { Platform = "x86", PreferredBits = null };
    }

    // The platform's name in the build tools' notation, the same in every language.
    private const string AnyCpu = "AnyCPU";

    private static void WriteSummary(SummaryInfo summary, DocumentBuilder output, IDocumentNotes notes, CancellationToken cancellationToken)
    {
        summary.Switch((output, notes, cancellationToken),
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
                    WriteNamespaceEntry(s.output, s.notes, entry);
                }
            },
            static (s, forwarded) =>
            {
                WriteNote(s.output, new DocumentNote.ForwardedTo(forwarded.Destination ?? ""), s.notes);
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
                WriteNote(s.output, new DocumentNote.ModuleKind(module.ContainsMetadata), s.notes);
                WriteImports(s.output, module.Imports);
            },
            static (s, import) =>
            {
                WriteLink(s.output, import.Method, SpanKind.Method);
                WriteNote(s.output, new DocumentNote.Calls { Entry = import.EntryPoint, ModuleName = import.ModuleName }, s.notes);
            },
            static (s, reference) =>
            {
                WriteComment(s.output, reference.Reference.FullName);
                var path = LoadedAssemblies.GetResolver(reference.Module).FindAssemblyFile(reference.Reference);
                WriteNote(s.output, new DocumentNote.Resolution(path is null ? null : $"{path}"), s.notes);
            },
            static (s, used) => WriteUsed(s.output, s.notes, used.Entries, "", s.cancellationToken),
            static (s, types) =>
            {
                foreach (var type in types.Entries)
                {
                    WriteLink(s.output, type, SpanKind.Type);
                }
            });
    }

    private static void WriteNamespaceEntry(DocumentBuilder output, IDocumentNotes notes, NamespaceEntry entry)
    {
        entry.Switch((output, notes),
            static (s, declared) => WriteTypeDeclaration(s.output, declared),
            static (s, forwarded) => WriteForwardedType(s.output, s.notes, forwarded.Line));
    }

    private static void WriteUsed(DocumentBuilder output, IDocumentNotes notes, IReadOnlyList<UsedEntry> entries, string indent, CancellationToken cancellationToken)
    {
        foreach (var entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            output.Write(indent);
            entry.Switch((output, notes, indent, cancellationToken),
                static (s, type) =>
                {
                    s.output.Write(type.FullName, SpanKind.Type, type.Target);
                    s.output.WriteLine();
                    WriteUsed(s.output, s.notes, type.Used, s.indent + UsedIndent, s.cancellationToken);
                },
                static (s, member) =>
                {
                    s.output.Write(member.Label, member.Kind, member.Target);
                    s.output.WriteLine();
                },
                static (s, forwarded) => WriteForwardedType(s.output, s.notes, forwarded.Line));
        }
    }

    private static void WriteForwardedType(DocumentBuilder output, IDocumentNotes notes, ForwardedLine line)
    {
        output.Write(line.Label, SpanKind.Type, line.Target);
        output.WriteNote(new DocumentNote.ForwardedTo(line.Destination ?? ""), notes);
        output.WriteLine();
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

    private static void WriteXaml(ModuleReadLease reading, DocumentRequest.Baml baml, DocumentBuilder output, CancellationToken cancellationToken)
    {
        var xaml = Baml.DecompileToText(reading, baml.Module, baml.Data, output.Callbacks, cancellationToken);
        ResourceText.Write(output, xaml, ResourceSyntax.Xml);
        output.WriteLine();
    }

    private static void WriteResource(DocumentRequest.Resource resource, DocumentBuilder output, IDocumentNotes notes)
    {
        var data = resource.Read();
        // Colored as XML or JSON when it is that, as ILSpy shows it.
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
            WriteNote(output, new DocumentNote.Truncated(MaxResourceBytes), notes);
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

    private static void WriteReferenceLine(DocumentBuilder output, SummaryLink reference)
    {
        output.Write("// ", SpanKind.Comment);
        output.Write(reference.Text, SpanKind.Plain, reference.Target);
        output.WriteLine();
    }

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
        output.Write(DocumentBuilder.CommentMarker + text, SpanKind.Comment);
        output.WriteLine();
    }

    // The app's note on a line of its own, kept as its cause (DocumentBuilder.WriteNote).
    private static void WriteNote(DocumentBuilder output, DocumentNote note, IDocumentNotes notes)
    {
        output.WriteNote(note, notes);
        output.WriteLine();
    }

    // Tells the decompiler where in the DocumentBuilder each token lands, so that it can record where each node of the
    // syntax tree was written: the app's code the library calls back (CD4).
    private sealed class LocatingTokenWriter(TokenWriter decorated, DocumentBuilder builder) : DecoratingTokenWriter(decorated), ILocatable
    {
        public TextLocation Location => builder.Callbacks.Run(builder, static b => b.Location);

        public int Length => 0;
    }
}

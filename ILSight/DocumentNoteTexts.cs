using ILSight.Core;
using ILSight.Core.Resources;

namespace ILSight;

/// <summary>The notes the app writes into documents beside the code, in <paramref name="t"/>'s language: the reference
/// warnings as their causes' notes (the module-unreadable cause's, the not-found reference's), the rest the documents'
/// own messages.</summary>
public sealed class DocumentNoteTexts(Strings t) : IDocumentNotes
{
    /// <summary>The notes in <paramref name="locale"/>'s language.</summary>
    public static DocumentNoteTexts For(Locale locale)
    {
        return new DocumentNoteTexts(Strings.For(locale));
    }

    public string ReferenceUnreadable(string reference, string file, string why, string module)
    {
        return t.CauseModuleUnreadableNote(reference: reference, file: file, why: why, module: module);
    }

    public string ReferenceNotFound(string reference)
    {
        return t.CauseReferenceNotFoundNote(reference: reference);
    }

    public string DecompileFailed(string name)
    {
        return t.DocumentNoteDecompileFailed(name: name);
    }

    public string FailureCause(string message)
    {
        return t.DocumentNoteFailureCause(message);
    }

    public string AssemblyUnreadable(string file, string? why)
    {
        return why is null ? t.ErrorFileDamaged(file) : t.CauseModuleUnreadableSentence(name: file, why: why);
    }

    public string ReferenceAssembly()
    {
        return t.DocumentNoteReferenceAssembly;
    }

    public string ModuleWithMetadata()
    {
        return t.DocumentNoteModuleWithMetadata;
    }

    public string ModuleWithoutMetadata()
    {
        return t.DocumentNoteModuleWithoutMetadata;
    }

    public string Calls(string entryPoint, string module)
    {
        return t.DocumentNoteCalls(entry: entryPoint, module: module);
    }

    public string ReferenceNotResolved()
    {
        return t.DocumentNoteReferenceNotResolved;
    }

    public string ResolvesTo(string path)
    {
        return t.DocumentNoteResolvesTo(path: path);
    }

    public string PictureUnreadable(PictureFault fault)
    {
        return fault.Match(
            c => t.CausePictureCutShortNote(format: c.Format),
            p => t.CausePicturePixelsCutShortNote(format: p.Format),
            s => t.CausePictureSizeOutOfRangeNote(width: s.Width, height: s.Height),
            _ => t.CausePicturePaletteIndexOutOfRangeNote,
            _ => t.CausePictureCompressionNotReadNote,
            b => t.CausePictureBitDepthNotReadNote(bits: b.Bits),
            n => t.CausePictureNoPictureNote(format: n.Format),
            _ => t.CausePictureCodeSizeOutOfRangeNote,
            c => t.CausePictureColorTypeUnknownNote(kind: c.ColorType),
            _ => t.CausePictureNotAPictureNote,
            l => t.CausePictureUnreadableNote(message: l.Message));
    }

    public string Truncated(int bytes)
    {
        return t.DocumentNoteTruncated(bytes: bytes);
    }

    public string Architecture(ModuleArchitecture architecture)
    {
        var bits = architecture.PreferredBits;
        if (bits is null)
        {
            return t.DocumentNoteArchitecture(architecture: architecture.Platform);
        }

        return t.DocumentNoteArchitecturePreferred(platform: architecture.Platform, bits: bits.Value);
    }

    public string TargetFramework(string framework)
    {
        return t.DocumentNoteTargetFramework(framework: framework);
    }

    public string EntryPoint(string method)
    {
        return t.DocumentNoteEntryPoint(method: method);
    }

    public string ForwardedTo(string destination)
    {
        return t.DocumentNoteForwardedTo(destination: destination);
    }
}

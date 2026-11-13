using ILSight.Core;

namespace ILSight.CodeViewArea;

/// <summary>What the pointer did in a code view: the press it holds, the reference a click marked, and what the
/// context menu was opened on.</summary>
internal sealed class CodePointer
{
    public CodePress? Press { get; private set; }

    /// <summary>The reference whose usages a click on it marked; null when none is.</summary>
    public CodeReference? Marked { get; private set; }

    public CodeMenuTarget Menu { get; private set; } = CodeMenuTarget.None;

    /// <summary>Whether a press on the text has moved off where it started, dragging a selection.</summary>
    public bool PressMoved => Press is CodePress.OnText text && text.Moved;

    public void PressOn(CodePress press)
    {
        Press = press;
    }

    public void Mark(CodeReference? reference)
    {
        Marked = reference;
    }

    public void OpenMenuOn(CodeMenuTarget target)
    {
        Menu = target;
    }

    /// <summary>Forgets the press, the mark and the menu's target, as a new document does.</summary>
    public void Forget()
    {
        Press = null;
        Marked = null;
        Menu = CodeMenuTarget.None;
    }
}

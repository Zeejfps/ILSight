namespace ILSight;

/// <summary>What a frame of a code view asked for: what the user asked it to do, the last thing when more
/// than one, or null; and how to change the size of its code.</summary>
public readonly record struct CodeViewResult(CodeViewIntent? Intent, CodeZoom Zoom);

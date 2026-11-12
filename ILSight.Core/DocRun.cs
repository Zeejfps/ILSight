namespace ILSight.Core;

public readonly record struct DocRun(string Text, DocStyle Style, CodeReference? Reference = null);

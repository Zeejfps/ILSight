namespace ILSight;

/// <summary>Something wrong in the theme file <see cref="File"/> at <see cref="Line"/>, counted from 1,
/// or anywhere in it when 0: the key or value it is about, and what more there is to tell.</summary>
public sealed record ThemeError(string File, int Line, ThemeProblem Problem, string Subject = "", string Detail = "");

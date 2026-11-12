namespace ILSight;

/// <summary>How many lines a text has, and how many characters its longest one.</summary>
internal readonly record struct TextSize(int Lines, int Longest);

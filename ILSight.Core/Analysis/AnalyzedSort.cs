namespace ILSight.Core.Analysis;

/// <summary>Which of the library's symbols an analyzed one is, as the table of analyses tells them apart.</summary>
public enum AnalyzedSort
{
    Module,
    Type,
    Field,
    Method,
    Property,
    Event,
    Other,
}

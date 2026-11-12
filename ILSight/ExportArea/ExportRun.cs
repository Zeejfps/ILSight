namespace ILSight.ExportArea;

/// <summary>Which export of the session one is, through waiting, running and finishing; given by the
/// queue as the export enters it, numbered from 1 in the order they entered.</summary>
public sealed record ExportRun
{
    internal ExportRun(int number)
    {
        Number = number;
    }

    internal int Number { get; }
}

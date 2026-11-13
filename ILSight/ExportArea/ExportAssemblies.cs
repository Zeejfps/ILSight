using ILSight.Core;
using ILSight.AssemblyArea;

namespace ILSight.ExportArea;

/// <summary>The open assemblies the export dialog offers, each with its project name and whether it is ticked, and
/// the page the dialog shows: the main page, or the Choose page, whose list is arranged afresh each time it
/// shows.</summary>
public sealed class ExportAssemblies
{
    private readonly List<ExportChoice> _choices = [];

    public IReadOnlyList<ExportChoice> Choices => _choices;

    public ExportPage Page { get; private set; }

    /// <summary>Offers <paramref name="assemblies"/> under unique project names on the main page, with
    /// <paramref name="picked"/> ticked, or the only one when there is just one.</summary>
    public void Offer(IReadOnlyList<LoadedModule> assemblies, LoadedModule? picked)
    {
        _choices.Clear();
        foreach (var project in ExportNames.NameProjects(assemblies))
        {
            _choices.Add(new ExportChoice { Assembly = project.Assembly, Name = project.Name });
        }

        TickFirstChoice(picked);
        Page = ExportPage.Main;
    }

    private void TickFirstChoice(LoadedModule? picked)
    {
        foreach (var choice in _choices)
        {
            choice.Included = choice.Assembly == picked;
        }

        if (_choices.Count == 1)
        {
            _choices[0].Included = true;
        }
    }

    /// <summary>Shows the Choose page, arranging <paramref name="rows"/>, its list, afresh.</summary>
    public void StartChoosing(PickerRows rows)
    {
        rows.Arrange(_choices);
        Page = ExportPage.Choose;
    }

    /// <summary>Goes back from the Choose page to what is exported.</summary>
    public void FinishChoosing()
    {
        Page = ExportPage.Main;
    }
}

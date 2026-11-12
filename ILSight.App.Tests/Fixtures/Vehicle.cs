namespace ILSight.App.Tests.Fixtures;

public abstract class Vehicle
{
    private int _mileage = 10;
    internal string Plate = "";

    public string Name { get; set; } = "";

    public Garage? Garage { get; set; }

    public abstract int Wheels { get; }

    /// <summary>How far the vehicle has gone.</summary>
    public int Mileage()
    {
        return _mileage;
    }
}

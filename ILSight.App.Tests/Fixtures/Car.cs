namespace ILSight.App.Tests.Fixtures;

public class Car : Vehicle, IDriven
{
    public override int Wheels => 4;

    public void Drive()
    {
    }
}

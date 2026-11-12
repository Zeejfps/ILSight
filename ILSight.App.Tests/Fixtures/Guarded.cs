#pragma warning disable SYSLIB0003

using System.Security.Permissions;

namespace ILSight.App.Tests.Fixtures;

public class Guarded
{
    [Guard(SecurityAction.Demand)]
    public void Enter()
    {
    }
}

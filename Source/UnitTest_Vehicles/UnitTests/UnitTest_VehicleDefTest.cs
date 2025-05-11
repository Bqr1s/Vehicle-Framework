using System.Collections.Generic;
using DevTools.UnitTesting;

namespace Vehicles.UnitTesting;

internal class UnitTest_VehicleDefTest
{
  protected readonly List<VehicleDef> vehicleDefs = [];

  protected virtual bool ShouldTest(VehicleDef vehicleDef)
  {
    return true;
  }

  [SetUp]
  protected void GenerateVehicles()
  {
    vehicleDefs.Clear();
    foreach (VehicleDef vehicleDef in VehicleHarmony.AllMoveableVehicleDefs)
    {
      if (!ShouldTest(vehicleDef))
        continue;
      vehicleDefs.Add(vehicleDef);
    }
  }
}
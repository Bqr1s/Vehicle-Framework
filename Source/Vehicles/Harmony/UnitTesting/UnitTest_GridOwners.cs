using System.Linq;
using DevTools;
using DevTools.UnitTesting;
using SmashTools;
using Verse;
using TestType = DevTools.UnitTesting.TestType;

namespace Vehicles.Testing;

[UnitTest(TestType.Playing)]
internal class UnitTest_GridOwners : UnitTest_MapTest
{
  protected override bool ShouldTest(VehicleDef vehicleDef)
  {
    VehicleMapping mapping = map.GetCachedMapComponent<VehicleMapping>();
    if (mapping.GridOwners.IsOwner(vehicleDef) && !mapping.GridOwners.GetPiggies(vehicleDef).Any())
    {
      // Single owner def, there's no point testing ownership since
      // ownership has no one to transfer to
      return false;
    }
    return PathingHelper.ShouldCreateRegions(vehicleDef);
  }

  [Test]
  private void Map()
  {
    foreach (VehiclePawn vehicle in vehicles)
    {
      using VehicleTestCase vtc = new(vehicle, this);

      VehicleDef vehicleDef = vehicle.VehicleDef;

      VehicleMapping mapping = map.GetCachedMapComponent<VehicleMapping>();
      VehicleMapping.VehiclePathData pathData = mapping[vehicleDef];

      mapping.deferredGridGeneration.DoPass();
      Assert.IsTrue(pathData.Suspended);
      Assert.IsFalse(pathData.VehiclePathGrid.Enabled);

      if (mapping.GridOwners.IsOwner(vehicleDef))
      {
        // We need to directly transfer ownership so we can test a piggy being spawned in with
        // invalid regions and acquiring ownership over it.
        VehicleDef tempNewOwnerDef = mapping.GridOwners.GetPiggies(vehicleDef).FirstOrDefault();
        Assert.IsNotNull(tempNewOwnerDef);
        mapping.GridOwners.TransferOwnership(tempNewOwnerDef);
      }
      Assert.IsFalse(mapping.GridOwners.IsOwner(vehicleDef));

      mapping.RequestGridsFor(vehicleDef, DeferredGridGeneration.Urgency.Urgent);

      Expect.IsTrue("Ownership Taken", mapping.GridOwners.IsOwner(vehicleDef));
      Expect.IsFalse("PathData Initialized", pathData.Suspended);
      Expect.IsTrue("PathGrid Enabled", pathData.VehiclePathGrid.Enabled);

      VehicleDef piggyDef = mapping.GridOwners.GetPiggies(vehicleDef).FirstOrDefault();
      Assert.IsNotNull(piggyDef);
      Assert.IsFalse(piggyDef == vehicleDef);
      VehicleMapping.VehiclePathData piggyPathData = mapping[piggyDef];
      Assert.IsTrue(piggyPathData.ReachabilityData == pathData.ReachabilityData);

      mapping.RequestGridsFor(piggyDef, DeferredGridGeneration.Urgency.Urgent);

      Expect.IsTrue("Ownership Retained", mapping.GridOwners.IsOwner(vehicleDef));
      Expect.IsFalse("PathData Initialized (Piggy)", piggyPathData.Suspended);
      Expect.IsTrue("PathGrid Enabled (Piggy)", piggyPathData.VehiclePathGrid.Enabled);

      VehiclePawn piggyVehicle = VehicleSpawner.GenerateVehicle(piggyDef, Faction);
      GenSpawn.Spawn(piggyVehicle, root, map, Rot4.North);

      mapping.deferredGridGeneration.DoPass();
      Expect.IsFalse("PathGrid Released", pathData.VehiclePathGrid.Enabled);
      Expect.IsFalse("Ownership Forfeited", mapping.GridOwners.IsOwner(vehicleDef));
      Expect.IsTrue("Ownership Transferred", mapping.GridOwners.IsOwner(piggyDef));
      Expect.IsTrue("MapGridOwners (Ownership Updated)",
        pathData.ReachabilityData.regionAndRoomUpdater.createdFor == piggyDef);
      Expect.IsFalse("PathData Retained", pathData.Suspended);
      Expect.IsTrue("PathGrid Retained (Piggy)", piggyPathData.VehiclePathGrid.Enabled);

      vehicle.Destroy();
      piggyVehicle.Destroy();

      mapping.deferredGridGeneration.DoPass();
      Expect.IsTrue("PathData Released", pathData.Suspended);
      Expect.IsFalse("PathGrid Released", pathData.VehiclePathGrid.Enabled);
      Expect.IsTrue("Final Ownership Retained", mapping.GridOwners.IsOwner(piggyDef));
    }
  }
}
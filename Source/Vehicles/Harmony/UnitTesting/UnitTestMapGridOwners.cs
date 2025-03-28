using System.Linq;
using SmashTools;
using SmashTools.Debugging;
using Verse;

namespace Vehicles.Testing;

internal class UnitTestMapGridOwners : UnitTestMapTest
{
  public override string Name => "MapGridOwners";

  protected override bool ShouldTest(VehicleDef vehicleDef)
  {
    VehicleMapping mapping = TestMap.GetCachedMapComponent<VehicleMapping>();
    if (mapping.GridOwners.IsOwner(vehicleDef) && !mapping.GridOwners.GetPiggies(vehicleDef).Any())
    {
      // Single owner def, there's no point testing ownership since
      // ownership has no one to transfer to
      return false;
    }
    return PathingHelper.ShouldCreateRegions(vehicleDef);
  }

  protected override UTResult TestVehicle(VehiclePawn vehicle, IntVec3 root)
  {
    VehicleDef vehicleDef = vehicle.VehicleDef;

    UTResult result = new();

    VehicleMapping mapping = TestMap.GetCachedMapComponent<VehicleMapping>();
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

    result.Add($"MapGridOwners_{vehicleDef} (Generated)",
      !pathData.Suspended && pathData.VehiclePathGrid.Enabled);
    result.Add("MapGridOwners (Ownership Transferred)", mapping.GridOwners.IsOwner(vehicleDef));

    VehicleDef piggyDef = mapping.GridOwners.GetPiggies(vehicleDef).FirstOrDefault();
    Assert.IsNotNull(piggyDef);
    Assert.IsFalse(piggyDef == vehicleDef);
    VehicleMapping.VehiclePathData piggyPathData = mapping[piggyDef];
    Assert.IsTrue(piggyPathData.ReachabilityData == pathData.ReachabilityData);

    mapping.RequestGridsFor(piggyDef, DeferredGridGeneration.Urgency.Urgent);

    result.Add("MapGridOwners (Piggy Generated)",
      !piggyPathData.Suspended && piggyPathData.VehiclePathGrid.Enabled);
    result.Add("MapGridOwners (Ownership Retained)", mapping.GridOwners.IsOwner(vehicleDef));

    VehiclePawn piggyVehicle = VehicleSpawner.GenerateVehicle(piggyDef, Faction);
    GenSpawn.Spawn(piggyVehicle, root, TestMap, Rot4.North);

    mapping.deferredGridGeneration.DoPass();
    result.Add("MapGridOwners (Owner PathGrid Released)", !pathData.VehiclePathGrid.Enabled);
    result.Add("MapGridOwners (Ownership Forfeited)", !mapping.GridOwners.IsOwner(vehicleDef));
    result.Add("MapGridOwners (Ownership Transferred)", mapping.GridOwners.IsOwner(piggyDef));
    result.Add("MapGridOwners (Ownership Updated)",
      pathData.ReachabilityData.regionAndRoomUpdater.createdFor == piggyDef);
    result.Add("MapGridOwners (RegionGrid Retained)", !pathData.Suspended);
    result.Add("MapGridOwners (Piggy PathGrid Retained)", piggyPathData.VehiclePathGrid.Enabled);

    vehicle.Destroy();
    piggyVehicle.Destroy();

    mapping.deferredGridGeneration.DoPass();
    result.Add("MapGridOwners (Final PathGrid Released)",
      pathData.Suspended && !pathData.VehiclePathGrid.Enabled);
    result.Add("MapGridOwners (Final Ownership Not Forfeited)",
      mapping.GridOwners.IsOwner(piggyDef));
    result.Add("MapGridOwners (Final RegionGrid Released)", pathData.Suspended);

    return result;
  }
}
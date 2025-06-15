﻿using System.Linq;
using DevTools;
using DevTools.UnitTesting;
using SmashTools;
using Verse;
using TestType = DevTools.UnitTesting.TestType;

namespace Vehicles.UnitTesting;

[UnitTest(TestType.Playing)]
internal sealed class UnitTest_GridOwners : UnitTest_MapTest
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

      Expect.IsTrue(mapping.GridOwners.IsOwner(vehicleDef), "Ownership Taken");
      Expect.IsFalse(pathData.Suspended, "PathData Initialized");
      Expect.IsTrue(pathData.VehiclePathGrid.Enabled, "PathGrid Enabled");

      VehicleDef piggyDef = mapping.GridOwners.GetPiggies(vehicleDef).FirstOrDefault();
      Assert.IsNotNull(piggyDef);
      Assert.ReferencesAreNotEqual(piggyDef, vehicleDef);
      VehicleMapping.VehiclePathData piggyPathData = mapping[piggyDef];
      Assert.IsTrue(piggyPathData.ReachabilityData == pathData.ReachabilityData);

      mapping.RequestGridsFor(piggyDef, DeferredGridGeneration.Urgency.Urgent);

      Expect.IsTrue(mapping.GridOwners.IsOwner(vehicleDef), "Ownership Retained");
      Expect.IsFalse(piggyPathData.Suspended, "Piggy PathData Initialized");
      Expect.IsTrue(piggyPathData.VehiclePathGrid.Enabled, "Piggy PathGrid Enabled");

      VehiclePawn piggyVehicle = VehicleSpawner.GenerateVehicle(piggyDef, Faction);
      GenSpawn.Spawn(piggyVehicle, root, map, Rot4.North);

      mapping.deferredGridGeneration.DoPass();
      Expect.IsFalse(pathData.VehiclePathGrid.Enabled, "PathGrid Released");
      Expect.IsFalse(mapping.GridOwners.IsOwner(vehicleDef), "Ownership Forfeited");
      Expect.IsTrue(mapping.GridOwners.IsOwner(piggyDef), "Ownership Transferred");
      Expect.ReferencesAreEqual(pathData.ReachabilityData.regionAndRoomUpdater.createdFor, piggyDef,
        "Ownership Updated");
      Expect.IsFalse(pathData.Suspended, "PathData Retained");
      Expect.IsTrue(piggyPathData.VehiclePathGrid.Enabled, "Piggy PathGrid Retained");

      vehicle.Destroy();
      piggyVehicle.Destroy();

      mapping.deferredGridGeneration.DoPass();
      Expect.IsTrue(pathData.Suspended, "PathData Released");
      Expect.IsFalse(pathData.VehiclePathGrid.Enabled, "PathGrid Released");
      Expect.IsTrue(mapping.GridOwners.IsOwner(piggyDef), "Final Ownership Retained");
    }
  }

  [TearDown]
  private void RegenerateAllGrids()
  {
    VehicleMapping mapping = map.GetCachedMapComponent<VehicleMapping>();
    mapping.deferredGridGeneration.DoPassExpectClear();
    mapping.RegenerateGrids(deferment: VehicleMapping.GridDeferment.Forced);
  }
}
using DevTools.UnitTesting;
using RimWorld;
using UnityEngine;
using Verse;

namespace Vehicles.Testing
{
  [UnitTest(TestType.Playing)]
  internal class UnitTest_ThingGrid : UnitTest_MapTest
  {
    protected override bool ShouldTest(VehicleDef vehicleDef)
    {
      return vehicleDef.vehicleType == VehicleType.Land &&
        VehiclePathGrid.PassableTerrainCost(vehicleDef, TerrainDefOf.Concrete, out _);
    }

    [Test]
    private void ThingGrid()
    {
      foreach (VehiclePawn vehicle in vehicles)
      {
        using VehicleTestCase vtc = new(vehicle, this);
        int maxSize = Mathf.Max(vehicle.VehicleDef.Size.x, vehicle.VehicleDef.Size.z);
        IntVec3 reposition = root + new IntVec3(maxSize, 0, 0);

        ThingGrid thingGrid = map.thingGrid;
        HitboxTester<VehiclePawn> positionTester = new(vehicle, root,
          (cell) => thingGrid.ThingAt(cell, ThingCategory.Pawn) as VehiclePawn,
          (thing) => thing == vehicle);
        positionTester.Start();

        GenSpawn.Spawn(vehicle, root, map);
        // Validate spawned vehicle registers in thingGrid
        Expect.IsTrue("Spawn", positionTester.Hitbox(true));

        // Validate position set updates thingGrid
        vehicle.Position = reposition;
        Expect.IsTrue("set_Position", positionTester.Hitbox(true));
        vehicle.Position = root;

        // Validate rotation set updates thingGrid
        vehicle.Rotation = Rot4.East;
        Expect.IsTrue("set_Rotation", positionTester.Hitbox(true));
        vehicle.Rotation = Rot4.North;

        // Validate despawning deregisters from thingGrid
        vehicle.DeSpawn();
        Expect.IsTrue("DeSpawn", positionTester.All(false));
      }
    }
  }
}
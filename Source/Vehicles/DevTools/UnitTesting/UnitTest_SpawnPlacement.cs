using DevTools;
using DevTools.UnitTesting;
using Verse;

namespace Vehicles.UnitTesting;

// NOTE - Both GenAdj.OccupiedRect and GenSpawn.Spawn have patches that adjust positions for
// vehicles. We can verify the adjustment keeps the vehicle stable (and doesn't shift positions)
// by comparing the CellRects of entity-based occupied rect vs. size based (which is not patched)
[UnitTest(TestType.Playing)]
internal sealed class UnitTest_SpawnPlacement : UnitTest_MapTest
{
  [Test]
  private void PlacementDrift()
  {
    foreach (VehiclePawn vehicle in vehicles)
    {
      using VehicleTestCase vtc = new(vehicle, this);

      IntVec2 size = vehicle.VehicleDef.Size;

      // North
      CellRect occupiedRect = GenAdj.OccupiedRect(root, Rot4.North, size);
      GenSpawn.Spawn(vehicle, root, map, Rot4.North);
      Expect.IsEqual(occupiedRect, vehicle.OccupiedRect(), "North OccupiedRect");
      Expect.IsEqual(vehicle.Position, root, "North Position");

      vehicle.DeSpawn();
      Assert.IsFalse(vehicle.Spawned);
      Assert.IsFalse(vehicle.Destroyed);
      Assert.IsFalse(vehicle.Discarded);

      // East
      occupiedRect = GenAdj.OccupiedRect(root, Rot4.East, size);
      GenSpawn.Spawn(vehicle, root, map, Rot4.East);
      Expect.IsEqual(occupiedRect, vehicle.OccupiedRect(), "East OccupiedRect");
      Expect.IsEqual(vehicle.Position, root, "East Position");

      vehicle.DeSpawn();
      Assert.IsFalse(vehicle.Spawned);
      Assert.IsFalse(vehicle.Destroyed);
      Assert.IsFalse(vehicle.Discarded);

      // South
      occupiedRect = GenAdj.OccupiedRect(root, Rot4.South, size);
      GenSpawn.Spawn(vehicle, root, map, Rot4.South);
      Expect.IsEqual(occupiedRect, vehicle.OccupiedRect(), "South OccupiedRect");
      Expect.IsEqual(vehicle.Position, root, "South Position");

      vehicle.DeSpawn();
      Assert.IsFalse(vehicle.Spawned);
      Assert.IsFalse(vehicle.Destroyed);
      Assert.IsFalse(vehicle.Discarded);

      // West
      occupiedRect = GenAdj.OccupiedRect(root, Rot4.West, size);
      GenSpawn.Spawn(vehicle, root, map, Rot4.West);
      Expect.IsEqual(occupiedRect, vehicle.OccupiedRect(), "West OccupiedRect");
      Expect.IsEqual(vehicle.Position, root, "West Position");

      vehicle.Destroy();
      Assert.IsFalse(vehicle.Spawned);
      Assert.IsTrue(vehicle.Destroyed);
    }
  }
}
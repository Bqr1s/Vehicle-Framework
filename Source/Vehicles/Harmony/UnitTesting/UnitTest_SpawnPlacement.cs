using DevTools;
using DevTools.UnitTesting;
using Verse;

namespace Vehicles.Testing
{
  // NOTE - Both GenAdj.OccupiedRect and GenSpawn.Spawn have patches that adjust positions for
  // vehicles. We can verify the adjustment keeps the vehicle stable (and doesn't shift positions)
  // by comparing the CellRects of entity-based occupied rect vs. size based (which is not patched)
  [UnitTest(TestType.Playing)]
  internal class UnitTest_SpawnPlacement : UnitTest_MapTest
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
        Expect.IsTrue("North OccupiedRect", occupiedRect == vehicle.OccupiedRect());
        Expect.IsTrue("North Position", vehicle.Position == root);

        vehicle.DeSpawn();
        Assert.IsFalse(vehicle.Spawned);
        Assert.IsFalse(vehicle.Destroyed);
        Assert.IsFalse(vehicle.Discarded);

        // East
        occupiedRect = GenAdj.OccupiedRect(root, Rot4.East, size);
        GenSpawn.Spawn(vehicle, root, map, Rot4.East);
        Expect.IsTrue("East OccupiedRect", occupiedRect == vehicle.OccupiedRect());
        Expect.IsTrue("East Position", vehicle.Position == root);

        vehicle.DeSpawn();
        Assert.IsFalse(vehicle.Spawned);
        Assert.IsFalse(vehicle.Destroyed);
        Assert.IsFalse(vehicle.Discarded);

        // South
        occupiedRect = GenAdj.OccupiedRect(root, Rot4.South, size);
        GenSpawn.Spawn(vehicle, root, map, Rot4.South);
        Expect.IsTrue("South OccupiedRect", occupiedRect == vehicle.OccupiedRect());
        Expect.IsTrue("South Position", vehicle.Position == root);

        vehicle.DeSpawn();
        Assert.IsFalse(vehicle.Spawned);
        Assert.IsFalse(vehicle.Destroyed);
        Assert.IsFalse(vehicle.Discarded);

        // West
        occupiedRect = GenAdj.OccupiedRect(root, Rot4.West, size);
        GenSpawn.Spawn(vehicle, root, map, Rot4.West);
        Expect.IsTrue("West OccupiedRect", occupiedRect == vehicle.OccupiedRect());
        Expect.IsTrue("West Position", vehicle.Position == root);

        vehicle.Destroy();
        Assert.IsFalse(vehicle.Spawned);
        Assert.IsTrue(vehicle.Destroyed);
      }
    }
  }
}
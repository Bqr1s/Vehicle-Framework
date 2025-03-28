using SmashTools.Debugging;
using Verse;

namespace Vehicles.Testing
{
  // NOTE - Both GenAdj.OccupiedRect and GenSpawn.Spawn have patches that adjust positions for
  // vehicles. We can verify the adjustment keeps the vehicle stable (and doesn't shift positions)
  // by comparing the CellRects of entity-based occupied rect vs. size based (which is not patched)
  internal class UnitTestSpawnPlacement : UnitTestMapTest
  {
    public override string Name => "SpawnPlacement";

    protected override UTResult TestVehicle(VehiclePawn vehicle, IntVec3 root)
    {
      IntVec2 size = vehicle.VehicleDef.Size;

      UTResult result = new();

      result.Add($"SpawnPlacement_{vehicle.def.defName} (Unspawned)", !vehicle.Spawned);

      // North
      CellRect occupiedRect = GenAdj.OccupiedRect(root, Rot4.North, size);
      GenSpawn.Spawn(vehicle, root, TestMap, Rot4.North);
      result.Add("SpawnPlacement (North)", occupiedRect == vehicle.OccupiedRect());
      result.Add("SpawnPlacement (Position)",
        (vehicle.Position - root).LengthHorizontalSquared <= 2);

      vehicle.DeSpawn();

      // East
      occupiedRect = GenAdj.OccupiedRect(root, Rot4.East, size);
      GenSpawn.Spawn(vehicle, root, TestMap, Rot4.East);
      result.Add("SpawnPlacement (East)", occupiedRect == vehicle.OccupiedRect());
      result.Add("SpawnPlacement (Position)",
        (vehicle.Position - root).LengthHorizontalSquared <= 2);

      vehicle.DeSpawn();

      // South
      occupiedRect = GenAdj.OccupiedRect(root, Rot4.South, size);
      GenSpawn.Spawn(vehicle, root, TestMap, Rot4.South);
      result.Add("SpawnPlacement (South)", occupiedRect == vehicle.OccupiedRect());
      result.Add("SpawnPlacement (Position)",
        (vehicle.Position - root).LengthHorizontalSquared <= 2);

      vehicle.DeSpawn();

      // West
      occupiedRect = GenAdj.OccupiedRect(root, Rot4.West, size);
      GenSpawn.Spawn(vehicle, root, TestMap, Rot4.West);
      result.Add("SpawnPlacement (West)", occupiedRect == vehicle.OccupiedRect());
      result.Add("SpawnPlacement (Position)",
        (vehicle.Position - root).LengthHorizontalSquared <= 2);

      // Vehicle will get destroyed from parent, we can just pass it off
      return result;
    }
  }
}
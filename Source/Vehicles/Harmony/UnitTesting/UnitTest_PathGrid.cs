using SmashTools;
using SmashTools.UnitTesting;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Vehicles.Testing
{
  internal class UnitTest_PathGrid : UnitTest_MapTest
  {
    public override string Name => "PathGrid";

    protected override UTResult TestVehicle(VehiclePawn vehicle, IntVec3 root)
    {
      int maxSize = Mathf.Max(vehicle.VehicleDef.Size.x, vehicle.VehicleDef.Size.z);

      UTResult result = new();
      IntVec3 reposition = root + new IntVec3(maxSize, 0, 0);
      VehicleMapping mapping = TestMap.GetCachedMapComponent<VehicleMapping>();
      VehicleMapping.VehiclePathData pathData = mapping[vehicle.VehicleDef];
      TerrainDef terrainDef = TestMap.terrainGrid.TerrainAt(root);

      VehiclePathGrid pathGrid = pathData.VehiclePathGrid;
      GenSpawn.Spawn(vehicle, root, TestMap);
      result.Add($"VehiclePathGrid_{vehicle.def} Spawned", vehicle.Spawned);

      HitboxTester<int> positionTester = new(vehicle, root,
        (cell) => pathGrid.CalculatedCostAt(cell),
        (cost) => cost == VehiclePathGrid.TerrainCostAt(vehicle.VehicleDef, terrainDef));
      positionTester.Start();

      // Spawn
      bool success = positionTester.All(true);
      result.Add("VehiclePathGrid (Spawn)", success);

      // set_Position
      vehicle.Position = reposition;
      success = positionTester.All(true);
      vehicle.Position = root;
      result.Add("VehiclePathGrid (set_Position)", success);

      // set_Rotation
      vehicle.Rotation = Rot4.East;
      success = positionTester.All(true);
      vehicle.Rotation = Rot4.North;
      result.Add("VehiclePathGrid (set_Rotation)", success);

      // Despawn
      vehicle.DeSpawn();
      success = positionTester.All(true);
      result.Add("VehiclePathGrid (DeSpawn)", success);

      // Vanilla PathGrid costs should take vehicles into account
      PathGrid vanillaPathGrid = TestMap.pathing.Normal.pathGrid;
      positionTester = new(vehicle, root,
        (cell) => vanillaPathGrid.CalculatedCostAt(cell, true, IntVec3.Invalid),
        (cost) => cost == terrainDef.pathCost ||
          (cost == PathGrid.ImpassableCost &&
            terrainDef.passability == Traversability.Impassable));
      positionTester.Start();

      GenSpawn.Spawn(vehicle, root, TestMap);
      result.Add($"{vehicle.def.defName} Spawned", vehicle.Spawned);

      // Spawn
      success = terrainDef.passability == Traversability.Impassable ?
        positionTester.All(true) :
        positionTester.Hitbox(false);
      result.Add("PathGrid (Spawn)", success);

      // set_Position
      vehicle.Position = reposition;
      success = terrainDef.passability == Traversability.Impassable ?
        positionTester.All(true) :
        positionTester.Hitbox(false);
      vehicle.Position = root;
      result.Add("PathGrid (set_Position)", success);

      // set_Rotation
      vehicle.Rotation = Rot4.East;
      success = terrainDef.passability == Traversability.Impassable ?
        positionTester.All(true) :
        positionTester.Hitbox(false);
      vehicle.Rotation = Rot4.North;
      result.Add("PathGrid (set_Rotation)", success);

      // Despawn
      vehicle.DeSpawn();
      success = positionTester.All(true);
      result.Add("PathGrid (DeSpawn)", success);

      return result;
    }
  }
}
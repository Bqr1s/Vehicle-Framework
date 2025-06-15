using DevTools;
using DevTools.UnitTesting;
using SmashTools;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Vehicles.Testing
{
  [UnitTest(TestType.Playing)]
  internal class UnitTest_PathGrid : UnitTest_MapTest
  {
    [Test]
    private void PathGrid()
    {
      foreach (VehiclePawn vehicle in vehicles)
      {
        using VehicleTestCase vtc = new(vehicle, this);

        int maxSize = Mathf.Max(vehicle.VehicleDef.Size.x, vehicle.VehicleDef.Size.z);

        IntVec3 reposition = root + new IntVec3(maxSize, 0, 0);
        VehicleMapping mapping = map.GetCachedMapComponent<VehicleMapping>();
        VehicleMapping.VehiclePathData pathData = mapping[vehicle.VehicleDef];
        TerrainDef terrainDef = map.terrainGrid.TerrainAt(root);

        VehiclePathGrid pathGrid = pathData.VehiclePathGrid;
        GenSpawn.Spawn(vehicle, root, map);
        Assert.IsTrue(vehicle.Spawned);

        HitboxTester<int> positionTester = new(vehicle, root,
          (cell) => pathGrid.CalculatedCostAt(cell),
          (cost) => cost == VehiclePathGrid.TerrainCostAt(vehicle.VehicleDef, terrainDef));
        positionTester.Start();

        // Spawn
        Expect.IsTrue("VehiclePathGrid (Spawn)", positionTester.All(true));

        // set_Position
        vehicle.Position = reposition;
        Expect.IsTrue("VehiclePathGrid (set_Position)", positionTester.All(true));
        vehicle.Position = root;

        // set_Rotation
        vehicle.Rotation = Rot4.East;
        Expect.IsTrue("VehiclePathGrid (set_Rotation)", positionTester.All(true));
        vehicle.Rotation = Rot4.North;

        // Despawn
        vehicle.DeSpawn();
        Expect.IsTrue("VehiclePathGrid (DeSpawn)", positionTester.All(true));

        // Vanilla PathGrid costs should take vehicles into account
        PathGrid vanillaPathGrid = map.pathing.Normal.pathGrid;
        positionTester = new HitboxTester<int>(vehicle, root,
          (cell) => vanillaPathGrid.CalculatedCostAt(cell, true, IntVec3.Invalid),
          (cost) => cost == terrainDef.pathCost ||
            (cost == Verse.AI.PathGrid.ImpassableCost &&
              terrainDef.passability == Traversability.Impassable));
        positionTester.Start();

        GenSpawn.Spawn(vehicle, root, map);
        Expect.IsTrue($"{vehicle.def.defName} Spawned", vehicle.Spawned);

        // Spawn
        Expect.IsTrue("PathGrid (Spawn)", terrainDef.passability == Traversability.Impassable ?
          positionTester.All(true) :
          positionTester.Hitbox(false));

        // set_Position
        vehicle.Position = reposition;
        Expect.IsTrue("PathGrid (set_Position)",
          terrainDef.passability == Traversability.Impassable ?
            positionTester.All(true) :
            positionTester.Hitbox(false));
        vehicle.Position = root;

        // set_Rotation
        vehicle.Rotation = Rot4.East;
        Expect.IsTrue("PathGrid (set_Rotation)",
          terrainDef.passability == Traversability.Impassable ?
            positionTester.All(true) :
            positionTester.Hitbox(false));
        vehicle.Rotation = Rot4.North;

        // Despawn
        vehicle.DeSpawn();
        Expect.IsTrue("PathGrid (DeSpawn)", positionTester.All(true));
      }
    }
  }
}
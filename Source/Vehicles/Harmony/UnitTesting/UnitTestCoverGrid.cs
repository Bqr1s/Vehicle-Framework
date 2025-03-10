﻿using SmashTools;
using SmashTools.Debugging;
using UnityEngine;
using Verse;

namespace Vehicles.Testing
{
  internal class UnitTestCoverGrid : UnitTestMapTest
  {
    public override string Name => "CoverGrid";

    protected override UTResult TestVehicle(VehiclePawn vehicle, IntVec3 root)
    {
      int maxSize = Mathf.Max(vehicle.VehicleDef.Size.x, vehicle.VehicleDef.Size.z);
      UTResult result = new();
      IntVec3 reposition = root + new IntVec3(maxSize, 0, 0);
      VehicleMapping mapping = TestMap.GetCachedMapComponent<VehicleMapping>();
      VehicleMapping.VehiclePathData pathData = mapping[vehicle.VehicleDef];

      CoverGrid coverGrid = TestMap.coverGrid;
      GenSpawn.Spawn(vehicle, root, TestMap);
      HitboxTester<Thing> coverTester = new(vehicle, TestMap, root,
        (cell) => coverGrid[cell],
        (thing) => thing == vehicle);
      coverTester.Start();

      // Validate spawned vehicle shows up in cover grid
      bool success = coverTester.Hitbox(true);
      result.Add("Cover Grid (Spawn)", success);

      // Validate position set moves vehicle in cover grid
      vehicle.Position = reposition;
      success = coverTester.Hitbox(true);
      vehicle.Position = root;
      result.Add("Cover Grid (set_Position)", success);

      // Validate rotation set moves vehicle in cover grid
      vehicle.Rotation = Rot4.East;
      success = coverTester.Hitbox(true);
      vehicle.Rotation = Rot4.North;
      result.Add("Cover Grid (set_Rotation)", success);

      // Validate despawning reverts back to thing before vehicle was spawned
      vehicle.DeSpawn();
      success = coverTester.All(false);
      result.Add("Cover Grid (DeSpawn)", success);
      return result;
    }
  }
}

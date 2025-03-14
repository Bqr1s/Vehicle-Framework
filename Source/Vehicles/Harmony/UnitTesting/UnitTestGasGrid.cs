﻿using System.Linq;
using SmashTools;
using SmashTools.Debugging;
using UnityEngine;
using Verse;

namespace Vehicles.Testing
{
  internal class UnitTestGasGrid : UnitTestMapTest
  {
    public override string Name => "GasGrid";

    protected override UTResult TestVehicle(VehiclePawn vehicle, IntVec3 root)
    {
      int maxSize = Mathf.Max(vehicle.VehicleDef.Size.x, vehicle.VehicleDef.Size.z);
      UTResult result = new();
      IntVec3 reposition = root + new IntVec3(maxSize, 0, 0);
      CellRect testArea = TestArea(vehicle.VehicleDef, root);
      VehicleMapping mapping = TestMap.GetCachedMapComponent<VehicleMapping>();
      VehicleMapping.VehiclePathData pathData = mapping[vehicle.VehicleDef];

      GasGrid gasGrid = TestMap.gasGrid;
      bool blocksGas = vehicle.VehicleDef.Fillage == FillCategory.Full;
      HitboxTester<bool> gasTester = new(vehicle, TestMap, root,
                                         gasGrid.AnyGasAt,
                                         // Gas can only occupy if vehicle Fillage != Full
                                         (gasAt) => gasAt == (!vehicle.Spawned || !blocksGas),
                                         (_) => gasGrid.Debug_ClearAll());
      gasTester.Start();

      gasGrid.Debug_FillAll();
      Assert.IsTrue(testArea.All(gasGrid.AnyGasAt));

      // Spawn
      GenSpawn.Spawn(vehicle, root, TestMap);
      result.Add($"{vehicle.def} Spawned", vehicle.Spawned);
      bool success = blocksGas ? gasTester.Hitbox(true) : gasTester.All(true);
      result.Add("Gas Grid (Spawn)", success);
      gasTester.Reset();

      // set_Position
      vehicle.Position = reposition;
      gasGrid.Debug_FillAll();
      success = blocksGas ? gasTester.Hitbox(true) : gasTester.All(true);
      vehicle.Position = root;
      result.Add("Gas Grid (set_Position)", success);
      gasTester.Reset();

      // set_Rotation
      vehicle.Rotation = Rot4.East;
      gasGrid.Debug_FillAll();
      success = blocksGas ? gasTester.Hitbox(true) : gasTester.All(true);
      vehicle.Rotation = Rot4.North;
      result.Add("Gas Grid (set_Rotation)", success);
      gasTester.Reset();

      // Despawn
      vehicle.DeSpawn();
      gasGrid.Debug_FillAll();
      success = gasTester.All(true);
      result.Add("Gas Grid (DeSpawn)", success);
      gasTester.Reset();
      return result;
    }
  }
}
﻿using System.Linq;
using DevTools;
using DevTools.UnitTesting;
using UnityEngine;
using Verse;
using TestType = DevTools.UnitTesting.TestType;

namespace Vehicles.Testing
{
  [UnitTest(TestType.Playing)]
  internal class UnitTest_GasGrid : UnitTest_MapTest
  {
    [Test]
    private void GasGrid()
    {
      foreach (VehiclePawn vehicle in vehicles)
      {
        using VehicleTestCase vtc = new(vehicle, this);

        int maxSize = Mathf.Max(vehicle.VehicleDef.Size.x, vehicle.VehicleDef.Size.z);

        IntVec3 reposition = root + new IntVec3(maxSize, 0, 0);
        CellRect testArea = TestArea(vehicle.VehicleDef);

        GasGrid gasGrid = map.gasGrid;
        bool blocksGas = vehicle.VehicleDef.Fillage == FillCategory.Full;
        HitboxTester<bool> gasTester = new(vehicle, root,
          gasGrid.AnyGasAt,
          // Gas can only occupy if vehicle Fillage != Full
          (gasAt) => gasAt == (!vehicle.Spawned || !blocksGas),
          (_) => gasGrid.Debug_ClearAll());
        gasTester.Start();

        gasGrid.Debug_FillAll();
        Assert.IsTrue(testArea.All(gasGrid.AnyGasAt));

        // Spawn
        GenSpawn.Spawn(vehicle, root, map);
        Expect.IsTrue($"{vehicle.def} Spawned", vehicle.Spawned);
        Expect.IsTrue("Spawn", blocksGas ? gasTester.Hitbox(true) : gasTester.All(true));
        gasTester.Reset();

        // set_Position
        vehicle.Position = reposition;
        gasGrid.Debug_FillAll();
        Expect.IsTrue("set_Position",
          blocksGas ? gasTester.Hitbox(true) : gasTester.All(true));
        vehicle.Position = root;
        gasTester.Reset();

        // set_Rotation
        vehicle.Rotation = Rot4.East;
        gasGrid.Debug_FillAll();
        Expect.IsTrue("set_Rotation",
          blocksGas ? gasTester.Hitbox(true) : gasTester.All(true));
        vehicle.Rotation = Rot4.North;
        gasTester.Reset();

        // Despawn
        vehicle.DeSpawn();
        gasGrid.Debug_FillAll();
        Expect.IsTrue("DeSpawn", gasTester.All(true));
        gasTester.Reset();
      }
    }
  }
}